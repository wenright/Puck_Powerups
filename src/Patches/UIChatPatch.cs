using AYellowpaper.SerializedCollections;
using System.Collections;
using System.Linq;
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

    ResetRpcExecStage(__instance);

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

    BroadcastNextFrame($"{player.Username.Value} used <b><color={powerupUsed.color}>{powerupUsed.name}</color></b>");

    return Constants.SKIP;
  }

  public static void SendToPlayer(string message, Player player)
  {
    ChatManager.Instance.Server_SendChatMessageToClients(message, new[] { player.OwnerClientId });
  }

  public static void Broadcast(string message)
  {
    ulong[] clientIds = PlayerManager.Instance.GetPlayers(false).Select(player => player.OwnerClientId).ToArray();
    ChatManager.Instance.Server_SendChatMessageToClients(message, clientIds);
  }

  public static void BroadcastNextFrame(string message)
  {
    ChatManager.Instance.StartCoroutine(BroadcastNextFrameCoroutine(message));
  }

  private static IEnumerator BroadcastNextFrameCoroutine(string message)
  {
    yield return null;
    Broadcast(message);
  }

  private static void ResetRpcExecStage(ChatManager chatManager)
  {
    var rpcExecStageField = AccessTools.Field(typeof(NetworkBehaviour), "__rpc_exec_stage");
    object noneValue = System.Enum.ToObject(rpcExecStageField.FieldType, 0);
    rpcExecStageField.SetValue(chatManager, noneValue);
  }
}
