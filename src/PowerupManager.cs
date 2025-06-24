using System.Collections.Generic;
using System.Linq;
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
    nextPowerupAvailableAt = Time.time + Powerup.cooldown;
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

    // Special actions that happen when powerup is activated. Per-frame actions happen in PlayerBodyV2Patch
    switch (activePowerup.name)
    {
      case PowerupNames.Lasso:
        Puck puck = PuckManager.Instance.GetPuck();
        if (puck)
          puck.Rigidbody.linearVelocity /= 2;

        break;
      case PowerupNames.Punch:
        float punchPower = 27.0f;
        
        puck = PuckManager.Instance.GetPuck();
        if (puck)
          puck.Rigidbody.linearVelocity = (puck.transform.position - player.PlayerBody.transform.position).normalized * punchPower;

        break;
      case PowerupNames.Kick:
        float kickPower = 5.0f;
      
        PlayerTeam enemyTeam = player.Team.Value == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
        List<Player> enemies = PlayerManager.Instance.GetPlayersByTeam(enemyTeam);
        if (enemies.Count == 0) break;

        enemies.Sort((e1, e2) => Mathf.RoundToInt((Vector3.Distance(player.PlayerBody.transform.position, e1.PlayerBody.transform.position) - Vector3.Distance(player.PlayerBody.transform.position, e2.PlayerBody.transform.position)) * 100));
        Player enemy = enemies[0];
        PlayerBodyV2 enemyBody = enemy.GetComponentInChildren<PlayerBodyV2>();
        if (enemyBody == null) break;

        enemyBody.OnSlip();
        enemyBody.Rigidbody.AddForceAtPosition((enemyBody.transform.position - player.PlayerBody.transform.position).normalized * kickPower, player.PlayerBody.Rigidbody.worldCenterOfMass + player.PlayerBody.transform.up * 0.5f, ForceMode.VelocityChange);
      
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
    if (activePowerup.duration > 2.0f) {
      UIChat.Instance.Server_ChatMessageRpc($"<b><color={activePowerup.color}>{activePowerup.name}</color></b> ended", UIChat.Instance.RpcTarget.Group(new[] { player.OwnerClientId }, RpcTargetUse.Temp));
    }
    activePowerup = null;
  }
}