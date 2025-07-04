using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Powerups;

[HarmonyPatch(typeof(Puck))]
public static class Puck_Patch
{
  public static Stick glueTarget;
  public static Vector3 offset = Vector3.zero;
  public static FieldInfo bladeHandleField = AccessTools.Field(typeof(Stick), "bladeHandle");

  [HarmonyPostfix]
  [HarmonyPatch("OnCollisionStay")]
  public static void Patch_Puck_OnCollisionStay(Collision collision, Puck __instance)
  {
    if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)) return;
    if (glueTarget || collision == null || collision.gameObject == null) return;

    Stick stick = collision.gameObject.GetComponent<Stick>();
    if (!stick) return;
    if (!PlayerBodyV2_Patch.powerupManagers.TryGetValue(stick.Player, out PowerupManager powerupManager)) return;
    if (powerupManager.activePowerup == null || powerupManager.activePowerup.name != PowerupNames.Glue) return;

    float glueDurationSeconds = 1.75f;

    if (!glueTarget)
    {
      glueTarget = stick;
      offset = __instance.transform.position - stick.BladeHandlePosition;
      powerupManager.lastUsedAt = Time.time - powerupManager.activePowerup.duration + glueDurationSeconds;
    }
  }

  [HarmonyPostfix]
  [HarmonyPatch("FixedUpdate")]
  public static void Patch_PlayerBodyV2_FixedUpdate(Puck __instance)
  {
    if (!(NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)) return;
    if (!glueTarget) return;
    if (!PlayerBodyV2_Patch.powerupManagers.TryGetValue(glueTarget.Player, out PowerupManager powerupManager)) return;
    if (powerupManager == null) return;

    if (powerupManager.activePowerup == null || powerupManager.activePowerup.name != PowerupNames.Glue)
    {
      glueTarget = null;
      offset = Vector3.zero;
    }
    else
    {
      GameObject bladeHandle = (GameObject)bladeHandleField.GetValue(glueTarget);
      if (bladeHandle == null) return;

      __instance.Rigidbody.MovePosition(glueTarget.BladeHandlePosition + (bladeHandle.transform.rotation * offset));
      __instance.Rigidbody.MoveRotation(bladeHandle.transform.rotation);
      __instance.Rigidbody.linearVelocity = glueTarget.Rigidbody.linearVelocity;
    }
  }
}