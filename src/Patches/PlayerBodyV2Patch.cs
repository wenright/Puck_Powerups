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

    /// <summary>
    /// Applies tornado force to a rigidbody at a given position
    /// </summary>
    /// <param name="rigidbody">The rigidbody to apply force to</param>
    /// <param name="objectPosition">The position of the object</param>
    /// <param name="tornadoCenter">The center position of the tornado</param>
    /// <param name="iceLevel">The Y level of the ice</param>
    /// <param name="forceMultiplier">Multiplier for force strength (higher for players)</param>
    /// <param name="tornadoRange">Maximum range of the tornado</param>
    /// <param name="maxTornadoForce">Maximum force strength</param>
    private static void ApplyTornadoForce(Rigidbody rigidbody, Vector3 objectPosition, Vector3 tornadoCenter, float iceLevel, float forceMultiplier, float tornadoRange, float maxTornadoForce)
    {
        Vector3 toObject = objectPosition - tornadoCenter;
        float distance = toObject.magnitude;

        // Only affect objects within range
        if (distance > tornadoRange) return;
        if (distance < 0.1f) return; // Avoid division by zero

        // Tornado force parameters
        float radialForceMultiplier = 0.15f;
        float circularForceMultiplier = 2.0f;
        float baseUpwardForce = 900.0f;
        float maxHeightAboveIce = 15.0f;
        float velocityDamping = 0.85f;
        float boundaryForceMultiplier = 7.5f;
        float boundaryStartRatio = 0.4f;
        Vector3 up = Vector3.up;

        // Inverse square falloff: force = maxForce * (range² / distance²)
        float forceStrength = maxTornadoForce * (tornadoRange * tornadoRange) / (distance * distance);

        // Radial force (pulling toward center)
        Vector3 radialDirection = -toObject.normalized;
        Vector3 radialForce = radialDirection * forceStrength * radialForceMultiplier * forceMultiplier * Time.fixedDeltaTime;

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
        Vector3 tangentialDirection = Vector3.Cross(radialDirection, up).normalized;
        Vector3 circularForce = tangentialDirection * forceStrength * circularForceMultiplier * circularForceScale * forceMultiplier * Time.fixedDeltaTime;

        // Upward force: flat amount that falls off when too far above ice
        float heightAboveIce = objectPosition.y - iceLevel;
        float upwardForceScale = 1.0f;
        if (heightAboveIce > maxHeightAboveIce)
        {
            // Falloff when too high above ice
            float excessHeight = heightAboveIce - maxHeightAboveIce;
            upwardForceScale = Mathf.Max(0.0f, 1.0f - (excessHeight / maxHeightAboveIce));
        }
        Vector3 upwardForce = up * baseUpwardForce * upwardForceScale * forceMultiplier * Time.fixedDeltaTime;

        // Boundary restoration force - gets stronger as object approaches edge
        Vector3 boundaryForce = Vector3.zero;
        if (distance > boundaryDistance)
        {
            float boundaryRatio = (distance - boundaryDistance) / (tornadoRange - boundaryDistance);
            float boundaryForceStrength = forceStrength * boundaryForceMultiplier * boundaryRatio;
            // Players get 5x boundary force multiplier
            float boundaryMultiplier = forceMultiplier > 1.0f ? 5.0f * forceMultiplier : forceMultiplier;
            boundaryForce = radialDirection * boundaryForceStrength * boundaryMultiplier * Time.fixedDeltaTime;
        }

        // Apply velocity damping to reduce radial velocity component (prevents slingshotting)
        Vector3 velocity = rigidbody.linearVelocity;
        Vector3 radialVelocity = Vector3.Project(velocity, radialDirection);
        Vector3 tangentialVelocity = velocity - radialVelocity;
        // Damp the radial velocity more than tangential to maintain orbit
        rigidbody.linearVelocity = radialVelocity * velocityDamping + tangentialVelocity;

        // Apply all forces
        rigidbody.AddForce(radialForce + circularForce + upwardForce + boundaryForce);
    }

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
                float playerForceMultiplier = 50.0f; // Force multiplier for players (50x stronger than puck)
                Vector3 tornadoCenter = __instance.transform.position;
                float iceLevel = tornadoCenter.y; // Use tornado center Y as ice level reference

                // Apply tornado force to the puck if it exists
                if (puck)
                {
                    ApplyTornadoForce(puck.Rigidbody, puck.transform.position, tornadoCenter, iceLevel, 1.0f, tornadoRange, maxTornadoForce);
                }

                // Get all players and apply tornado force to nearby ones
                PlayerBodyV2[] allPlayers = GameObject.FindObjectsByType<PlayerBodyV2>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (PlayerBodyV2 otherPlayer in allPlayers)
                {
                    // Skip the player who activated the tornado
                    if (otherPlayer == __instance) continue;
                    if (otherPlayer.Player == null) continue;

                    ApplyTornadoForce(otherPlayer.Rigidbody, otherPlayer.transform.position, tornadoCenter, iceLevel, playerForceMultiplier, tornadoRange, maxTornadoForce);
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