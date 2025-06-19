using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Powerups;

[HarmonyPatch(typeof(UIChat), "Server_ProcessPlayerChatMessage")]
public static class UIChatPatch
{
  public static Dictionary<Player, float> lastAbilityUsedAt =  new Dictionary<Player, float>();
  public static float cooldown = 10.0f;
  public static float abilityDuration = 4f;
  
  [HarmonyPrefix]
  public static bool Patch_UIChat_Server_ProcessPlayerChatMessage(Player player, string message, ulong clientId, bool useTeamChat, bool isMuted, SerializedDictionary<int, string[]> ___quickChatMessages, UIChat __instance)
  {
    if (!NetworkManager.Singleton.IsServer ||
        message != ___quickChatMessages[0][0])
      return Constants.CONTINUE;
    float lastUsedTime = lastAbilityUsedAt.GetValueOrDefault(player, -cooldown);

    if (lastUsedTime + cooldown > Time.time)
    {
	    float msRemaining = lastUsedTime + cooldown - Time.time;
		  string formattedMsRemaining = msRemaining.ToString("0.00");

      __instance.Server_ChatMessageRpc($"Ability on cooldown for <b>{formattedMsRemaining}</b>s", __instance.RpcTarget.Group(new[] { player.OwnerClientId }, RpcTargetUse.Temp));
      return Constants.SKIP;
    }

    lastAbilityUsedAt[player] = Time.time + abilityDuration;

    __instance.Server_ChatMessageRpc(__instance.WrapPlayerUsername(player) + $" used <color=magenta>Magnet</color>", __instance.RpcTarget.ClientsAndHost);

    return Constants.SKIP;
  }
}
