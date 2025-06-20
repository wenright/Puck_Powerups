using System.Linq;
using UnityEngine;

public class PowerupManager
{
  public Player player;
  public Powerup availablePowerup;
  public Powerup activePowerup;
  public float lastUsedAt = -Mathf.Infinity;
  public float nextPowerupAvailableAt = -Mathf.Infinity;
  public bool hasUsedAbilityOnce = false;

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

    hasUsedAbilityOnce = true;

    lastUsedAt = Time.time;

    activePowerup = availablePowerup;
    availablePowerup = null;
    nextPowerupAvailableAt = Time.time + Powerup.cooldown + activePowerup.duration;

    // Special actions that happen when powerup is activated. Per-frame actions happen in PlayerBodyV2Patch
    switch (activePowerup.name)
    {
      case PowerupNames.Lasso:
        Puck puck = PuckManager.Instance.GetPuck();
        if (puck)
          puck.Rigidbody.linearVelocity = Vector3.zero;

        break;
      case PowerupNames.Punch:
        puck = PuckManager.Instance.GetPuck();
        float punchPower = 20.0f;
        if (puck)
          puck.Rigidbody.linearVelocity = (puck.transform.position - player.PlayerBody.transform.position).normalized * punchPower;

        break;
    }

    return activePowerup;
  }

  public Powerup GenerateNextPowerup()
  {
    if (!CanUse()) return null;

    availablePowerup = PowerupList.dict.ElementAt(Random.Range(0, PowerupList.dict.Count)).Value;

    return availablePowerup;
  }

  public void End()
  {
    // Special actions that happen when powerup is ended
    // switch (activePowerup.name)
    // {

    // }

    activePowerup = null;
  }
}