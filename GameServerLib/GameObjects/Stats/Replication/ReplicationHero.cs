using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public class ReplicationHero : Replication
    {
        public ReplicationHero(Champion owner) : base(owner)
        {
        }
        public override void Update()
        {
            UpdateFloat(Stats.Gold, ReplicationBucket.ClientOnly, 0); //mGold
            // UpdateFloat(Stats.TotalGold, ReplicationBucket.ClientOnly, 1); //mGoldTotal
            UpdateUint((uint)Stats.SpellsEnabled, ReplicationBucket.ClientOnly, 2); //mReplicatedSpellCanCastBitsLower1
            UpdateUint((uint)(Stats.SpellsEnabled >> 32), ReplicationBucket.ClientOnly, 3); //mReplicatedSpellCanCastBitsUpper1
            UpdateUint((uint)Stats.SummonerSpellsEnabled, ReplicationBucket.ClientOnly, 4); //mReplicatedSpellCanCastBitsLower2
            UpdateUint((uint)(Stats.SummonerSpellsEnabled >> 32), ReplicationBucket.ClientOnly, 5); //mReplicatedSpellCanCastBitsUpper2
            UpdateUint(Stats.EvolvePoints, ReplicationBucket.ClientOnly, 6); //mEvolvePoints
            UpdateUint(Stats.EvolveFlags, ReplicationBucket.ClientOnly, 7); //mEvolveFlag
            for (var i = 0; i < 4; i++)
            {
                UpdateFloat(Stats.ManaCost[i], ReplicationBucket.ClientOnly, 8 + i); //ManaCost_{i}
            }
            for(var i = 0; i < 16; i++)
            {
                UpdateFloat(Stats.ManaCost[45 + i], ReplicationBucket.ClientOnly, 12 + i); //ManaCost_Ex{i}
            }
            UpdateUint((uint)Stats.ActionState, ReplicationBucket.Local1, 0); //ActionState
            UpdateBool(Stats.IsMagicImmune, ReplicationBucket.Local1, 1); //MagicImmune
            UpdateBool(Stats.IsInvulnerable, ReplicationBucket.Local1, 2); //IsInvulnerable
            UpdateBool(Stats.IsPhysicalImmune, ReplicationBucket.Local1, 3); //IsPhysicalImmune
            UpdateBool(Stats.IsLifestealImmune, ReplicationBucket.Local1, 4); //IsLifestealImmune
            UpdateFloat(Stats.AttackDamage.BaseValue, ReplicationBucket.Local1, 5); //mBaseAttackDamage
            UpdateFloat(Stats.AbilityPower.BaseValue, ReplicationBucket.Local1, 6); //mBaseAbilityDamage
            UpdateFloat(Stats.Dodge.Total, ReplicationBucket.Local1, 7); //mDodge
            // Crit chance is capped at 100% (replay-verified: Ashe's mCrit reads exactly 1.0 while her
            // Focus guaranteed-crit is active, never base+100%). Excess crit (e.g. the guaranteed-crit
            // buff stacked on top of item crit) is wasted, not displayed — matches Riot's ceiling.
            UpdateFloat(Math.Min(Stats.CriticalChance.Total, 1.0f), ReplicationBucket.Local1, 8); //mCrit
            UpdateFloat(Stats.Armor.Total, ReplicationBucket.Local1, 9); //mArmor
            UpdateFloat(Stats.MagicResist.Total, ReplicationBucket.Local1, 10); //mSpellBlock
            UpdateFloat(Stats.HealthRegeneration.Total, ReplicationBucket.Local1, 11); //mHPRegenRate
            UpdateFloat(Stats.ManaRegeneration.Total, ReplicationBucket.Local1, 12); //mPARRegenRate
            UpdateFloat(Stats.Range.Total, ReplicationBucket.Local1, 13); //mAttackRange
            UpdateFloat(Stats.AttackDamage.FlatBonus, ReplicationBucket.Local1, 14); //mFlatPhysicalDamageMod
            UpdateFloat(Stats.AttackDamage.PercentBonus, ReplicationBucket.Local1, 15); //mPercentPhysicalDamageMod
            UpdateFloat(Stats.AbilityPower.Total - Stats.AbilityPower.BaseValue, ReplicationBucket.Local1, 16); //mFlatMagicDamageMod
            // UpdateFloat(Stats.MagicResist.FlatBonus, ReplicationBucket.Local1, 17); //mFlatMagicReduction
            // UpdateFloat(Stats.MagicResist.PercentBonus, ReplicationBucket.Local1, 18); //mPercentMagicReduction
            UpdateFloat(Stats.AttackSpeedMultiplier.Total, ReplicationBucket.Local1, 19); //mAttackSpeedMod
            UpdateFloat(Stats.Range.FlatBonus, ReplicationBucket.Local1, 20); //mFlatCastRangeMod
            // TODO: Find out why a negative value is required for ability cooldowns to display properly.
            UpdateFloat(Stats.CooldownReduction.Total, ReplicationBucket.Local1, 21); //mPercentCooldownMod
            UpdateFloat(Stats.PassiveCooldownEndTime, ReplicationBucket.Local1, 22); //mPassiveCooldownEndTime
            UpdateFloat(Stats.PassiveCooldownTotalTime, ReplicationBucket.Local1, 23); //mPassiveCooldownTotalTime
            UpdateFloat(Stats.ArmorPenetration.FlatBonus, ReplicationBucket.Local1, 24); //mFlatArmorPenetration
            UpdateFloat(1.0f - Math.Clamp(Stats.ArmorPenetration.PercentBaseBonus, 0.0f, 1.0f), ReplicationBucket.Local1, 25); //mPercentArmorPenetration
            UpdateFloat(Stats.MagicPenetration.FlatBonus, ReplicationBucket.Local1, 26); //mFlatMagicPenetration
            UpdateFloat(1.0f - Math.Clamp(Stats.MagicPenetration.PercentBaseBonus, 0.0f, 1.0f), ReplicationBucket.Local1, 27); //mPercentMagicPenetration
            UpdateFloat(Stats.LifeSteal.Total, ReplicationBucket.Local1, 28); //mPercentLifeStealMod
            UpdateFloat(Stats.SpellVamp.Total, ReplicationBucket.Local1, 29); //mPercentSpellVampMod
            UpdateFloat(Stats.Tenacity.Total, ReplicationBucket.Local1, 30); //mPercentCCReduction
            UpdateFloat(1.0f - Math.Clamp(Stats.ArmorPenetration.PercentBonus, 0.0f, 1.0f), ReplicationBucket.Local2, 0); //mPercentBonusArmorPenetration
            UpdateFloat(1.0f - Math.Clamp(Stats.MagicPenetration.PercentBonus, 0.0f, 1.0f), ReplicationBucket.Local2, 1); //mPercentBonusMagicPenetration
            UpdateFloat(Stats.HealthRegeneration.BaseValue, ReplicationBucket.Local2, 2); //mBaseHPRegenRate
            UpdateFloat(Stats.ManaRegeneration.BaseValue, ReplicationBucket.Local2, 3); //mBasePARRegenRate
            UpdateFloat(Stats.CurrentHealth, ReplicationBucket.Map, 0); //mHP
            UpdateFloat(Stats.CurrentMana, ReplicationBucket.Map, 1); //mMP
            UpdateFloat(Stats.HealthPoints.Total, ReplicationBucket.Map, 2); //mMaxHP
            UpdateFloat(Stats.ManaPoints.Total, ReplicationBucket.Map, 3); //mMaxMP
            UpdateFloat(Stats.Experience, ReplicationBucket.Map, 4); //mExp
            // UpdateFloat(Stats.LifeTime, ReplicationBucket.Map, 5); //mLifetime
            // UpdateFloat(Stats.MaxLifeTime, ReplicationBucket.Map, 6); //mMaxLifetime
            // UpdateFloat(Stats.LifeTimeTicks, ReplicationBucket.Map, 7); //mLifetimeTicks
            // UpdateFloat(Stats.PerceptionRange.FlatMod, ReplicationBucket.Map, 8); //mFlatBubbleRadiusMod
            // UpdateFloat(Stats.PerceptionRange.PercentMod, ReplicationBucket.Map, 9); //mPercentBubbleRadiusMod
            UpdateFloat(Stats.GetTrueMoveSpeed(), ReplicationBucket.Map, 10); //mMoveSpeed
            UpdateFloat(Stats.Size.Total, ReplicationBucket.Map, 11); //mSkinScaleCoef(mistyped as mCrit)
            // UpdateFloat(Stats.FlatPathfindingRadiusMod, ReplicationBucket.Map, 12); //mPathfindingRadiusMod
            UpdateUint(Stats.Level, ReplicationBucket.Map, 13); //mLevelRef
            UpdateUint((uint)Owner.MinionCounter, ReplicationBucket.Map, 14); //mNumNeutralMinionsKilled
            UpdateBool(Stats.IsTargetable, ReplicationBucket.Map, 15); //mIsTargetable
            UpdateUint((uint)Stats.IsTargetableToTeam, ReplicationBucket.Map, 16); //mIsTargetableToTeamFlags
        }
    }
}
