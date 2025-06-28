using HarmonyLib;
using UnityEngine;

namespace Powerups;

[HarmonyPatch(typeof(PIDController))]
public static class PIDControllerPatch
{
  [HarmonyPostfix]
  [HarmonyPatch("Update")]
  public static void PIDControllerPatch_Update(PIDController __instance)
  {
    __instance.outputMax = 40;
    __instance.outputMin = -40;
  }
}

[HarmonyPatch(typeof(VelocityLean))]
public static class VelocityLeanPatch
{
  [HarmonyPrefix]
  [HarmonyPatch("FixedUpdate")]
  public static bool VelocityLeanPatch_FixedUpdate(VelocityLean __instance)
  {
    if (__instance.Rigidbody.angularVelocity.magnitude > 150.0f) return Constants.SKIP;
    if (__instance.Rigidbody.linearVelocity.magnitude > 150.0f) return Constants.SKIP;
    
    return Constants.CONTINUE;
  }
}