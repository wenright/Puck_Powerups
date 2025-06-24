using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Powerups;

[HarmonyPatch(typeof(PlayerBodyV2))]
public static class PlayerBodyV2_Patch
{
    public static Dictionary<Player, PowerupManager> powerupManagers = new Dictionary<Player, PowerupManager>();

    public static float cooldown = 10.0f;

    [HarmonyPostfix]
    [HarmonyPatch("OnNetworkPostSpawn")]
    public static void Patch_PlayerBodyV2_OnNetworkPostSpawn(PlayerBodyV2 __instance)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        powerupManagers[__instance.Player] = new PowerupManager(__instance.Player);
    }

    [HarmonyPostfix]
    [HarmonyPatch("FixedUpdate")]
    public static void Patch_PlayerBodyV2_FixedUpdate(PlayerBodyV2 __instance)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (GameManager.Instance.Phase != GamePhase.Playing) return;
        if (!powerupManagers.TryGetValue(__instance.Player, out PowerupManager powerupManager)) return;

        if (powerupManager.availablePowerup == null && powerupManager.CanUse())
        {
            Powerup nextPowerup = powerupManager.GenerateNextPowerup();
            UIChat.Instance.Server_ChatMessageRpc($"<b><color={nextPowerup.color}>{nextPowerup.name}</color></b> is ready to use", UIChat.Instance.RpcTarget.Group(new[] { __instance.Player.OwnerClientId }, RpcTargetUse.Temp));
        }

        if (powerupManager.activePowerup == null) return;

        // Reset active Powerup when duration is over
        if (Time.time > powerupManager.lastUsedAt + powerupManager.activePowerup.duration)
        {
            powerupManager.End();
            return;
        }

        Puck puck = PuckManager.Instance.GetPuck();
        if (!puck) return;

        Vector3 bladePosition = __instance.Stick.BladeHandlePosition;

        switch (powerupManager.activePowerup.name)
        {
            case PowerupNames.Magnet:
                float magnetRange = 3.5f;
                float magnetForce = 700.0f;

                float puckDistance = Vector3.Distance(puck.transform.position, bladePosition);
                if (puckDistance > magnetRange) return;

                Vector3 puckDirection = (bladePosition - puck.transform.position).normalized;
                puck.Rigidbody.AddForce(puckDirection * magnetForce * Time.fixedDeltaTime);

                break;
            case PowerupNames.Lasso:
                float lassoForce = 1000.0f;

                puckDirection = (bladePosition - puck.transform.position).normalized;
                puck.Rigidbody.AddForce(puckDirection * lassoForce * Time.fixedDeltaTime);

                break;
            case PowerupNames.Grapple:
                float grappleSpeed = 20.0f;

                Vector3 directionFromPlayer = (__instance.transform.position - puck.transform.position).normalized;
                __instance.Rigidbody.linearVelocity = -(directionFromPlayer) * grappleSpeed;

                // Grapple should end early if we reach the puck
                if (Vector3.Distance(puck.transform.position, __instance.transform.position) < 1.5f)
                {
                    powerupManager.End();
                }
                
                break;
        }
    }


    // Handles Rage powerup
    [HarmonyPrefix]
    [HarmonyPatch("OnCollisionEnter")]
    public static bool Patch_OnCollisionEnter(Collision collision, PlayerBodyV2 __instance)
    {
        if (!NetworkManager.Singleton.IsServer) return Constants.CONTINUE;
        if (!powerupManagers.TryGetValue(__instance.Player, out PowerupManager powerupManager)) return Constants.CONTINUE;
        if (powerupManager.activePowerup == null || powerupManager.activePowerup.name != PowerupNames.Rage) return Constants.CONTINUE;

        PlayerBodyV2 component = collision.gameObject.GetComponent<PlayerBodyV2>();
        if (!component)
        {
            return Constants.CONTINUE;
        }

        component.OnSlip();

        float knockbackPower = 6.0f;
        component.Rigidbody.AddForceAtPosition(-collision.relativeVelocity.normalized * knockbackPower, __instance.Rigidbody.worldCenterOfMass + __instance.transform.up * 0.5f, ForceMode.VelocityChange);

        return Constants.SKIP;
    }
}