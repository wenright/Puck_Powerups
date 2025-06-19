using System.Linq;
using UnityEngine;

public class AbilityManager
{
  public Player player;
  public Ability activeAbility;
  public float lastUsedAt = -Mathf.Infinity;
  public float nextAbilityAvailableAt = -Mathf.Infinity;

  public AbilityManager(Player player)
  {
    this.player = player;
  }

  public bool CanUse()
  {
    return Time.time > nextAbilityAvailableAt;
  }

  public Ability UseAbility()
  {
    if (!CanUse()) return null;

    lastUsedAt = Time.time;

    activeAbility = Abilities.dict.ElementAt(Random.Range(0, Abilities.dict.Count)).Value;
    nextAbilityAvailableAt = Time.time + Ability.cooldown + activeAbility.duration;

    return activeAbility;
  }
}