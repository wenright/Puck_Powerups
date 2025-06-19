using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Powerups;

// Abilities: lasso, magnet, rage, grappling hook, glue

[HarmonyPatch(typeof(PlayerBodyV2), "FixedUpdate")]
public static class PlayerBodyV2Patch
{
    public static float cooldown = 10.0f;

    [HarmonyPostfix]
    public static void Patch_PlayerBodyV2_FixedUpdate(PlayerBodyV2 __instance)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (!UIChatPatch.lastAbilityUsedAt.TryGetValue(__instance.Player, out float lastAbilityUsedAt)) return;
        if (lastAbilityUsedAt + UIChatPatch.abilityDuration < Time.time) return;
        
        // Magnet
        Puck puck = PuckManager.Instance.GetPuck();

        float magnetRange = 3.0f;
        float magnetForce = 700.0f;
        Vector3 bladePosition = __instance.Stick.BladeHandlePosition;

        if (Vector3.Distance(puck.transform.position, bladePosition) > magnetRange) return;
        
        puck.Rigidbody.AddForce((bladePosition - puck.transform.position) * magnetForce * Time.fixedDeltaTime);
    }
}