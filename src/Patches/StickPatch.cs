using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace SingularityStopper;

// Prevents all physics objects from getting sucked into the middle of the ring when a player recieves enough momentum

[HarmonyPatch(typeof(Stick), "FixedUpdate")]
public class StickPhysicsFix
{
    static bool Prefix(Stick __instance,
        GameObject ___rotationContainer,
        float ___bladeAngleStep,
        Vector3PIDController ___shaftHandlePIDController,
        Vector3PIDController ___bladeHandlePIDController,
        GameObject ___shaftHandle,
        GameObject ___bladeHandle,
        float ___angularVelocityTransferMultiplier,
        float ___linearVelocityTransferMultiplier,
        ref bool ___transferAngularVelocity,
        ref float ___bladeHandleProportionalGainMultiplier,
        float ___shaftHandleProportionalGain,
        float ___shaftHandleProportionalGainMultiplier,
        float ___shaftHandleIntegralGain,
        float ___shaftHandleIntegralSaturation,
        float ___shaftHandleDerivativeGain,
        float ___shaftHandleDerivativeSmoothing,
        float ___bladeHandleProportionalGain,
        float ___bladeHandleIntegralGain,
        float ___bladeHandleIntegralSaturation,
        float ___bladeHandleDerivativeGain,
        float ___bladeHandleDerivativeSmoothing)
    {
        if (!__instance.Player || !__instance.StickPositioner)
            return false;
            
        var playerInput = __instance.Player.PlayerInput;
        if (!playerInput)
            return false;

        ___rotationContainer.transform.localRotation = Quaternion.AngleAxis(
            playerInput.BladeAngleInput.ServerValue * ___bladeAngleStep, 
            Vector3.forward);

        if (!NetworkManager.Singleton.IsServer)
            return false;

        ___shaftHandlePIDController.proportionalGain = ___shaftHandleProportionalGain * ___shaftHandleProportionalGainMultiplier;
        ___shaftHandlePIDController.integralGain = ___shaftHandleIntegralGain;
        ___shaftHandlePIDController.integralSaturation = ___shaftHandleIntegralSaturation;
        ___shaftHandlePIDController.derivativeGain = ___shaftHandleDerivativeGain;
        ___shaftHandlePIDController.derivativeSmoothing = ___shaftHandleDerivativeSmoothing;
        
        ___bladeHandlePIDController.proportionalGain = ___bladeHandleProportionalGain * ___bladeHandleProportionalGainMultiplier;
        ___bladeHandlePIDController.integralGain = ___bladeHandleIntegralGain;
        ___bladeHandlePIDController.integralSaturation = ___bladeHandleIntegralSaturation;
        ___bladeHandlePIDController.derivativeGain = ___bladeHandleDerivativeGain;
        ___bladeHandlePIDController.derivativeSmoothing = ___bladeHandleDerivativeSmoothing;

        Vector3 shaftForce = ___shaftHandlePIDController.Update(
            Time.fixedDeltaTime, 
            __instance.ShaftHandlePosition, 
            __instance.StickPositioner.ShaftTargetPosition);
            
        Vector3 bladeForce = ___bladeHandlePIDController.Update(
            Time.fixedDeltaTime, 
            __instance.BladeHandlePosition, 
            __instance.StickPositioner.BladeTargetPosition);

        Vector3 shaftPointVelocity = __instance.PlayerBody.Rigidbody.GetPointVelocity(___shaftHandle.transform.position);
        Vector3 bladePointVelocity = __instance.PlayerBody.Rigidbody.GetPointVelocity(___bladeHandle.transform.position);
        
        float maxPointVelocity = 100f;
        shaftPointVelocity = Vector3.ClampMagnitude(shaftPointVelocity, maxPointVelocity);
        bladePointVelocity = Vector3.ClampMagnitude(bladePointVelocity, maxPointVelocity);
        
        Vector3 shaftVelocityTransfer = shaftPointVelocity * ___linearVelocityTransferMultiplier * Time.fixedDeltaTime;
        Vector3 bladeVelocityTransfer = bladePointVelocity * ___linearVelocityTransferMultiplier * Time.fixedDeltaTime;

        __instance.Rigidbody.AddForceAtPosition(
            shaftVelocityTransfer, 
            ___shaftHandle.transform.position, 
            ForceMode.VelocityChange);
            
        __instance.Rigidbody.AddForceAtPosition(
            bladeVelocityTransfer, 
            ___bladeHandle.transform.position, 
            ForceMode.VelocityChange);

        __instance.Rigidbody.AddForceAtPosition(
            shaftForce * Time.fixedDeltaTime, 
            __instance.ShaftHandlePosition, 
            ForceMode.VelocityChange);
            
        __instance.Rigidbody.AddForceAtPosition(
            bladeForce * Time.fixedDeltaTime, 
            __instance.BladeHandlePosition, 
            ForceMode.VelocityChange);

        __instance.Rigidbody.angularVelocity = __instance.transform.TransformDirection(
            __instance.transform.InverseTransformVector(__instance.Rigidbody.angularVelocity) with
            {
                z = 0.0f
            });

        float maxAngularVel = 30f;
        __instance.Rigidbody.angularVelocity = Vector3.ClampMagnitude(
            __instance.Rigidbody.angularVelocity, maxAngularVel);

        Vector3 wrappedAngles = Utils.WrapEulerAngles(__instance.transform.eulerAngles);
        __instance.Rigidbody.MoveRotation(Quaternion.Euler(new Vector3(wrappedAngles.x, wrappedAngles.y, 0.0f)));

        Vector3 angularVelocityTransfer = Vector3.Scale(
            __instance.Rigidbody.angularVelocity, 
            new Vector3(0.5f, 1f, 0.0f)) * ___angularVelocityTransferMultiplier;

        if (___transferAngularVelocity)
            __instance.PlayerBody.Rigidbody.AddTorque(-angularVelocityTransfer, ForceMode.Acceleration);

        ___bladeHandleProportionalGainMultiplier = 1f;

        return false;
    }
}
