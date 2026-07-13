
namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public class StatsModifier
    {
        // Stats
        public StatModifier AbilityPower { get; } = new StatModifier();
        public StatModifier AcquisitionRange { get; } = new StatModifier();
        public StatModifier Armor { get; } = new StatModifier();
        public StatModifier ArmorPenetration { get; } = new StatModifier();
        public StatModifier AttackDamage { get; } = new StatModifier();
        public StatModifier AttackDamagePerLevel { get; } = new StatModifier();
        public StatModifier AttackSpeed { get; } = new StatModifier();
        public StatModifier CooldownReduction { get; } = new StatModifier();
        public StatModifier CriticalChance { get; } = new StatModifier();
        public StatModifier CriticalDamage { get; } = new StatModifier();
        public StatModifier DeathTimerReduction { get; } = new StatModifier();
        public StatModifier ExpGivenOnDeath { get; } = new StatModifier();
        public StatModifier GoldGivenOnDeath { get; } = new StatModifier();
        public StatModifier GoldPerSecond { get; } = new StatModifier();
        public StatModifier HealthPoints { get; } = new StatModifier();
        public StatModifier HealthRegeneration { get; } = new StatModifier();
        public StatModifier LifeSteal { get; } = new StatModifier();
        public StatModifier MagicPenetration { get; } = new StatModifier();
        public StatModifier MagicResist { get; } = new StatModifier();
        public StatModifier ManaPoints { get; } = new StatModifier();
        public StatModifier ManaRegeneration { get; } = new StatModifier();
        public StatModifier MissChance { get; } = new StatModifier();
        public StatModifier Dodge { get; } = new StatModifier();
        public StatModifier MoveSpeed { get; } = new StatModifier();
        /// <summary>
        /// Percent bonus experience (Riot ItemData mPercentEXPBonus, key "PercentEXPBonus"; the only
        /// 4.20 content carrying it is rune 5368, Greater Quintessence of Experience, +0.02). Applied
        /// additively into the unit's EXP-bonus pool (Riot ItemData.cpp:582 CharInter.mPercentEXPBonus
        /// += mPercentEXPBonus). Use FlatBonus.
        /// </summary>
        public StatModifier PercentEXPBonus { get; } = new StatModifier();
        public StatModifier Range { get; } = new StatModifier();
        public StatModifier Size { get; } = new StatModifier();
        public StatModifier SpellVamp { get; } = new StatModifier();

        // Tenacity (CC-duration reduction), bucketed per the 4.17 decomp (ItemData.cpp:538-543).
        // A single source contributes to exactly ONE bucket. Item/Mastery/Cleanse/Character combine
        // by MAX within their bucket (identical unique passives don't stack — highest wins); Rune is
        // additive. Buckets then combine multiplicatively into Stats.PercentCCReduction. Static
        // equipped-item tenacity feeds TenacityItem (from ItemData JSON); buff/champ tenacity (Irelia,
        // URF, Elixir) feeds TenacityCharacter. See docs/TENACITY_IMPLEMENTATION_PLAN.md.
        public float TenacityItem { get; set; }
        public float TenacityCharacter { get; set; }
        public float TenacityRune { get; set; }
        public float TenacityMastery { get; set; }
        public float TenacityCleanse { get; set; }

        public float MultiplicativeSpeedBonus { get; set; }
        // Slow-resist reduces slow MAGNITUDE (strength), applied in Stats.CalculateTrueMoveSpeed.
        // Separate axis from tenacity, which reduces slow DURATION.
        public float SlowResistPercent { get; set; }

        /// <summary>
        /// The buff this modifier belongs to, set automatically by Buff.ActivateBuff before
        /// the buff script runs. Used by the slow registry in Stats to derive the named-effect
        /// key (OriginSpell name, or the shared "item" bucket) the same named effect never
        /// stacks with itself, different effects stack at 35% (pre-V5.13 rules).
        /// Null for modifiers applied outside the buff pipeline; those count as their own effect.
        /// </summary>
        public Buff SourceBuff { get; set; }
    }
}
