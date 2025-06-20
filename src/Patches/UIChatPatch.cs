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
    if (!NetworkManager.Singleton.IsServer || message != ___quickChatMessages[0][0])
      return Constants.CONTINUE;

    if (!PlayerBodyV2_Patch.powerupManagers.TryGetValue(player, out PowerupManager powerupManager)) return Constants.CONTINUE;

    Debug.Log($"Next available at {powerupManager.nextPowerupAvailableAt}. Last used: {powerupManager.lastUsedAt}. Time: {Time.time}. Count: {PlayerBodyV2_Patch.powerupManagers.Count}");

    if (!powerupManager.CanUse())
    {
      Debug.Log("Can't use Powerup, still on cooldown");
	    float msRemaining = powerupManager.nextPowerupAvailableAt - Time.time;
		  string formattedMsRemaining = msRemaining.ToString("0.00");

      __instance.Server_ChatMessageRpc($"Powerup on cooldown for <b>{formattedMsRemaining}</b>s", __instance.RpcTarget.Group(new[] { player.OwnerClientId }, RpcTargetUse.Temp));
      return Constants.SKIP;
    }
    Debug.Log("Using Powerup");

    Powerup PowerupUsed = powerupManager.UsePowerup();

    __instance.Server_ChatMessageRpc(__instance.WrapPlayerUsername(player) + $" used <b><color={PowerupUsed.color}>{PowerupUsed.name}</color></b>", __instance.RpcTarget.ClientsAndHost);

    return Constants.SKIP;
  }
}
