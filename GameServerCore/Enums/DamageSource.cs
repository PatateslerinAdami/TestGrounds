namespace GameServerCore.Enums
{
    /// <summary>
    /// Source types for damage. Used in determining when damage is applied, such as before mitigation.
    /// Verified against the S4 mac decomp: exact 1:1 match with `DamageSource`
    /// (AI/Damage/DamageEnums.h, values 0-11, DAMAGESOURCE_Numof = 12 - complete).
    /// Values 0-10 are also Lua-exported by Riot (S1 luaspellscripthelper.cpp), keep exact.
    /// PROC vs REACTIVE confirmed from Riot's own decompiled scripts (Averdrian): on-hit damage
    /// when YOU attack = PROC (BuffOnHitUnitBuildingBlocks), reflect damage when YOU are hit =
    /// REACTIVE (BuffOnBeingHitBuildingBlocks, e.g. Thornmail).
    /// </summary>
    public enum DamageSource
    {
        /// <summary>
        /// Unmitigated.
        /// </summary>
        DAMAGE_SOURCE_RAW,
        /// <summary>
        /// Raw damage applied internally by a spell's sub-component / applicator
        /// (e.g. Xerath Q arcanopulse ball, Galio righteous gust, AoE applicators).
        /// Grants no lifesteal/spell-vamp (not in the spell-vamp table).
        /// </summary>
        DAMAGE_SOURCE_INTERNALRAW,
        /// <summary>
        /// Buff spell dots.
        /// </summary>
        DAMAGE_SOURCE_PERIODIC,
        /// <summary>
        /// On-hit / proc damage applied when the owner ATTACKS or hits a unit
        /// (Riot BuffOnHitUnitBuildingBlocks: on-hit items like Madred's Razors / Wit's End,
        /// and ability on-hit procs e.g. Kayle E nova, Jinx Q splash). This is what our
        /// OnHitUnit handlers should use.
        /// </summary>
        DAMAGE_SOURCE_PROC,
        /// <summary>
        /// Reflect / retaliate damage dealt in reaction to BEING hit (not to attacking)
        /// (Riot BuffOnBeingHitBuildingBlocks, e.g. Thornmail reflecting a % of damage taken).
        /// Belongs in OnBeingHit handlers, NOT OnHit. Scaled by the server's ReactiveRatio.
        /// </summary>
        DAMAGE_SOURCE_REACTIVE,
        /// <summary>
        /// Engine-internal death-related damage source (spell-vamp category). NOTE: scripts do NOT
        /// tag damage with this — "deal damage when X dies" effects use a BuffOnDeath trigger block
        /// with a normally-chosen source (e.g. Malzahar voidling detonation = DAMAGE_SOURCE_RAW),
        /// not this. Exact engine usage unconfirmed (no script consumer found in available decomps).
        /// </summary>
        DAMAGE_SOURCE_ONDEATH,
        /// <summary>
        /// Single instance spell damage.
        /// </summary>
        DAMAGE_SOURCE_SPELL,
        /// <summary>
        /// The basic auto-attack's own damage. The ONLY source healed via physical LifeSteal
        /// (every other source heals via SpellVamp scaled by a per-source ratio). On-hit procs
        /// that fire from an attack are DAMAGE_SOURCE_PROC, not this.
        /// </summary>
        DAMAGE_SOURCE_ATTACK,
        /// <summary>
        /// Generic / fallback damage source — used by assorted effects incl. some basic-attack
        /// overrides (e.g. Pantheon's), summoner DoTs, and misc scripts. Grants no lifesteal/
        /// spell-vamp (not in the spell-vamp table).
        /// </summary>
        DAMAGE_SOURCE_DEFAULT,
        /// <summary>
        /// Any area based spells.
        /// </summary>
        DAMAGE_SOURCE_SPELLAOE,
        /// <summary>
        /// Passive, on update or timed repeat.
        /// </summary>
        DAMAGE_SOURCE_SPELLPERSIST,
        /// <summary>
        /// Damage dealt by a pet / summoned unit (Malzahar voidlings, Annie's Tibbers,
        /// Yorick ghouls, Heimerdinger turrets, etc.). Spell-vamped at its own ratio
        /// (sv_PetRatio in the map Constants.var, default 0).
        /// </summary>
        DAMAGE_SOURCE_PET
    }

}
