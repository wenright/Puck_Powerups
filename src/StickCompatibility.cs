using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;

namespace Powerups;

public static class StickCompatibility
{
    private static readonly MethodInfo PlayerGetter = AccessTools.PropertyGetter(typeof(Stick), "Player");
    private static readonly MethodInfo StickPlayerBodyGetter = AccessTools.PropertyGetter(typeof(Stick), "PlayerBody");
    private static readonly FieldInfo StickPlayerReferenceField = AccessTools.Field(typeof(Stick), "PlayerReference");
    private static readonly MethodInfo PlayerBodyPlayerGetter = AccessTools.PropertyGetter(typeof(PlayerBody), "Player");
    private static readonly FieldInfo PlayerBodyPlayerReferenceField = AccessTools.Field(typeof(PlayerBody), "PlayerReference");
    private static readonly MethodInfo PlayerPlayerBodyGetter = AccessTools.PropertyGetter(typeof(Player), "PlayerBody");
    private static readonly FieldInfo PlayerPlayerBodyField = AccessTools.Field(typeof(Player), "PlayerBody");

    public static Player GetPlayer(Stick stick)
    {
        PlayerBody playerBody = GetPlayerBody(stick);
        Player player = GetPlayer(playerBody);
        if (player) return player;

        player = InvokeGetter<Player>(PlayerGetter, stick);
        if (player) return player;

        return GetPlayerFromReferenceField(StickPlayerReferenceField, stick);
    }

    public static Player GetPlayer(PlayerBody playerBody)
    {
        if (!playerBody) return null;

        Player player = InvokeGetter<Player>(PlayerBodyPlayerGetter, playerBody);
        if (player) return player;

        return GetPlayerFromReferenceField(PlayerBodyPlayerReferenceField, playerBody);
    }

    public static PlayerBody GetPlayerBody(Stick stick)
    {
        return InvokeGetter<PlayerBody>(StickPlayerBodyGetter, stick);
    }

    public static PlayerBody GetPlayerBody(Player player)
    {
        if (!player) return null;

        PlayerBody playerBody = InvokeGetter<PlayerBody>(PlayerPlayerBodyGetter, player);
        if (playerBody) return playerBody;

        return PlayerPlayerBodyField?.GetValue(player) as PlayerBody;
    }

    private static T InvokeGetter<T>(MethodInfo getter, object instance) where T : class
    {
        if (instance == null || getter == null) return null;

        try
        {
            return getter.Invoke(instance, null) as T;
        }
        catch (MissingMemberException)
        {
            return null;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is MissingMemberException)
        {
            return null;
        }
    }

    private static Player GetPlayerFromReferenceField(FieldInfo referenceField, object instance)
    {
        if (referenceField == null || instance == null) return null;

        try
        {
            object referenceVariable = referenceField.GetValue(instance);
            if (referenceVariable == null) return null;

            MethodInfo valueGetter = AccessTools.PropertyGetter(referenceVariable.GetType(), "Value");
            object referenceValue = valueGetter?.Invoke(referenceVariable, null);
            if (!(referenceValue is NetworkObjectReference objectReference)) return null;

            if (!objectReference.TryGet(out NetworkObject networkObject, null) || !networkObject) return null;
            return networkObject.GetComponent<Player>();
        }
        catch (MissingMemberException)
        {
            return null;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is MissingMemberException)
        {
            return null;
        }
    }
}
