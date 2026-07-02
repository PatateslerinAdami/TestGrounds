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

        /// <summary>
        /// 4.17 decomp mask <c>kTenacityReducibleCCFlags = 0x12640DA0</c> (BuffEnums.h:55) =
        /// {Stun, Silence, Taunt, Slow, Snare, Sleep, Fear, Charm, Blind, Flee}, EXTENDED here
        /// with Polymorph (bit 9) and Disarm (bit 31) per project decision (2026-07-02) to match
        /// modern/4.20 tenacity behavior (tenacity reduces all CC except airborne/knockup/knockback/
        /// suppression). Extended mask = 0x12640DA0 | (1&lt;&lt;9) | (1&lt;&lt;31) = 0x92640FA0.
        /// See docs/TENACITY_IMPLEMENTATION_PLAN.md §2c.
        /// </summary>
        private const uint TenacityReducibleMask = 0x92640FA0u;

        /// <summary>
        /// Whether a buff of this <see cref="BuffType"/> has its duration shortened by tenacity
        /// (<see cref="GameServerCore.Enums.BuffType"/> is the CC classifier). Applied at buff
        /// creation as <c>duration *= (1 - tenacity)</c>. Slow is reducible on its DURATION here;
        /// its magnitude is reduced separately by SlowResistPercent. Suppression, Knockup and
        /// Knockback are exempt.
        /// </summary>
        public static bool IsTenacityReducible(this BuffType type)
        {
            return (TenacityReducibleMask & (1u << (int)type)) != 0;
        }
    }
}
