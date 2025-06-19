using System.Linq;
using UnityEngine;

public class PowerupManager
{
  public Player player;
  public Powerup availablePowerup;
  public Powerup activePowerup;
  public float lastUsedAt = -Mathf.Infinity;
  public float nextPowerupAvailableAt = -Mathf.Infinity;

  public PowerupManager(Player player)
  {
    this.player = player;
  }

  public bool CanUse()
  {
    return Time.time > nextPowerupAvailableAt;
  }

  public Powerup UsePowerup()
  {
    if (!CanUse()) return null;

    lastUsedAt = Time.time;

    activePowerup = availablePowerup;
    availablePowerup = null;
    nextPowerupAvailableAt = Time.time + Powerup.cooldown + activePowerup.duration;

    return activePowerup;
  }

  public Powerup GenerateNextPowerup()
  {
    if (!CanUse()) return null;

    availablePowerup = PowerupList.dict.ElementAt(Random.Range(0, PowerupList.dict.Count)).Value;

    return availablePowerup;
  }
}