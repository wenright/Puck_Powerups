using System.Collections.Generic;
using System.Linq;
using Powerups;
using Unity.Netcode;
using UnityEngine;

public class PowerupManager
{
  public Player player;
  public Powerup availablePowerup;
  public Powerup activePowerup;
  public float lastUsedAt = 0;
  public float nextPowerupAvailableAt = 0;

  public PowerupManager(Player player)
  {
    this.player = player;

    lastUsedAt = Time.time;
    nextPowerupAvailableAt = Time.time + Powerup.GetCooldown();
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
    nextPowerupAvailableAt = Time.time + Powerup.GetCooldown() + activePowerup.duration;

    // Special actions that happen when powerup is activated. Per-frame actions happen in PlayerBodyV2Patch
    switch (activePowerup.name)
    {
      case PowerupNames.Kick:
      {
        float kickPower = 18.5f;
        PlayerBody playerBody = StickCompatibility.GetPlayerBody(player);
        if (!playerBody) break;
      
        PlayerTeam enemyTeam = player.Team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
        List<Player> enemies = PlayerManager.Instance.GetPlayersByTeam(enemyTeam);
        if (enemies.Count == 0) break;

        PlayerBody enemyBody = null;
        float closestEnemyDistance = float.MaxValue;
        foreach (Player enemy in enemies)
        {
          PlayerBody candidateBody = StickCompatibility.GetPlayerBody(enemy);
          if (!candidateBody) candidateBody = enemy.GetComponentInChildren<PlayerBody>();
          if (!candidateBody) continue;

          float enemyDistance = Vector3.Distance(playerBody.transform.position, candidateBody.transform.position);
          if (enemyDistance >= closestEnemyDistance) continue;

          enemyBody = candidateBody;
          closestEnemyDistance = enemyDistance;
        }
        if (!enemyBody) break;

        enemyBody.OnSlip();
        enemyBody.Rigidbody.AddForceAtPosition((enemyBody.transform.position - playerBody.transform.position).normalized * kickPower, playerBody.Rigidbody.worldCenterOfMass + playerBody.transform.up * 0.5f, ForceMode.VelocityChange);
      
        break;
      }
      case PowerupNames.LowGrav:
        SetGravity(false);

        break;
      case PowerupNames.Backflip:
      {
        float upwardsForce = 44000;
        float flipTorque = 37500;
        PlayerBody playerBody = StickCompatibility.GetPlayerBody(player);
        if (!playerBody) break;
        
        playerBody.Rigidbody.AddForce(Vector3.up * upwardsForce);
        playerBody.Rigidbody.AddTorque(-playerBody.transform.right * flipTorque);

        nextPowerupAvailableAt = Time.time + 3.0f;
        
        break;
      }
      case PowerupNames.Slowmo:
        Time.timeScale = 0.5f;
      
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
    if (CountActivePowerupByName(PowerupNames.LowGrav) <= 1)
    {
      SetGravity(true);
    }

    if (CountActivePowerupByName(PowerupNames.Slowmo) <= 1)
    {
      Time.timeScale = 1f;
    }

    if (activePowerup.duration > 2.0f) {
      UIChatPatch.SendToPlayer($"<b><color={activePowerup.color}>{activePowerup.name}</color></b> ended", player);
    }
    activePowerup = null;
  }

  private void SetGravity(bool enabled)
  {
    Puck[] pucks = GameObject.FindObjectsByType<Puck>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    foreach (Puck puck in pucks)
    {
      puck.Rigidbody.useGravity = enabled;
    }

    PlayerBody[] players = GameObject.FindObjectsByType<PlayerBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    foreach (PlayerBody player in players)
    {
      player.Rigidbody.useGravity = enabled;
    }
  }

  private int CountActivePowerupByName(string name)
  {
    int powerupCount = 0;
    foreach (var manager in PlayerBodyV2_Patch.powerupManagers)
    {
      if (manager.Value?.activePowerup?.name == name)
      {
        powerupCount++;
      }
    }

    return powerupCount;
  }
}
