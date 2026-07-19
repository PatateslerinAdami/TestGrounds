using System;
using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public class Stats
    {
        public ulong SpellsEnabled { get; private set; }
        public ulong SummonerSpellsEnabled { get; private set; }

        public ActionState ActionState { get; private set; }
        public PrimaryAbilityResourceType ParType { get; private set; }

        public bool IsMagicImmune { get; set; }
        public bool IsInvulnerable { get; set; }
        public bool IsPhysicalImmune { get; set; }
        public bool IsLifestealImmune { get; set; }
        public bool IsTargetable { get; set; }
        public SpellDataFlags IsTargetableToTeam { get; set; }

        public float AttackSpeedFlat { get; set; }
        public float HealthPerLevel { get; set; }
        public float ManaPerLevel { get; set; }
        public float ArmorPerLevel { get; set; }
        public float MagicResistPerLevel { get; set; }
        public float HealthRegenerationPerLevel { get; set; }
        public float ManaRegenerationPerLevel { get; set; }
        public float GrowthAttackSpeed { get; set; }
        // Per-slot current mana cost (decomp Spellbook.h mCurManaCost[63]); maintained on level-up
        // (Spell.LevelUp/SetLevel) and replicated owner-only (ReplicationHero).
        public float[] ManaCost { get; }
        // Per-slot mana-cost increments = Riot's SpellDataInst::SetIncManaCost /
        // SetIncMultiplicativeManaCost (BBSetPARCostInc), set via ApiFunctionManager.SetSpellPARCost.
        // Effective cost = (SpellData.ManaCost[level] + ManaCostInc[slot]) * (1 + ManaCostMult[slot]),
        // clamped >= 0. Kept as a separate layer so a spell level-up (which rewrites ManaCost[slot]
        // from SpellData) does not clobber the increment. See Spell.GetManaCost().
        public float[] ManaCostInc { get; }
        public float[] ManaCostMult { get; }

        public Stat AbilityPower { get; }
        public Stat Armor { get; }
        public Stat ArmorPenetration { get; }
        public Stat AttackDamage { get; }
        public Stat AttackDamagePerLevel { get; set; }
        public Stat AttackSpeedMultiplier { get; set; }
        public Stat CooldownReduction { get; }
        public Stat CriticalChance { get; }
        public Stat CriticalDamage { get; }
        public Stat DeathTimerReduction { get; }
        public Stat ExpGivenOnDeath { get; }
        /// <summary>
        /// Percent bonus experience this unit gains (Riot CharInter.mPercentEXPBonus, surfaced by
        /// AIHero::GetPercentEXPBonus / the StatsExperience interface — hero-only consumer). Fed by
        /// item/rune stat "PercentEXPBonus"; consumed in Champion.AddExperience with the
        /// gcd_PercentEXPBonusMinimum/Maximum clamp.
        /// </summary>
        public Stat PercentEXPBonus { get; }
        public Stat GoldPerGoldTick { get; }
        public Stat GoldGivenOnDeath { get; }
        public Stat HealthPoints { get; }
        public Stat HealthRegeneration { get; }
        /// <summary>
        /// Char-data BaseFactorHPRegen: fraction of CURRENT max HP regenerated per second, on top of
        /// the static regen. Zero for champions and lane minions; 0.0015 on pets/monsters/props
        /// (Tibbers, jungle camps, super minions, Anivia egg, ...).
        /// </summary>
        public float HealthRegenerationFactor { get; internal set; }
        /// <summary>
        /// Effective HP regen per second: static/modifier regen plus the max-HP-scaling factor part.
        /// Computed on read because the factor tracks the buffed max HP (wire-verified on Tibbers:
        /// mHPRegenRate goes 2.3 -> 5.0 when R3 raises max HP 1200 -> 3000). Use this — not
        /// <see cref="HealthRegeneration"/>.Total — for regen ticks and mHPRegenRate replication.
        /// </summary>
        public float TotalHealthRegen => HealthRegeneration.Total + HealthRegenerationFactor * HealthPoints.Total;
        public Stat LifeSteal { get; }
        public Stat MagicResist { get; }
        public Stat MagicPenetration { get; }
        public Stat ManaPoints { get; }
        public Stat ManaRegeneration { get; }
        /// <summary>
        /// Chance [0..1] that this unit's auto attacks miss (rolled per attack in
        /// <see cref="ObjAIBase.RollAutoAttackMiss"/>). Raised to 1.0 by Blind.
        /// Mirrors Riot's miss-chance stat (script API IncFlatMissChanceMod/GetMissChance).
        /// </summary>
        public Stat MissChance { get; }
        /// <summary>
        /// Chance [0..1] that this unit DODGES an incoming auto attack (rolled per attack against the
        /// attacker in <see cref="ObjAIBase.RollDodge"/>). Target-side counterpart of <see cref="MissChance"/>.
        /// Mirrors Riot's mDodge stat (script API IncFlatDodgeMod). Removed as a general stat after S1;
        /// in 4.20 only set to 1.0 by abilities like Jax E (Counter Strike / JaxEvasion buff). Defaults to 0.
        /// </summary>
        public Stat Dodge { get; }
        public Stat MoveSpeed { get; }
        public Stat Range { get; }
        public Stat Size { get; }
        public Stat SpellVamp { get; }
        public Stat AcquisitionRange { get; set; }

        // Tenacity (CC-duration reduction), bucketed per the 4.17 decomp (CharacterIntermediate.h:19-25,
        // ItemData.cpp:538-543). Each active StatsModifier contributes to at most one bucket; within the
        // max buckets only the strongest contribution counts (unique passives don't stack), so removal
        // must recompute from the remaining contributions — hence per-modifier dictionaries (same pattern
        // as _slows). Rune is a running additive sum. Combined multiplicatively by PercentCCReduction.
        private readonly Dictionary<StatsModifier, float> _tenacityItem = new();
        private readonly Dictionary<StatsModifier, float> _tenacityCharacter = new();
        private readonly Dictionary<StatsModifier, float> _tenacityMastery = new();
        private readonly Dictionary<StatsModifier, float> _tenacityCleanse = new();
        private float _tenacityRune;

        // Slow-resist (slow MAGNITUDE reduction) — same max-across-sources rule as the tenacity item
        // bucket (decomp ItemData.cpp:543 std::max). Separate axis from tenacity's duration cut.
        private readonly Dictionary<StatsModifier, float> _slowResist = new();

        /// <summary>
        /// Upper clamp on total tenacity. The client asserts CC reduction never reaches 100%
        /// (HeroHealthBar.cpp:719), so we keep it just under 1.0.
        /// </summary>
        private const float MaxTenacity = 0.999f;

        /// <summary>
        /// Final aggregated CC-duration reduction [0..1), replicated to the client as
        /// <c>mPercentCCReduction</c>. Buckets combine multiplicatively:
        /// <c>1 - (1-rune)(1-mastery)(1-item)(1-character)(1-cleanse)</c>, each max-bucket taking its
        /// strongest contribution. Consumed at buff creation to shorten reducible CC durations.
        /// </summary>
        public float PercentCCReduction
        {
            get
            {
                float item = MaxOrZero(_tenacityItem);
                float character = MaxOrZero(_tenacityCharacter);
                float mastery = MaxOrZero(_tenacityMastery);
                float cleanse = MaxOrZero(_tenacityCleanse);
                float reduction = 1f - (1f - _tenacityRune) * (1f - mastery) * (1f - item) * (1f - character) * (1f - cleanse);
                return Math.Clamp(reduction, 0f, MaxTenacity);
            }
        }

        private static float MaxOrZero(Dictionary<StatsModifier, float> bucket)
        {
            float max = 0f;
            foreach (var v in bucket.Values)
            {
                if (v > max)
                {
                    max = v;
                }
            }
            return max;
        }

        public float Gold { get; set; }
        public byte Level { get; set; }
        public float Experience { get; set; }
        public float Points { get; set; }
        public uint EvolvePoints { get; set; }
        public uint EvolveFlags { get; set; }
        /// <summary>
        /// Slow-resist [0..1): reduces slow MAGNITUDE (applied in CalculateTrueMoveSpeed). Combines by
        /// MAX across sources (decomp ItemData.cpp:543), so multiple slow-resist items (e.g. Boots of
        /// Swiftness + a boot enchant) grant only the highest, not the sum. Separate axis from tenacity,
        /// which cuts slow DURATION.
        /// </summary>
        public float SlowResistPercent => MaxOrZero(_slowResist);
        /// <summary>
        /// Epic-monster slow rejection (Baron/Dragon, Monster_Epic tag). Slows are a MoveSpeed stat-mod,
        /// not a status flag, so they bypass the BuffType CC-flag suppression in AttackableUnit
        /// .RecomputeBuffEffects — this flag rejects their movespeed effect in CalculateTrueMoveSpeed.
        /// The slow buff object is still added (BuffAdd2 sent, replay-faithful); only its speed effect is
        /// nullified, matching Riot's "buff added, internal CC rejected" model. See
        /// reference_epic_monster_cc_immunity.
        /// </summary>
        public bool ImmuneToSlow { get; set; }
        public float MultiplicativeSpeedBonus { get; set; }
        // Slow registry: every active slow keyed by its StatsModifier instance (reference
        // identity this is robust against equal percentages from different buffs and against the
        // per-tick remove/mutate/re-add pattern decaying slows use). Key = named effect
        // (OriginSpell name via StatsModifier.SourceBuff, "item" for item passives, null =
        // standalone modifier counting as its own effect). CalculateTrueMoveSpeed regroups
        // on every recalc, so weaker same-effect instances stay dormant and take over when
        // the stronger one expires, and decaying slows can change rank order mid-duration.
        private readonly Dictionary<StatsModifier, (string EffectKey, float Percent)> _slows
            = new Dictionary<StatsModifier, (string, float)>();
        private readonly List<float> _armorPenPercentMultipliers = new List<float>();
        private readonly List<float> _armorPenBonusPercentMultipliers = new List<float>();
        private readonly List<float> _magicPenPercentMultipliers = new List<float>();
        private readonly List<float> _magicPenBonusPercentMultipliers = new List<float>();
        private const float PENETRATION_COMPARE_EPSILON = 0.0001f;
        private float _currentHealth;
        private float _trueMoveSpeed;
        public float CurrentHealth
        {
            get => Math.Min(HealthPoints.Total, _currentHealth);
            set => _currentHealth = value;
        }

        private float _currentMana;
        public float CurrentMana
        {
            get => Math.Min(ManaPoints.Total, _currentMana);
            set => _currentMana = value;
        }

        public bool IsGeneratingGold { get; set; } // Used to determine if the Stats update should include generating gold. Changed in Champion.h
        public float SpellCostReduction { get; set; } //URF Buff/Lissandra's passive
        public float PassiveCooldownEndTime { get; set; }
        public float PassiveCooldownTotalTime { get; set; }

        public Stats()
        {
            Level = 1;
            SpellCostReduction = 0;
            ManaCost = new float[64];
            ManaCostInc = new float[64];
            ManaCostMult = new float[64];
            ActionState = ActionState.CAN_ATTACK | ActionState.CAN_CAST | ActionState.CAN_MOVE | ActionState.TARGETABLE;
            IsTargetable = true;
            IsTargetableToTeam = SpellDataFlags.TargetableToAll;

            AbilityPower = new Stat();
            Armor = new Stat();
            ArmorPenetration = new Stat();
            AttackDamage = new Stat();
            AttackDamagePerLevel = new Stat();
            AttackSpeedMultiplier = new Stat(1.0f, 0, 0, 0, 0);
            CooldownReduction = new Stat();
            CriticalChance = new Stat();
            CriticalDamage = new Stat();
            DeathTimerReduction = new Stat();
            ExpGivenOnDeath = new Stat();
            PercentEXPBonus = new Stat();
            GoldPerGoldTick = new Stat();
            GoldGivenOnDeath = new Stat();
            HealthPoints = new Stat();
            HealthRegeneration = new Stat();
            LifeSteal = new Stat();
            MagicResist = new Stat();
            MagicPenetration = new Stat();
            ManaPoints = new Stat();
            ManaRegeneration = new Stat();
            MissChance = new Stat();
            Dodge = new Stat();
            MoveSpeed = new Stat();
            Range = new Stat();
            Size = new Stat(1.0f, 0, 0, 0, 0);
            SpellVamp = new Stat();
            AcquisitionRange = new Stat();
        }

        public void LoadStats(CharData charData)
        {
            // Epic monsters reject the slow EFFECT (tag-derived, same source as AttackableUnit
            // .IsCrowdControlImmune); the slow buff still applies, only its movespeed reduction is nullified.
            ImmuneToSlow = charData.UnitTags.HasTag(UnitTag.Monster_Epic);
            AcquisitionRange.BaseValue = charData.AcquisitionRange;
            AttackDamagePerLevel.BaseValue = charData.DamagePerLevel;
            Armor.BaseValue = charData.Armor;
            ArmorPerLevel = charData.ArmorPerLevel;
            AttackDamage.BaseValue = charData.BaseDamage;
            // AttackSpeedFlat = GlobalAttackSpeed / CharAttackDelay
            AttackSpeedFlat = 1.0f / GlobalData.GlobalCharacterDataConstants.AttackDelay / (1.0f + charData.AttackDelayOffsetPercent);
            CriticalDamage.BaseValue = charData.CritDamageBonus;
            ExpGivenOnDeath.BaseValue = charData.ExpGivenOnDeath;
            GoldGivenOnDeath.BaseValue = charData.GoldGivenOnDeath;
            GrowthAttackSpeed = charData.AttackSpeedPerLevel;
            HealthPerLevel = charData.HpPerLevel;
            HealthPoints.BaseValue = charData.BaseHp;
            HealthRegeneration.BaseValue = charData.BaseStaticHpRegen;
            HealthRegenerationFactor = charData.BaseFactorHpRegen;
            HealthRegenerationPerLevel = charData.HpRegenPerLevel;
            MagicResist.BaseValue = charData.SpellBlock;
            MagicResistPerLevel = charData.SpellBlockPerLevel;
            ManaPerLevel = charData.MpPerLevel;
            ManaPoints.BaseValue = charData.BaseMp;
            ManaRegeneration.BaseValue = charData.BaseStaticMpRegen;
            ManaRegenerationPerLevel = charData.MpRegenPerLevel;
            MoveSpeed.BaseValue = charData.MoveSpeed;
            ParType = charData.ParType;
            Range.BaseValue = charData.AttackRange;
            CalculateTrueMoveSpeed();
        }

        public void AddModifier(StatsModifier modifier)
        {
            AbilityPower.ApplyStatModifier(modifier.AbilityPower);
            Armor.ApplyStatModifier(modifier.Armor);
            AddPenetrationModifier(ArmorPenetration, modifier.ArmorPenetration, _armorPenPercentMultipliers, _armorPenBonusPercentMultipliers);
            AttackDamage.ApplyStatModifier(modifier.AttackDamage);
            AttackDamagePerLevel.ApplyStatModifier(modifier.AttackDamagePerLevel);
            AttackSpeedMultiplier.ApplyStatModifier(modifier.AttackSpeed);
            CooldownReduction.ApplyStatModifier(modifier.CooldownReduction);
            CriticalChance.ApplyStatModifier(modifier.CriticalChance);
            CriticalDamage.ApplyStatModifier(modifier.CriticalDamage);
            DeathTimerReduction.ApplyStatModifier(modifier.DeathTimerReduction);
            ExpGivenOnDeath.ApplyStatModifier(modifier.ExpGivenOnDeath);
            PercentEXPBonus.ApplyStatModifier(modifier.PercentEXPBonus);
            GoldGivenOnDeath.ApplyStatModifier(modifier.GoldGivenOnDeath);
            GoldPerGoldTick.ApplyStatModifier(modifier.GoldPerSecond);
            HealthPoints.ApplyStatModifier(modifier.HealthPoints);
            HealthRegeneration.ApplyStatModifier(modifier.HealthRegeneration);
            LifeSteal.ApplyStatModifier(modifier.LifeSteal);
            MagicResist.ApplyStatModifier(modifier.MagicResist);
            AddPenetrationModifier(MagicPenetration, modifier.MagicPenetration, _magicPenPercentMultipliers, _magicPenBonusPercentMultipliers);
            ManaPoints.ApplyStatModifier(modifier.ManaPoints);
            ManaRegeneration.ApplyStatModifier(modifier.ManaRegeneration);
            MissChance.ApplyStatModifier(modifier.MissChance);
            Dodge.ApplyStatModifier(modifier.Dodge);

            if (modifier.MoveSpeed.PercentBonus < 0)
            {
                // Dictionary assignment (not Add): re-adding the same modifier with a new
                // percent (decaying slows) just updates the entry in place.
                _slows[modifier] = (GetSlowEffectKey(modifier), modifier.MoveSpeed.PercentBonus);
            }
            else
            {
                MoveSpeed.ApplyStatModifier(modifier.MoveSpeed);
            }
            CalculateTrueMoveSpeed();

            Range.ApplyStatModifier(modifier.Range);
            Size.ApplyStatModifier(modifier.Size);
            SpellVamp.ApplyStatModifier(modifier.SpellVamp);
            ApplyTenacityBuckets(modifier);
            MultiplicativeSpeedBonus += modifier.MultiplicativeSpeedBonus;
        }

        public void RemoveModifier(StatsModifier modifier)
        {
            AbilityPower.RemoveStatModifier(modifier.AbilityPower);
            Armor.RemoveStatModifier(modifier.Armor);
            RemovePenetrationModifier(ArmorPenetration, modifier.ArmorPenetration, _armorPenPercentMultipliers, _armorPenBonusPercentMultipliers);
            AttackDamage.RemoveStatModifier(modifier.AttackDamage);
            AttackSpeedMultiplier.RemoveStatModifier(modifier.AttackSpeed);
            CooldownReduction.RemoveStatModifier(modifier.CooldownReduction);
            CriticalChance.RemoveStatModifier(modifier.CriticalChance);
            CriticalDamage.RemoveStatModifier(modifier.CriticalDamage);
            DeathTimerReduction.RemoveStatModifier(modifier.DeathTimerReduction);
            ExpGivenOnDeath.RemoveStatModifier(modifier.ExpGivenOnDeath);
            PercentEXPBonus.RemoveStatModifier(modifier.PercentEXPBonus);
            GoldGivenOnDeath.RemoveStatModifier(modifier.GoldGivenOnDeath);
            GoldPerGoldTick.RemoveStatModifier(modifier.GoldPerSecond);
            HealthPoints.RemoveStatModifier(modifier.HealthPoints);
            HealthRegeneration.RemoveStatModifier(modifier.HealthRegeneration);
            LifeSteal.RemoveStatModifier(modifier.LifeSteal);
            MagicResist.RemoveStatModifier(modifier.MagicResist);
            RemovePenetrationModifier(MagicPenetration, modifier.MagicPenetration, _magicPenPercentMultipliers, _magicPenBonusPercentMultipliers);
            ManaPoints.RemoveStatModifier(modifier.ManaPoints);
            ManaRegeneration.RemoveStatModifier(modifier.ManaRegeneration);
            MissChance.RemoveStatModifier(modifier.MissChance);
            Dodge.RemoveStatModifier(modifier.Dodge);

            if (modifier.MoveSpeed.PercentBonus < 0)
            {
                _slows.Remove(modifier);
            }
            else
            {
                MoveSpeed.RemoveStatModifier(modifier.MoveSpeed);
            }
            CalculateTrueMoveSpeed();

            Range.RemoveStatModifier(modifier.Range);
            Size.RemoveStatModifier(modifier.Size);
            SpellVamp.RemoveStatModifier(modifier.SpellVamp);
            RemoveTenacityBuckets(modifier);
            MultiplicativeSpeedBonus -= modifier.MultiplicativeSpeedBonus;
        }

        private void ApplyTenacityBuckets(StatsModifier modifier)
        {
            // Dictionary assignment (not Add) so re-applying the same modifier with a new value
            // (e.g. Irelia's enemy-count-scaled passive) updates its contribution in place.
            if (modifier.TenacityItem != 0f) _tenacityItem[modifier] = modifier.TenacityItem;
            if (modifier.TenacityCharacter != 0f) _tenacityCharacter[modifier] = modifier.TenacityCharacter;
            if (modifier.TenacityMastery != 0f) _tenacityMastery[modifier] = modifier.TenacityMastery;
            if (modifier.TenacityCleanse != 0f) _tenacityCleanse[modifier] = modifier.TenacityCleanse;
            _tenacityRune += modifier.TenacityRune;
            if (modifier.SlowResistPercent != 0f) _slowResist[modifier] = modifier.SlowResistPercent;
        }

        private void RemoveTenacityBuckets(StatsModifier modifier)
        {
            _tenacityItem.Remove(modifier);
            _tenacityCharacter.Remove(modifier);
            _tenacityMastery.Remove(modifier);
            _tenacityCleanse.Remove(modifier);
            _tenacityRune -= modifier.TenacityRune;
            _slowResist.Remove(modifier);
        }

        public float GetTotalAttackSpeed()
        {
            // Floor the attack-speed multiplier at 1 + gcd_PercentAttackSpeedModMinimum. Constants.var
            // (Map1:25): "The lowest Attack Speed Percent Mod penalty can go" = -0.95, i.e. the multiplier
            // bottoms out at 0.05 (a slow can remove at most 95% attack speed). Clamp semantics are taken
            // from the Riot Constants.var comment — the 4.17 decomp's application site was not recovered.
            var minMultiplier = 1.0f + GlobalData.GlobalCharacterDataConstants.PercentAttackSpeedModMinimum;
            return AttackSpeedFlat * Math.Max(AttackSpeedMultiplier.Total, minMultiplier);
        }

        /// <summary>
        /// Cooldown-reduction mod floored at gcd_PercentCooldownModMinimum (Constants.var: "The lowest
        /// Cooldown Percent Mod bonus can go" — -0.4 on SR, overridden to -0.8 in URF). CooldownReduction
        /// is stored negative-for-reduction, so this floor caps how much CDR can shorten a cooldown.
        /// Comment-sourced from Constants.var; the 4.17 decomp clamp site was not recovered.
        /// </summary>
        public float GetClampedCooldownReduction()
        {
            return Math.Max(CooldownReduction.Total, GlobalData.GlobalCharacterDataConstants.PercentCooldownModMinimum);
        }

        public float GetRespawnTimer(float baseTimer)
        {
            // Floor the respawn-time mod at gcd_PercentRespawnTimeModMinimum (-0.95) before applying it,
            // then keep the existing 1000ms resulting-time floor. Comment-sourced from Constants.var.
            var mod = Math.Max(DeathTimerReduction.Total, GlobalData.GlobalCharacterDataConstants.PercentRespawnTimeModMinimum);
            return Math.Max(1000.0f, baseTimer * (1 + mod));
        }

        public float GetTrueMoveSpeed()
        {
            return _trueMoveSpeed;
        }

        /// <summary>
        /// Whether any slow effect is currently registered (the run-animation watcher uses
        /// presence rather than net speed: a slowed-but-booted unit still LOOKS slowed).
        /// </summary>
        public bool HasActiveSlows => _slows.Count > 0;

        /// <summary>
        /// Derives the named-effect key for the slow registry from the modifier's owning buff:
        /// the originating spell name, or the shared "item" bucket for buffs without an origin
        /// spell (item passives like Rylai. Riot's ItemSlow.lua treats all item slows as one
        /// strongest-only group). Null when the modifier was applied outside the buff pipeline;
        /// such entries count as their own effect.
        /// </summary>
        private static string GetSlowEffectKey(StatsModifier modifier)
        {
            var buff = modifier.SourceBuff;
            if (buff == null)
            {
                return null;
            }
            return buff.OriginSpell?.SpellName ?? "item";
        }

        public void ClearSlows()
        {
            _slows.Clear();
            CalculateTrueMoveSpeed();
        }

        /// <summary>
        /// Length of one regen window in ms. The regen tick in AttackableUnit.Update accumulates
        /// frame time and calls <see cref="Update"/> once per elapsed window; Update applies exactly
        /// one window's worth of HP/mana regeneration (single shared constant so the loop cadence
        /// and the regen math can't drift apart).
        /// </summary>
        public const float RegenTickMs = 500f;

        public void Update(AttackableUnit? owner)
        {
            const float seconds = RegenTickMs * 0.001f;

            if (owner != null && TotalHealthRegen > 0 && CurrentHealth < HealthPoints.Total && CurrentHealth > 0)
                owner.TakeHeal(owner, TotalHealthRegen * seconds, HealType.HealthRegeneration);

            if ((byte)ParType > 1) return;

            if (ManaRegeneration.Total > 0 && CurrentMana < ManaPoints.Total)
            {
                var regenAmount = ManaRegeneration.Total * seconds;
                if (owner != null)
                {
                    owner.IncreasePAR(owner, regenAmount);
                }
                else
                {
                    CurrentMana = Math.Min(ManaPoints.Total, CurrentMana + regenAmount);
                }
            }
        }

        public void LevelUp()
        {
            Level++;
            StatsModifier statsLevelUp = new StatsModifier();
            statsLevelUp.HealthPoints.BaseValue = GetLevelUpStatValue(HealthPerLevel);
            statsLevelUp.ManaPoints.BaseValue = GetLevelUpStatValue(ManaPerLevel);
            statsLevelUp.AttackDamage.BaseValue = GetLevelUpStatValue(AttackDamagePerLevel.BaseValue);
            statsLevelUp.AttackDamage.FlatBonus = GetLevelUpStatValue(AttackDamagePerLevel.FlatBonus);
            statsLevelUp.Armor.BaseValue = GetLevelUpStatValue(ArmorPerLevel);
            statsLevelUp.MagicResist.BaseValue = GetLevelUpStatValue(MagicResistPerLevel);
            statsLevelUp.HealthRegeneration.BaseValue = GetLevelUpStatValue(HealthRegenerationPerLevel);
            statsLevelUp.ManaRegeneration.BaseValue = GetLevelUpStatValue(ManaRegenerationPerLevel);
            statsLevelUp.AttackSpeed.PercentBaseBonus = GetLevelUpStatValue(GrowthAttackSpeed / 100.0f);
            AddModifier(statsLevelUp);

            CurrentHealth += statsLevelUp.HealthPoints.BaseValue;
            CurrentMana += statsLevelUp.ManaPoints.BaseValue;
        }

        public float GetLevelUpStatValue(float value)
        {
            return value * (0.65f + 0.035f * Level);
        }

        public bool GetSpellEnabled(byte id)
        {
            return (SpellsEnabled & 1u << id) != 0;
        }

        public void SetSpellEnabled(byte id, bool enabled)
        {
            if (enabled)
            {
                SpellsEnabled |= 1u << id;
            }
            else
            {
                SpellsEnabled &= ~(1u << id);
            }
        }

        public bool GetSummonerSpellEnabled(byte id)
        {
            return (SummonerSpellsEnabled & 16u << id) != 0;
        }

        public void SetSummonerSpellEnabled(byte id, bool enabled)
        {
            if (enabled)
            {
                SummonerSpellsEnabled |= 16u << id;
            }
            else
            {
                SummonerSpellsEnabled &= ~(16u << id);
            }
        }

        public bool GetActionState(ActionState state)
        {
            return ActionState.HasFlag(state);
        }

        private static float ClampPenetrationPercent(float value)
        {
            return Math.Clamp(value, 0.0f, 1.0f);
        }

        private static bool IsEffectivelyZero(float value)
        {
            return Math.Abs(value) <= PENETRATION_COMPARE_EPSILON;
        }

        private static float ToPenetrationMultiplier(float penetrationPercent)
        {
            return 1.0f - ClampPenetrationPercent(penetrationPercent);
        }

        private static float GetCombinedMultiplier(List<float> multipliers)
        {
            var combinedMultiplier = 1.0f;
            foreach (var multiplier in multipliers)
            {
                combinedMultiplier *= multiplier;
            }

            return combinedMultiplier;
        }

        private static float GetCombinedPenetrationPercent(List<float> multipliers)
        {
            return 1.0f - GetCombinedMultiplier(multipliers);
        }

        private static void RemovePenetrationMultiplier(List<float> multipliers, float multiplier)
        {
            var index = multipliers.FindIndex(existing => Math.Abs(existing - multiplier) <= PENETRATION_COMPARE_EPSILON);
            if (index >= 0)
            {
                multipliers.RemoveAt(index);
            }
        }

        private static void UpdateCombinedPenetrationPercents(
            Stat penetrationStat,
            List<float> totalPercentMultipliers,
            List<float> bonusPercentMultipliers
        )
        {
            penetrationStat.PercentBaseBonus = GetCombinedPenetrationPercent(totalPercentMultipliers);
            penetrationStat.PercentBonus = GetCombinedPenetrationPercent(bonusPercentMultipliers);
        }

        private static void AddPenetrationModifier(
            Stat penetrationStat,
            StatModifier modifier,
            List<float> totalPercentMultipliers,
            List<float> bonusPercentMultipliers
        )
        {
            if (!modifier.StatModified)
            {
                return;
            }

            penetrationStat.BaseValue += modifier.BaseValue;
            penetrationStat.BaseBonus += modifier.BaseBonus;
            penetrationStat.FlatBonus += modifier.FlatBonus;

            if (!IsEffectivelyZero(modifier.PercentBaseBonus))
            {
                totalPercentMultipliers.Add(ToPenetrationMultiplier(modifier.PercentBaseBonus));
            }

            if (!IsEffectivelyZero(modifier.PercentBonus))
            {
                bonusPercentMultipliers.Add(ToPenetrationMultiplier(modifier.PercentBonus));
            }

            UpdateCombinedPenetrationPercents(penetrationStat, totalPercentMultipliers, bonusPercentMultipliers);
        }

        private static void RemovePenetrationModifier(
            Stat penetrationStat,
            StatModifier modifier,
            List<float> totalPercentMultipliers,
            List<float> bonusPercentMultipliers
        )
        {
            if (!modifier.StatModified)
            {
                return;
            }

            penetrationStat.BaseValue -= modifier.BaseValue;
            penetrationStat.BaseBonus -= modifier.BaseBonus;
            penetrationStat.FlatBonus -= modifier.FlatBonus;

            if (!IsEffectivelyZero(modifier.PercentBaseBonus))
            {
                RemovePenetrationMultiplier(totalPercentMultipliers, ToPenetrationMultiplier(modifier.PercentBaseBonus));
            }

            if (!IsEffectivelyZero(modifier.PercentBonus))
            {
                RemovePenetrationMultiplier(bonusPercentMultipliers, ToPenetrationMultiplier(modifier.PercentBonus));
            }

            UpdateCombinedPenetrationPercents(penetrationStat, totalPercentMultipliers, bonusPercentMultipliers);
        }

        private float GetBaseArmorPortion()
        {
            return (Armor.BaseValue + Armor.BaseBonus) * (1 + Armor.PercentBaseBonus) * (1 + Armor.PercentBonus);
        }

        private float GetEffectiveArmorAfterPenetration(AttackableUnit attacker)
        {
            var armor = Armor.Total;
            if (armor <= 0.0f)
            {
                return armor;
            }

            var attackerArmorPen = attacker.Stats.ArmorPenetration;
            var percentArmorMultiplier = 1.0f - ClampPenetrationPercent(attackerArmorPen.PercentBaseBonus);
            var percentBonusArmorMultiplier = 1.0f - ClampPenetrationPercent(attackerArmorPen.PercentBonus);
            var flatArmorPenetration = Math.Max(0.0f, attackerArmorPen.FlatBonus);

            var baseArmor = GetBaseArmorPortion();
            var bonusArmor = Math.Max(0.0f, armor - baseArmor);
            return (baseArmor + bonusArmor * percentBonusArmorMultiplier) * percentArmorMultiplier - flatArmorPenetration;
        }

        private float GetEffectiveMagicResistAfterPenetration(AttackableUnit attacker)
        {
            var magicResist = MagicResist.Total;
            if (magicResist <= 0.0f)
            {
                return magicResist;
            }

            var attackerMagicPen = attacker.Stats.MagicPenetration;
            var percentMagicMultiplier = 1.0f - ClampPenetrationPercent(attackerMagicPen.PercentBaseBonus);
            var percentBonusMagicMultiplier = 1.0f - ClampPenetrationPercent(attackerMagicPen.PercentBonus);
            var flatMagicPenetration = Math.Max(0.0f, attackerMagicPen.FlatBonus);

            return magicResist * percentMagicMultiplier * percentBonusMagicMultiplier - flatMagicPenetration;
        }

        // internal (not private) so the test assembly can pin the Riot armor/MR mitigation curve directly.
        internal static float GetMitigationMultiplier(float resistance)
        {
            if (resistance >= 0.0f)
            {
                return 100.0f / (100.0f + resistance);
            }

            return 2.0f - 100.0f / (100.0f - resistance);
        }

        public float GetPostMitigationDamage(float damage, DamageType type, AttackableUnit attacker)
        {
            if (damage <= 0f)
            {
                return 0.0f;
            }

            float stat;
            switch (type)
            {
                case DamageType.DAMAGE_TYPE_PHYSICAL:
                    stat = GetEffectiveArmorAfterPenetration(attacker);
                    break;
                case DamageType.DAMAGE_TYPE_MAGICAL:
                    stat = GetEffectiveMagicResistAfterPenetration(attacker);
                    break;
                case DamageType.DAMAGE_TYPE_TRUE:
                    return damage;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            return damage * GetMitigationMultiplier(stat);
        }

        public void SetActionState(ActionState state, bool enabled)
        {
            if (enabled)
            {
                ActionState |= state;
            }
            else
            {
                ActionState &= ~state;
            }
        }

        public void CalculateTrueMoveSpeed()
        {
            // Riot order of operations: (Base + Flat) * (1 + sum of additive %)
            // * multiplicative % * slows so the soft caps are applied to the END value,
            // NOT to Base+Flat (we previously capped before the multipliers, which made
            // every %-speedup overshoot Riot's effective speeds and skipped the <220
            // slow-softening bracket entirely).
            float speed = MoveSpeed.BaseValue + MoveSpeed.FlatBonus;

            speed = speed * (1 + MoveSpeed.PercentBonus) * (1 + MultiplicativeSpeedBonus);

            if (_slows.Count > 0 && !ImmuneToSlow)
            {
                // Stage 1: named-effect dedup: the same effect never stacks with itself,
                // not even from different casters (wiki: Exhaust re-apply = duration reset
                // only; Riot's ItemSlow.lua funnels ALL item slows through one strongest-only
                // chain, hence the shared "item" bucket). Only the strongest instance per
                // effect counts; weaker ones stay dormant and take over when it expires.
                var effectiveSlows = new List<float>();
                Dictionary<string, float> strongestPerEffect = null;
                foreach (var (effectKey, percent) in _slows.Values)
                {
                    if (effectKey == null)
                    {
                        // Standalone modifier (no buff context): its own effect.
                        effectiveSlows.Add(percent);
                        continue;
                    }
                    strongestPerEffect ??= new Dictionary<string, float>();
                    if (!strongestPerEffect.TryGetValue(effectKey, out var current) || percent < current)
                    {
                        strongestPerEffect[effectKey] = percent;
                    }
                }
                if (strongestPerEffect != null)
                {
                    effectiveSlows.AddRange(strongestPerEffect.Values);
                }

                // Stage 2: pre-V5.13 cross-effect stacking (our 4.x era): the strongest slow
                // applies fully, every additional slow only at 35% effectiveness, combined
                // multiplicatively. Slow values are negative, so ascending sort puts the
                // strongest first. SlowResist dampens every individual slow.
                effectiveSlows.Sort();
                var slowResistFactor = 1 - SlowResistPercent;
                for (var i = 0; i < effectiveSlows.Count; i++)
                {
                    var effectiveness = i == 0 ? 1f : 0.35f;
                    speed *= 1 + effectiveSlows[i] * effectiveness * slowResistFactor;
                }
            }

            // Soft caps on the final raw value: high speeds are compressed, low speeds
            // cushioned (the <220 bracket is what softens heavy slows; <0 floors near 110).
            if (speed > 490.0f)
            {
                speed = speed * 0.5f + 230.0f;
            }
            else if (speed >= 415.0f)
            {
                speed = speed * 0.8f + 83.0f;
            }
            else if (speed < 0.0f)
            {
                speed = 110.0f + speed * 0.01f;
            }
            else if (speed < 220.0f)
            {
                speed = speed * 0.5f + 110.0f;
            }

            _trueMoveSpeed = speed;
        }
    }
}
