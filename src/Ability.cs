

using System;
using System.Collections.Generic;
using UnityEngine;

public class Ability
{
  public string name = "unnamed";
  public static float cooldown = 10.0f;
  public float duration = 3.0f;
  public string color;

  public Ability(string name, float duration, string color)
  {
    this.name = name;
    this.duration = duration;
    this.color = color;
  }
}

public static class AbilityNames {
  public const string Magnet = "Magnet";
}

public static class Abilities
{
  public static Dictionary<string, Ability> dict = new Dictionary<string, Ability>()
  {
    [AbilityNames.Magnet] = new Ability(AbilityNames.Magnet, 3.0f, "magenta")
  };
}