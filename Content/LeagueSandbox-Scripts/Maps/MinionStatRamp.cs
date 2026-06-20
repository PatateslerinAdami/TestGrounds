using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace MapScripts
{
    /// <summary>
    /// Per-minion-type lane-minion stat ramp — a faithful port of the 4.20 LevelScript
    /// UpgradeMinionTimer accumulator (fires every UPGRADE_MINION_TIMER = 90s). Each map populates
    /// its own instances from that map's Lua values, then ticks them; <see cref="ToStatsModifier"/>
    /// yields the additive bonus to apply to a minion spawning at that point in the game.
    ///
    /// Both per-map variants are data-driven here:
    ///  - Map1 ramps Armor/MagicResist (ArmorUpgrade/MagicResistUpgrade != 0) and keeps HpUpgrade
    ///    constant (HpUpgradeGrowth = 0) -> linear HP.
    ///  - Map11 (real 4.20 SR) does NOT ramp Armor/MagicResist (set those upgrades to 0) but grows
    ///    HpUpgrade by HpUpgradeGrowth each tick -> accelerating HP.
    /// GoldGiven/ExpGiven (base + ramp) are applied as the minion's death reward at spawn — gold ramps
    /// (+GoldUpgrade/tick, capped at GoldMaximumBonus); Exp is static (ExpUpgrade = 0 on every type).
    /// </summary>
    public class MinionStatRamp
    {
        // --- static config (from the map's Lua minion-info table) ---
        public float HpUpgrade;
        public float HpUpgradeGrowth;
        public float DamageUpgrade;
        public float ArmorUpgrade;
        public float MagicResistUpgrade;
        public float GoldUpgrade;
        public float ExpUpgrade;
        public float GoldMaximumBonus = float.MaxValue;
        public float GoldGivenBase;
        public float ExpGivenBase;

        // --- running accumulators (advanced by Tick) ---
        public float HpBonus;
        public float DamageBonus;
        public float ArmorBonus;
        public float MagicResistBonus;
        public float GoldBonus;
        public float ExpBonus;

        /// <summary>One 90s upgrade step. Mirrors LevelScript UpgradeMinionTimer (HPBonus += HPUpgrade;
        /// HPUpgrade += HPUpgradeGrowth; Gold capped at GoldMaximumBonus).</summary>
        public void Tick()
        {
            HpBonus += HpUpgrade;
            DamageBonus += DamageUpgrade;
            GoldBonus += GoldUpgrade;
            if (GoldBonus > GoldMaximumBonus)
            {
                GoldBonus = GoldMaximumBonus;
            }
            ArmorBonus += ArmorUpgrade;
            MagicResistBonus += MagicResistUpgrade;
            ExpBonus += ExpUpgrade;
            HpUpgrade += HpUpgradeGrowth;
        }

        public float GoldGiven => GoldBonus + GoldGivenBase;
        public float ExpGiven => ExpBonus + ExpGivenBase;

        /// <summary>The current accumulated bonus as an additive stat modifier (HP/AD/Armor/MR).
        /// Transmitted HealthBonus/DamageBonus are derived from the HP/AD flat bonuses by the spawn path.</summary>
        public StatsModifier ToStatsModifier()
        {
            var mod = new StatsModifier();
            mod.HealthPoints.FlatBonus = HpBonus;
            mod.AttackDamage.FlatBonus = DamageBonus;
            mod.Armor.FlatBonus = ArmorBonus;
            mod.MagicResist.FlatBonus = MagicResistBonus;
            return mod;
        }
    }
}
