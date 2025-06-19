using System.Linq;
using UnityEngine;

public class AbilityManager
{
  public Player player;
  public Ability availableAbility;
  public Ability activeAbility;
  public float lastUsedAt = -Mathf.Infinity;
  public float nextAbilityAvailableAt = -999;

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

    activeAbility = availableAbility;
    availableAbility = null;
    nextAbilityAvailableAt = Time.time + Ability.cooldown + activeAbility.duration;
    Debug.Log($"Setting next available to {nextAbilityAvailableAt}");

    return activeAbility;
  }

  public Ability GenerateNextAbility()
  {
    if (!CanUse()) return null;

    availableAbility = Abilities.dict.ElementAt(Random.Range(0, Abilities.dict.Count)).Value;

    return availableAbility;
  }
}