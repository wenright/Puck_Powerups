using HarmonyLib;
using Unity.Netcode;

namespace Powerups;

[HarmonyPatch(typeof(ChatManager), nameof(ChatManager.AddChatMessage))]
public static class ClientPresentationChatPatch
{
    [HarmonyPostfix]
    public static void Patch_ChatManager_AddChatMessage(ChatMessage chatMessage)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!networkManager || !networkManager.IsClient || chatMessage == null) return;

        PowerupRuntime.ObserveServerChat(chatMessage.Content.ToString());
    }
}
