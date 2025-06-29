using System;
using HarmonyLib;
using UnityEngine;

namespace Powerups;

public class Constants
{
  public const bool SKIP = false;
  public const bool CONTINUE = true;
}

public class Plugin : IPuckMod
{
  public static string MOD_NAME = "Powerups";
  public static string MOD_VERSION = "0.5.0";
  public static string MOD_GUID = "wenright.powerups";

  static readonly Harmony harmony = new Harmony(MOD_GUID);

  public bool OnEnable()
  {
    try
    {
      harmony.PatchAll();

      Debug.Log($"Enabled {MOD_GUID}");
      
      return true;
    }
    catch (Exception e)
    {
      Debug.LogError($"Failed enabling {MOD_GUID}: {e}");
      return false;
    }
  }

  public bool OnDisable()
  {
    try
    {
      harmony.UnpatchSelf();
      return true;
    }
    catch (Exception e)
    {
      Debug.LogError($"Failed to disable {MOD_GUID}: {e.Message}!");
      return false;
    }
  }
}