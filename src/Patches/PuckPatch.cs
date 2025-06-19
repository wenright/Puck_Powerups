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
    if (!PlayerBodyV2_Patch.abilityManagers.TryGetValue(stick.Player, out AbilityManager abilityManager)) return;
    if (abilityManager.activeAbility == null || abilityManager.activeAbility.name != AbilityNames.Glue) return;

    // __instance.Rigidbody.useGravity = false;
    offset = __instance.transform.position - stick.BladeHandlePosition;
    glueTarget = stick;
  }

  [HarmonyPostfix]
  [HarmonyPatch("FixedUpdate")]
  public static void Patch_PlayerBodyV2_FixedUpdate(PlayerBodyV2 __instance)
  {
    if (!NetworkManager.Singleton.IsServer) return;
    if (!glueTarget) return;


    __instance.Rigidbody.MovePosition(offset);

    if (!PlayerBodyV2_Patch.abilityManagers.TryGetValue(glueTarget.Player, out AbilityManager abilityManager)) return;
    if (abilityManager != null) return;

    if (abilityManager.activeAbility.name != AbilityNames.Glue) {
      glueTarget = null;
      offset = Vector3.zero;
      // __instance.Rigidbody.useGravity = true;
    }
  }
}