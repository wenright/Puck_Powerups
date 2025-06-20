using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Powerups;

[HarmonyPatch(typeof(Puck))]
public static class Puck_Patch
{
  public static Stick glueTarget;
  public static Vector3 offset = Vector3.zero;

  [HarmonyPostfix]
  [HarmonyPatch("OnCollisionEnter")]
  public static void Patch_Puck_OnCollisionEnter(Collision collision, Puck __instance)
  {
    if (!NetworkManager.Singleton.IsServer) return;
    if (glueTarget || collision == null || collision.gameObject == null) return;

    Stick stick = collision.gameObject.GetComponent<Stick>();
    if (!stick) return;
    if (!PlayerBodyV2_Patch.powerupManagers.TryGetValue(stick.Player, out PowerupManager powerupManager)) return;
    if (powerupManager.activePowerup == null || powerupManager.activePowerup.name != PowerupNames.Glue) return;


    offset = __instance.transform.position - stick.BladeHandlePosition;
    glueTarget = stick;
    __instance.Rigidbody.useGravity = false;
    __instance.Rigidbody.detectCollisions = false;
  }

  [HarmonyPostfix]
  [HarmonyPatch("FixedUpdate")]
  public static void Patch_PlayerBodyV2_FixedUpdate(Puck __instance)
  {
    if (!NetworkManager.Singleton.IsServer) return;
    if (!glueTarget) return;
    if (!PlayerBodyV2_Patch.powerupManagers.TryGetValue(glueTarget.Player, out PowerupManager powerupManager)) return;
    if (powerupManager == null) return;

    if (powerupManager.activePowerup == null || powerupManager.activePowerup.name != PowerupNames.Glue)
    {
      glueTarget = null;
      offset = Vector3.zero;
      __instance.Rigidbody.useGravity = true;
      __instance.Rigidbody.detectCollisions = true;
    }
    else
    {
      __instance.Rigidbody.MovePosition(glueTarget.BladeHandlePosition + (glueTarget.transform.rotation * offset));
      __instance.Rigidbody.MoveRotation(glueTarget.transform.rotation);
      __instance.Rigidbody.linearVelocity = Vector3.zero;
    }
  }
}