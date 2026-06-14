
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
        public StatModifier Range { get; } = new StatModifier();
        public StatModifier Size { get; } = new StatModifier();
        public StatModifier SpellVamp { get; } = new StatModifier();
        public StatModifier Tenacity { get; } = new StatModifier();
        public float MultiplicativeSpeedBonus { get; set; }
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
