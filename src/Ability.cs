

using System;
using System.Collections.Generic;

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
  public const string Lasso = "Lasso";
  public const string Rage = "Rage";
}

public static class Abilities
{
  public static Dictionary<string, Ability> dict = new Dictionary<string, Ability>()
  {
    [AbilityNames.Magnet] = new Ability(AbilityNames.Magnet, 6.0f, "magenta"),
    [AbilityNames.Lasso] = new Ability(AbilityNames.Lasso, 1.0f, "cyan"),
    [AbilityNames.Lasso] = new Ability(AbilityNames.Rage, 7.0f, "purple"),
  };
}