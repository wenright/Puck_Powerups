using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Powerups;

// Abilities: lasso, magnet, rage, grappling hook, glue
[HarmonyPatch(typeof(PlayerBodyV2))]
public static class PlayerBodyV2_Patch
{
    public static Dictionary<Player, AbilityManager> abilityManagers = new Dictionary<Player, AbilityManager>();

    public static float cooldown = 10.0f;

    [HarmonyPostfix]
    [HarmonyPatch("OnNetworkPostSpawn")]
    public static void Patch_PlayerBodyV2_OnNetworkPostSpawn(PlayerBodyV2 __instance)
    {
        abilityManagers[__instance.Player] = new AbilityManager(__instance.Player);
    }

    [HarmonyPostfix]
    [HarmonyPatch("FixedUpdate")]
    public static void Patch_PlayerBodyV2_FixedUpdate(PlayerBodyV2 __instance)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (!abilityManagers.TryGetValue(__instance.Player, out AbilityManager abilityManager)) return;

        if (abilityManager.availableAbility == null && abilityManager.CanUse())
        {
            Ability nextAbility = abilityManager.GenerateNextAbility();
            UIChat.Instance.Server_ChatMessageRpc($"<color={nextAbility.color}>{nextAbility.name}</color> is ready to use", UIChat.Instance.RpcTarget.Group(new[] { __instance.Player.OwnerClientId }, RpcTargetUse.Temp));
        }

        if (abilityManager.activeAbility == null) return;
        
        Puck puck = PuckManager.Instance.GetPuck();

        switch (abilityManager.activeAbility.name)
        {
            case AbilityNames.Magnet:
                float magnetRange = 3.0f;
                float magnetForce = 700.0f;
                Vector3 bladePosition = __instance.Stick.BladeHandlePosition;

                if (Vector3.Distance(puck.transform.position, bladePosition) > magnetRange) return;

                puck.Rigidbody.AddForce((bladePosition - puck.transform.position) * magnetForce * Time.fixedDeltaTime);
                
                break;
        }
    }
}