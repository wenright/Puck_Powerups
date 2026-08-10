using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Powerups;

public static class UIChatPatch
{
  private static readonly MethodInfo SendChatMessage = HarmonyLib.AccessTools.Method(typeof(ChatManager), "Server_SendChatMessage", new[] { typeof(string), typeof(string), typeof(ulong[]) });
  private static readonly MethodInfo SendChatMessageToClients = HarmonyLib.AccessTools.Method(typeof(ChatManager), "Server_SendChatMessageToClients", new[] { typeof(string), typeof(ulong[]) });
  private static readonly MethodInfo BroadcastChatMessage = HarmonyLib.AccessTools.Method(typeof(ChatManager), "Server_BroadcastChatMessage", new[] { typeof(string), typeof(string) });
  private static readonly MethodInfo BroadcastChatMessageLegacy = HarmonyLib.AccessTools.Method(typeof(ChatManager), "Server_BroadcastChatMessage", new[] { typeof(string) });

  public static void SendToPlayer(string message, Player player)
  {
    SendToClients(message, new[] { player.OwnerClientId });
  }

  public static void Broadcast(string message)
  {
    ChatManager chatManager = ChatManager.Instance;
    if (!chatManager) return;

    if (TryInvoke(BroadcastChatMessage, chatManager, message, null)) return;
    if (TryInvoke(BroadcastChatMessageLegacy, chatManager, message)) return;

    ulong[] clientIds = PlayerManager.Instance.GetPlayers(false).Select(player => player.OwnerClientId).ToArray();
    SendToClients(message, clientIds);
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

  private static void SendToClients(string message, ulong[] clientIds)
  {
    ChatManager chatManager = ChatManager.Instance;
    if (!chatManager) return;

    if (TryInvoke(SendChatMessage, chatManager, message, null, clientIds)) return;
    if (TryInvoke(SendChatMessageToClients, chatManager, message, clientIds)) return;

    Debug.LogWarning("Powerups could not find a compatible ChatManager server-send method.");
  }

  private static bool TryInvoke(MethodInfo method, object instance, params object[] parameters)
  {
    if (method == null || instance == null) return false;

    try
    {
      method.Invoke(instance, parameters);
      return true;
    }
    catch (MissingMemberException)
    {
      return false;
    }
    catch (TargetInvocationException exception) when (exception.InnerException is MissingMemberException)
    {
      return false;
    }
  }
}
