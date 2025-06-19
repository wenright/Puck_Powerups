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
    if (!NetworkManager.Singleton.IsServer ||
        message != ___quickChatMessages[0][0])
      return Constants.CONTINUE;

    if (!PlayerBodyV2_Patch.abilityManagers.TryGetValue(player, out AbilityManager abilityManager)) return Constants.CONTINUE;

    Debug.Log($"Next available at {abilityManager.nextAbilityAvailableAt}. Last used: {abilityManager.lastUsedAt}. Time: {Time.time}. Count: {PlayerBodyV2_Patch.abilityManagers.Count}");

    if (!abilityManager.CanUse())
    {
      Debug.Log("Can't use ability, still on cooldown");
	    float msRemaining = abilityManager.nextAbilityAvailableAt - Time.time;
		  string formattedMsRemaining = msRemaining.ToString("0.00");

      __instance.Server_ChatMessageRpc($"Ability on cooldown for <b>{formattedMsRemaining}</b>s", __instance.RpcTarget.Group(new[] { player.OwnerClientId }, RpcTargetUse.Temp));
      return Constants.SKIP;
    }
    Debug.Log("Using ability");

    Ability abilityUsed = abilityManager.UseAbility();

    __instance.Server_ChatMessageRpc(__instance.WrapPlayerUsername(player) + $" used <color={abilityUsed.color}>{abilityUsed.name}</color>", __instance.RpcTarget.ClientsAndHost);

    return Constants.SKIP;
  }
}
