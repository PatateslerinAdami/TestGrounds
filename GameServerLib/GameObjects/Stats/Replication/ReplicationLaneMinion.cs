using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public class ReplicationLaneMinion : Replication
    {
        public ReplicationLaneMinion(LaneMinion owner) : base(owner)
        {

        }
        public override void Update()
        {
            // See ReplicationMinion / AttackableUnit.UsesParReplication: on Twisted Treeline (Map10) lane
            // minions replicate with the PAR Map layout (MaxMP/MP prepended to Map var0/1, MoveSpeed/scale/
            // targetability shifted +2, Local1 ActionState shifted 7→5); on Summoner's Rift they are non-PAR
            // (scale at Map var3). Replay-verified.
            bool par = Owner.UsesParReplication;

            UpdateFloat(Stats.CurrentHealth, ReplicationBucket.Local1, 0); //mHP
            UpdateFloat(Stats.HealthPoints.Total, ReplicationBucket.Local1, 1); //mMaxHP
            // Local1 var2,3,4 = mLifetime/mMaxLifetime/mLifetimeTicks (not replicated)

            if (par)
            {
                UpdateFloat(Stats.ManaPoints.Total, ReplicationBucket.Map, 0); //mMaxMP
                UpdateFloat(Stats.CurrentMana, ReplicationBucket.Map, 1); //mMP

                UpdateUint((uint)Stats.ActionState, ReplicationBucket.Local1, 5); //ActionState
                UpdateBool(Stats.IsMagicImmune, ReplicationBucket.Local1, 6); //MagicImmune
                UpdateBool(Stats.IsInvulnerable, ReplicationBucket.Local1, 7); //IsInvulnerable
                UpdateBool(Stats.IsPhysicalImmune, ReplicationBucket.Local1, 8); //IsPhysicalImmune
                UpdateBool(Stats.IsLifestealImmune, ReplicationBucket.Local1, 9); //IsLifestealImmune
                UpdateFloat(Stats.AttackDamage.BaseValue, ReplicationBucket.Local1, 10); //mBaseAttackDamage
                UpdateFloat(Stats.Armor.Total, ReplicationBucket.Local1, 11); //mArmor
                UpdateFloat(Stats.MagicResist.Total, ReplicationBucket.Local1, 12); //mSpellBlock
                UpdateFloat(Stats.AttackSpeedMultiplier.Total, ReplicationBucket.Local1, 13); //mAttackSpeedMod
                UpdateFloat(Stats.AttackDamage.FlatBonus, ReplicationBucket.Local1, 14); //mFlatPhysicalDamageMod
                UpdateFloat(Stats.AttackDamage.PercentBonus, ReplicationBucket.Local1, 15); //mPercentPhysicalDamageMod
                UpdateFloat(Stats.AbilityPower.Total, ReplicationBucket.Local1, 16); //mFlatMagicDamageMod
                UpdateFloat(Stats.HealthRegeneration.Total, ReplicationBucket.Local1, 17); //mHPRegenRate
                UpdateFloat(Stats.ManaRegeneration.Total, ReplicationBucket.Local1, 18); //mPARRegenRate
                UpdateFloat(Stats.MagicResist.FlatBonus, ReplicationBucket.Local1, 19); //mFlatMagicReduction
                UpdateFloat(Stats.MagicResist.PercentBonus, ReplicationBucket.Local1, 20); //mPercentMagicReduction

                UpdateFloat(Stats.GetTrueMoveSpeed(), ReplicationBucket.Map, 4); //mMoveSpeed
                UpdateFloat(Stats.Size.Total, ReplicationBucket.Map, 5); //mSkinScaleCoef(mistyped as mCrit)
                UpdateBool(Stats.IsTargetable, ReplicationBucket.Map, 6); //mIsTargetable
                UpdateUint((uint)Stats.IsTargetableToTeam, ReplicationBucket.Map, 7); //mIsTargetableToTeamFlags
            }
            else
            {
                // Local1 var5,6 = mMaxMP/mMP (not replicated for lane minions)
                UpdateUint((uint)Stats.ActionState, ReplicationBucket.Local1, 7); //ActionState
                UpdateBool(Stats.IsMagicImmune, ReplicationBucket.Local1, 8); //MagicImmune
                UpdateBool(Stats.IsInvulnerable, ReplicationBucket.Local1, 9); //IsInvulnerable
                UpdateBool(Stats.IsPhysicalImmune, ReplicationBucket.Local1, 10); //IsPhysicalImmune
                UpdateBool(Stats.IsLifestealImmune, ReplicationBucket.Local1, 11); //IsLifestealImmune
                UpdateFloat(Stats.AttackDamage.BaseValue, ReplicationBucket.Local1, 12); //mBaseAttackDamage
                UpdateFloat(Stats.Armor.Total, ReplicationBucket.Local1, 13); //mArmor
                UpdateFloat(Stats.MagicResist.Total, ReplicationBucket.Local1, 14); //mSpellBlock
                UpdateFloat(Stats.AttackSpeedMultiplier.Total, ReplicationBucket.Local1, 15); //mAttackSpeedMod
                UpdateFloat(Stats.AttackDamage.FlatBonus, ReplicationBucket.Local1, 16); //mFlatPhysicalDamageMod
                UpdateFloat(Stats.AttackDamage.PercentBonus, ReplicationBucket.Local1, 17); //mPercentPhysicalDamageMod
                UpdateFloat(Stats.AbilityPower.Total, ReplicationBucket.Local1, 18); //mFlatMagicDamageMod
                UpdateFloat(Stats.HealthRegeneration.Total, ReplicationBucket.Local1, 19); //mHPRegenRate
                UpdateFloat(Stats.ManaRegeneration.Total, ReplicationBucket.Local1, 20); //mPARRegenRate
                UpdateFloat(Stats.MagicResist.FlatBonus, ReplicationBucket.Local1, 21); //mFlatMagicReduction
                UpdateFloat(Stats.MagicResist.PercentBonus, ReplicationBucket.Local1, 22); //mPercentMagicReduction
                UpdateFloat(Stats.GetTrueMoveSpeed(), ReplicationBucket.Map, 2); //mMoveSpeed
                UpdateFloat(Stats.Size.Total, ReplicationBucket.Map, 3); //mSkinScaleCoef(mistyped as mCrit)
                UpdateBool(Stats.IsTargetable, ReplicationBucket.Map, 4); //mIsTargetable
                UpdateUint((uint)Stats.IsTargetableToTeam, ReplicationBucket.Map, 5); //mIsTargetableToTeamFlags
            }
        }
    }
}
