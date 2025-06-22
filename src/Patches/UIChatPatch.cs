using AYellowpaper.SerializedCollections;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Powerups;

[HarmonyPatch(typeof(UIChat), "Server_ProcessPlayerChatMessage")]
public static class UIChatPatch
{
  [HarmonyPrefix]
  public static bool Patch_UIChat_Server_ProcessPlayerChatMessage(Player player, string message, ulong clientId, bool useTeamChat, bool isMuted, SerializedDictionary<int, string[]> ___quickChatMessages, UIChat __instance)
  {
    if (!NetworkManager.Singleton.IsServer || (message != ___quickChatMessages[0][0] && message != ___quickChatMessages[0][1]))
      return Constants.CONTINUE;
    
    if (!PlayerBodyV2_Patch.powerupManagers.TryGetValue(player, out PowerupManager powerupManager)) return Constants.CONTINUE;

    float msRemaining = powerupManager.nextPowerupAvailableAt - Time.time;
    string formattedMsRemaining = msRemaining.ToString("0.0");

    if (!powerupManager.CanUse())
    {
      __instance.Server_ChatMessageRpc($"Powerup on cooldown for <b>{formattedMsRemaining}</b>s", __instance.RpcTarget.Group(new[] { player.OwnerClientId }, RpcTargetUse.Temp));
      return Constants.SKIP;
    }

    if (message == ___quickChatMessages[0][1])
    {
      if (powerupManager.CanUse())
      {
        __instance.Server_ChatMessageRpc($"<b><color={powerupManager.availablePowerup.color}>{powerupManager.availablePowerup.name}</color></b> is ready to use", UIChat.Instance.RpcTarget.Group(new[] { player.OwnerClientId }, RpcTargetUse.Temp));
      }
      return Constants.SKIP;
    }

    Powerup powerupUsed = powerupManager.UsePowerup();

    __instance.Server_ChatMessageRpc(__instance.WrapPlayerUsername(player) + $" used <b><color={powerupUsed.color}>{powerupUsed.name}</color></b>", __instance.RpcTarget.ClientsAndHost);

    return Constants.SKIP;
  }
}
