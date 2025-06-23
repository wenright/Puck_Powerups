

using System.Collections.Generic;

public class Powerup
{
  public string name = "unnamed";
  public static float cooldown = 0.0f;
  public float duration = 3.0f;
  public string color;

  public Powerup(string name, float duration, string color)
  {
    this.name = name;
    this.duration = duration;
    this.color = color;
  }
}

public static class PowerupNames {
  public const string Magnet = "Magnet";
  public const string Lasso = "Lasso";
  public const string Rage = "Rage";
  public const string Grapple = "Grapple";
  public const string Punch = "Punch";
  public const string Glue = "Glue";
  public const string Kick = "Kick";
}

public static class PowerupList
{
  public static Dictionary<string, Powerup> dict = new Dictionary<string, Powerup>()
  {
    [PowerupNames.Magnet] = new Powerup(PowerupNames.Magnet, 7.0f, "magenta"),
    [PowerupNames.Lasso] = new Powerup(PowerupNames.Lasso, 1.0f, "cyan"),
    [PowerupNames.Rage] = new Powerup(PowerupNames.Rage, 7.0f, "purple"),
    [PowerupNames.Grapple] = new Powerup(PowerupNames.Grapple, 7.0f, "teal"),
    [PowerupNames.Punch] = new Powerup(PowerupNames.Punch, 0.5f, "orange"),
    [PowerupNames.Glue] = new Powerup(PowerupNames.Glue, 2.5f, "yellow"),
    [PowerupNames.Kick] = new Powerup(PowerupNames.Kick, 0.5f, "lightblue"),
  };
}