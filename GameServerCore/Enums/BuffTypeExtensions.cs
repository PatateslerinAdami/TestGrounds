namespace GameServerCore.Enums
{
    public static class BuffTypeExtensions
    {
        /// <summary>
        /// Maps a buff's <see cref="BuffType"/> to the crowd-control <see cref="StatusFlags"/> it
        /// imposes, so CC states are DERIVED from the active buffs' types (re-aggregated each tick by
        /// AttackableUnit.RecomputeBuffEffects) instead of being set imperatively per buff script.
        /// This makes CC-state overlap correct — the flag stays set while ANY buff of that type is
        /// active and clears only when the last (longest) one expires (mirrors Riot, which derives
        /// CharacterState from active buffs' BuffType). Single-flag projection of Riot's
        /// kCrowdControlBuffFlags (docs/riot-bufftype-cc-taxonomy.md).
        ///
        /// Returns <see cref="StatusFlags.None"/> for types with no corresponding status flag:
        ///  - SLOW            -> MoveSpeed stat debuff (not a flag)
        ///  - KNOCKUP/KNOCKBACK -> forced movement (the dash / ForceMovement system, not a flag)
        ///  - POLYMORPH/BLIND -> no corresponding StatusFlags bit exists (known gap)
        ///  - all non-CC types (AURA, DAMAGE, HEAL, ...) -> None
        /// </summary>
        public static StatusFlags ToStatusFlag(this BuffType type)
        {
            return type switch
            {
                BuffType.STUN => StatusFlags.Stunned,
                BuffType.SNARE => StatusFlags.Rooted,
                BuffType.CHARM => StatusFlags.Charmed,
                BuffType.FEAR => StatusFlags.Feared,
                BuffType.FLEE => StatusFlags.Feared,
                BuffType.TAUNT => StatusFlags.Taunted,
                BuffType.SILENCE => StatusFlags.Silenced,
                BuffType.SUPPRESSION => StatusFlags.Suppressed,
                BuffType.SLEEP => StatusFlags.Sleep,
                BuffType.DISARM => StatusFlags.Disarmed,
                BuffType.NEAR_SIGHT => StatusFlags.NearSighted,
                _ => StatusFlags.None,
            };
        }
    }
}
