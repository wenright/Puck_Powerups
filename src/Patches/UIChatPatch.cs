using AYellowpaper.SerializedCollections;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Powerups;

[HarmonyPatch(typeof(ChatManager), "Client_SendChatMessageRpc")]
public static class UIChatPatch
{
  [HarmonyPrefix]
  public static bool Patch_UIChat_Server_ProcessPlayerChatMessage(string content, bool isQuickChat, RpcParams rpcParams, SerializedDictionary<QuickChatCategory, QuickChat[]> ___quickChats, ChatManager __instance)
  {
    if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
      return Constants.CONTINUE;

    if (!isQuickChat)
      return Constants.CONTINUE;

    Player player = PlayerManager.Instance.GetPlayerByClientId(rpcParams.Receive.SenderClientId);
    if (!player)
      return Constants.CONTINUE;
      
    QuickChat[] informationQuickChats = ___quickChats[QuickChatCategory.Information];
    if (content != informationQuickChats[0].Content && content != informationQuickChats[1].Content)
      return Constants.CONTINUE;
    
    if (!PlayerBodyV2_Patch.powerupManagers.TryGetValue(player, out PowerupManager powerupManager)) return Constants.CONTINUE;

    float msRemaining = powerupManager.nextPowerupAvailableAt - Time.time;
    string formattedMsRemaining = msRemaining.ToString("0.0");

    if (!powerupManager.CanUse())
    {
      SendToPlayer($"Powerup on cooldown for <b>{formattedMsRemaining}</b>s", player);
      return Constants.SKIP;
    }

    if (content == informationQuickChats[1].Content)
    {
      if (powerupManager.CanUse())
      {
        SendToPlayer($"<b><color={powerupManager.availablePowerup.color}>{powerupManager.availablePowerup.name}</color></b> is ready to use", player);
      }
      return Constants.SKIP;
    }

    Powerup powerupUsed = powerupManager.UsePowerup();

    Broadcast($"{player.Username.Value} used <b><color={powerupUsed.color}>{powerupUsed.name}</color></b>");

    return Constants.SKIP;
  }

  public static void SendToPlayer(string message, Player player)
  {
    ChatManager.Instance.Server_SendChatMessageToClients(message, new[] { player.OwnerClientId });
  }

  public static void Broadcast(string message)
  {
    ChatManager.Instance.Server_BroadcastChatMessage(message);
  }
}
