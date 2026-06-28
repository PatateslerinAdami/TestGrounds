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
                // STUN/SNARE/SILENCE/DISARM are NOT flags in Riot — they are pure capability disables
                // (see ToCapabilityDisable). Only the bits below are real Riot CharacterState states.
                BuffType.CHARM => StatusFlags.Charmed,
                BuffType.FEAR => StatusFlags.Feared,
                BuffType.FLEE => StatusFlags.Feared,
                BuffType.TAUNT => StatusFlags.Taunted,
                BuffType.SUPPRESSION => StatusFlags.Suppressed,
                BuffType.SLEEP => StatusFlags.Sleep,
                BuffType.NEAR_SIGHT => StatusFlags.NearSighted,
                _ => StatusFlags.None,
            };
        }

        /// <summary>
        /// Maps a buff's <see cref="BuffType"/> to the CAPABILITIES (CanMove/CanAttack/CanCast) it disables —
        /// the faithful 4.20 model (M2 Phase 2). Replay-verified (2026-06-27, real champion ActionState): Riot
        /// represents crowd control on the wire by CLEARING the positive CAN_MOVE/CAN_ATTACK/CAN_CAST bits
        /// (the CAN_NOT_MOVE/CAN_NOT_ATTACK bits are never set), i.e. CC is a capability disable, not a flag.
        /// Aggregated into buffDisable by AttackableUnit.RecomputeBuffEffects, so overlap is union/longest-
        /// duration safe. See docs/M2_CHARACTERSTATE_REBUILD_PLAN.md.
        ///
        /// CHARM/FEAR/TAUNT deliberately do NOT disable CanMove: the AI DRIVES their movement (flee / walk to
        /// charmer / walk to taunter), so the server CanMove() must stay true. They disable cast (+ attack for
        /// charm/fear) and carry their state bit (Charmed/Feared/Taunted) via <see cref="ToStatusFlag"/>.
        /// </summary>
        public static StatusFlags ToCapabilityDisable(this BuffType type)
        {
            const StatusFlags Move = StatusFlags.CanMove;
            const StatusFlags Attack = StatusFlags.CanAttack;
            const StatusFlags Cast = StatusFlags.CanCast;
            return type switch
            {
                BuffType.STUN => Move | Attack | Cast,
                BuffType.SLEEP => Move | Attack | Cast,
                BuffType.SUPPRESSION => Move | Attack | Cast,
                BuffType.SNARE => Move,                      // root: move only
                BuffType.SILENCE => Cast,
                BuffType.DISARM => Attack,
                BuffType.CHARM => Attack | Cast,             // AI walks the unit to the charmer
                BuffType.FEAR => Attack | Cast,              // AI flees
                BuffType.FLEE => Attack | Cast,
                BuffType.TAUNT => Cast,                      // forced to auto-attack the taunter
                _ => StatusFlags.None,
            };
        }
    }
}
