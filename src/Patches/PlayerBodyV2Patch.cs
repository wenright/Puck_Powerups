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
        if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)) return;
        powerupManagers[__instance.Player] = new PowerupManager(__instance.Player);
    }

    [HarmonyPostfix]
    [HarmonyPatch("FixedUpdate")]
    public static void Patch_PlayerBodyV2_FixedUpdate(PlayerBodyV2 __instance)
    {
        if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)) return;
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
        Vector3 bladePosition = __instance.Stick.BladeHandlePosition;
        float puckDistance = puck ? Vector3.Distance(puck.transform.position, bladePosition) : float.MaxValue;

        switch (powerupManager.activePowerup.name)
        {
            case PowerupNames.Magnet:
                if (!puck) return;
                float magnetRange = 3.5f;
                float magnetForce = 600.0f;

                if (puckDistance > magnetRange) return;

                Vector3 puckDirection = (bladePosition - puck.transform.position).normalized;
                puck.Rigidbody.AddForce(puckDirection * magnetForce * Time.fixedDeltaTime);

                break;
            case PowerupNames.Lasso:
                if (!puck) return;
                float lassoForce = 900.0f;

                puckDirection = (bladePosition - puck.transform.position).normalized;
                puck.Rigidbody.AddForce(puckDirection * lassoForce * Time.fixedDeltaTime);

                break;
            case PowerupNames.Grapple:
                if (!puck) return;
                float grappleSpeed = 20.0f;

                Vector3 directionFromPlayer = (__instance.transform.position - puck.transform.position).normalized;
                __instance.Rigidbody.linearVelocity = -directionFromPlayer * grappleSpeed;

                // Grapple should end early if we reach the puck
                if (Vector3.Distance(puck.transform.position, __instance.transform.position) < 2f)
                {
                    powerupManager.End();
                }
                
                break;
            case PowerupNames.Tornado:
                float tornadoRange = 8.0f;
                float maxTornadoForce = 600.0f;
                float radialForceMultiplier = 0.15f; // Reduced radial force to prevent overshooting
                float circularForceMultiplier = 2.0f; // Increased circular force to maintain orbit
                float baseUpwardForce = 900.0f; // Flat upward force amount (maxTornadoForce * 0.3)
                float maxHeightAboveIce = 15.0f; // Height above ice where upward force starts falling off
                float velocityDamping = 0.85f; // Stronger damping to reduce velocity buildup
                float boundaryForceMultiplier = 7.5f; // Force multiplier for boundary restoration
                float boundaryStartRatio = 0.4f; // Start applying boundary force at 70% of range
                float playerForceMultiplier = 50.0f; // Force multiplier for players (10x stronger than puck)
                Vector3 tornadoCenter = __instance.transform.position;
                Vector3 up = Vector3.up;
                float iceLevel = tornadoCenter.y; // Use tornado center Y as ice level reference

                // Apply tornado force to the puck if it exists
                if (puck)
                {
                    Vector3 toPuck = puck.transform.position - tornadoCenter;
                    float puckTornadoDistance = toPuck.magnitude;
                    if (puckTornadoDistance <= tornadoRange && puckTornadoDistance > 0.1f)
                    {
                        // Inverse square falloff: force = maxForce * (range² / distance²)
                        float puckForceStrength = maxTornadoForce * (tornadoRange * tornadoRange) / (puckTornadoDistance * puckTornadoDistance);

                        Vector3 puckRadialDirection = -toPuck.normalized;
                        Vector3 puckRadialForce = puckRadialDirection * puckForceStrength * radialForceMultiplier * Time.fixedDeltaTime;

                        // Reduce circular force near boundary to prevent escape
                        float boundaryDistance = tornadoRange * boundaryStartRatio;
                        float circularForceScale = 1.0f;
                        if (puckTornadoDistance > boundaryDistance)
                        {
                            // Linearly reduce circular force from boundary to edge
                            float boundaryRatio = (puckTornadoDistance - boundaryDistance) / (tornadoRange - boundaryDistance);
                            circularForceScale = 1.0f - boundaryRatio;
                        }

                        Vector3 puckTangentialDirection = Vector3.Cross(puckRadialDirection, up).normalized;
                        Vector3 puckCircularForce = puckTangentialDirection * puckForceStrength * circularForceMultiplier * circularForceScale * Time.fixedDeltaTime;

                        // Upward force: flat amount that falls off when too far above ice
                        float puckHeightAboveIce = puck.transform.position.y - iceLevel;
                        float puckUpwardForceScale = 1.0f;
                        if (puckHeightAboveIce > maxHeightAboveIce)
                        {
                            // Falloff when too high above ice
                            float excessHeight = puckHeightAboveIce - maxHeightAboveIce;
                            puckUpwardForceScale = Mathf.Max(0.0f, 1.0f - (excessHeight / maxHeightAboveIce));
                        }
                        Vector3 puckUpwardForce = up * baseUpwardForce * puckUpwardForceScale * Time.fixedDeltaTime;

                        // Boundary restoration force - gets stronger as object approaches edge
                        Vector3 puckBoundaryForce = Vector3.zero;
                        if (puckTornadoDistance > boundaryDistance)
                        {
                            float boundaryRatio = (puckTornadoDistance - boundaryDistance) / (tornadoRange - boundaryDistance);
                            float boundaryForceStrength = puckForceStrength * boundaryForceMultiplier * boundaryRatio;
                            puckBoundaryForce = puckRadialDirection * boundaryForceStrength * Time.fixedDeltaTime;
                        }

                        // Apply velocity damping to reduce radial velocity component (prevents slingshotting)
                        Vector3 puckVelocity = puck.Rigidbody.linearVelocity;
                        Vector3 puckRadialVelocity = Vector3.Project(puckVelocity, puckRadialDirection);
                        Vector3 puckTangentialVelocity = puckVelocity - puckRadialVelocity;
                        // Damp the radial velocity more than tangential to maintain orbit
                        puck.Rigidbody.linearVelocity = puckRadialVelocity * velocityDamping + puckTangentialVelocity;

                        puck.Rigidbody.AddForce(puckRadialForce + puckCircularForce + puckUpwardForce + puckBoundaryForce);
                    }
                }

                // Get all players and apply tornado force to nearby ones
                PlayerBodyV2[] allPlayers = GameObject.FindObjectsByType<PlayerBodyV2>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (PlayerBodyV2 otherPlayer in allPlayers)
                {
                    // Skip the player who activated the tornado
                    if (otherPlayer == __instance) continue;
                    if (otherPlayer.Player == null) continue;

                    Vector3 toOtherPlayer = otherPlayer.transform.position - tornadoCenter;
                    float distance = toOtherPlayer.magnitude;

                    // Only affect players within range
                    if (distance > tornadoRange) continue;
                    if (distance < 0.1f) continue; // Avoid division by zero

                    // Inverse square falloff: force = maxForce * (range² / distance²)
                    float forceStrength = maxTornadoForce * (tornadoRange * tornadoRange) / (distance * distance);

                    // Radial force (pulling toward center)
                    Vector3 radialDirection = -toOtherPlayer.normalized;
                    Vector3 radialForce = radialDirection * forceStrength * radialForceMultiplier * playerForceMultiplier * Time.fixedDeltaTime;

                    // Reduce circular force near boundary to prevent escape
                    float boundaryDistance = tornadoRange * boundaryStartRatio;
                    float circularForceScale = 1.0f;
                    if (distance > boundaryDistance)
                    {
                        // Linearly reduce circular force from boundary to edge
                        float boundaryRatio = (distance - boundaryDistance) / (tornadoRange - boundaryDistance);
                        circularForceScale = 1.0f - boundaryRatio;
                    }

                    // Circular force (tangential, perpendicular to radial)
                    // Use cross product with up vector to get tangential direction
                    Vector3 tangentialDirection = Vector3.Cross(radialDirection, up).normalized;
                    Vector3 circularForce = tangentialDirection * forceStrength * circularForceMultiplier * circularForceScale * playerForceMultiplier * Time.fixedDeltaTime;

                    // Upward force: flat amount that falls off when too far above ice
                    float playerHeightAboveIce = otherPlayer.transform.position.y - iceLevel;
                    float upwardForceScale = 1.0f;
                    if (playerHeightAboveIce > maxHeightAboveIce)
                    {
                        // Falloff when too high above ice
                        float excessHeight = playerHeightAboveIce - maxHeightAboveIce;
                        upwardForceScale = Mathf.Max(0.0f, 1.0f - (excessHeight / maxHeightAboveIce));
                    }
                    Vector3 upwardForce = up * baseUpwardForce * upwardForceScale * playerForceMultiplier * Time.fixedDeltaTime;

                    // Boundary restoration force - gets stronger as object approaches edge
                    Vector3 boundaryForce = Vector3.zero;
                    if (distance > boundaryDistance)
                    {
                        float boundaryRatio = (distance - boundaryDistance) / (tornadoRange - boundaryDistance);
                        float boundaryForceStrength = forceStrength * boundaryForceMultiplier * boundaryRatio;
                        boundaryForce = 5 * radialDirection * boundaryForceStrength * playerForceMultiplier * Time.fixedDeltaTime;
                    }

                    // Apply all forces
                    otherPlayer.Rigidbody.AddForce(radialForce + circularForce + upwardForce + boundaryForce);
                }

                break;
        }
    }


    // Handles Rage powerup, and ending glue on collisions
    [HarmonyPrefix]
    [HarmonyPatch("OnCollisionEnter")]
    public static bool Patch_OnCollisionEnter(Collision collision, PlayerBodyV2 __instance)
    {
        if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)) return Constants.CONTINUE;
        if (!powerupManagers.TryGetValue(__instance.Player, out PowerupManager powerupManager)) return Constants.CONTINUE;
        if (powerupManager.activePowerup == null) return Constants.CONTINUE;

        PlayerBodyV2 component = collision.gameObject.GetComponent<PlayerBodyV2>();
        if (!component)
        {
            return Constants.CONTINUE;
        }

        switch (powerupManager.activePowerup.name)
        {
            case PowerupNames.Rage:
                component.OnSlip();

                float knockbackPower = 8.0f;
                component.Rigidbody.AddForceAtPosition(-collision.relativeVelocity.normalized * knockbackPower, __instance.Rigidbody.worldCenterOfMass + __instance.transform.up * 0.5f, ForceMode.VelocityChange);
				component.Rigidbody.AddForce(Vector3.up * 15.0f);

                return Constants.SKIP;
            case PowerupNames.Glue:
                powerupManager.End();
            
                break;
        }

        return Constants.CONTINUE;
    }
}