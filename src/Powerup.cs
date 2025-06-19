

using System.Collections.Generic;

public class Powerup
{
  public string name = "unnamed";
  public static float cooldown = 10.0f;
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
}

public static class PowerupList
{
  public static Dictionary<string, Powerup> dict = new Dictionary<string, Powerup>()
  {
    [PowerupNames.Magnet] = new Powerup(PowerupNames.Magnet, 6.0f, "magenta"),
    [PowerupNames.Lasso] = new Powerup(PowerupNames.Lasso, 1.0f, "cyan"),
    [PowerupNames.Lasso] = new Powerup(PowerupNames.Rage, 7.0f, "purple"),
  };
}