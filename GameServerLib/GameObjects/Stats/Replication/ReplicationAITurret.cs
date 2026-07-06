using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public class ReplicationAITurret : Replication
    {
        public ReplicationAITurret(BaseTurret owner) : base(owner)
        {

        }
        public override void Update()
        {
            // UpdateFloat(Stats.ManaPoints.Total, ReplicationBucket.Local1, 0); //mMaxMP
            // UpdateFloat(Stats.CurrentMana, ReplicationBucket.Local1, 1); //mMP
            UpdateUint((uint)Stats.ActionState, ReplicationBucket.Local1, 2); //ActionState
            UpdateBool(Stats.IsMagicImmune, ReplicationBucket.Local1, 3); //MagicImmune
            UpdateBool(Stats.IsInvulnerable, ReplicationBucket.Local1, 4); //IsInvulnerable
            UpdateBool(Stats.IsPhysicalImmune, ReplicationBucket.Local1, 5); //IsPhysicalImmune
            UpdateBool(Stats.IsLifestealImmune, ReplicationBucket.Local1, 6); //IsLifestealImmune
            UpdateFloat(Stats.AttackDamage.BaseValue, ReplicationBucket.Local1, 7); //mBaseAttackDamage
            UpdateFloat(Stats.Armor.Total, ReplicationBucket.Local1, 8); //mArmor
            UpdateFloat(Stats.MagicResist.Total, ReplicationBucket.Local1, 9); //mSpellBlock
            UpdateFloat(Stats.AttackSpeedMultiplier.Total, ReplicationBucket.Local1, 10); //mAttackSpeedMod
            UpdateFloat(Stats.AttackDamage.FlatBonus, ReplicationBucket.Local1, 11); //mFlatPhysicalDamageMod
            UpdateFloat(Stats.AttackDamage.PercentBonus, ReplicationBucket.Local1, 12); //mPercentPhysicalDamageMod
            UpdateFloat(Stats.AbilityPower.Total, ReplicationBucket.Local1, 13); //mFlatMagicDamageMod
            UpdateFloat(Stats.HealthRegeneration.Total, ReplicationBucket.Local1, 14); //mHPRegenRate
            UpdateFloat(Stats.CurrentHealth, ReplicationBucket.Map, 0); //mHP
            UpdateFloat(Stats.HealthPoints.Total, ReplicationBucket.Map, 1); //mMaxHP
            // UpdateFloat(Stats.PerceptionRange.FlatBonus, ReplicationBucket.Map, 2); //mFlatBubbleRadiusMod
            // UpdateFloat(Stats.PerceptionRange.PercentBonus, ReplicationBucket.Map, 3); //mPercentBubbleRadiusMod
            UpdateFloat(Stats.GetTrueMoveSpeed(), ReplicationBucket.Map, 4); //mMoveSpeed
            UpdateFloat(Stats.Size.Total, ReplicationBucket.Map, 5); //mSkinScaleCoef(mistyped as mCrit)
            UpdateBool(Stats.IsTargetable, ReplicationBucket.Global, 0); //mIsTargetable
            UpdateUint((uint)Stats.IsTargetableToTeam, ReplicationBucket.Global, 1); //mIsTargetableToTeamFlags
        }
    }
}
