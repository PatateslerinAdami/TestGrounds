using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Augments
{
    public class SmokeBombAugment : Augment
    {
        public SmokeBombAugment() : base(
            "Augment_SmokeBomb",
            "Smoke Bomb",
            "Dealing Ability Damage applies NearSight to the enemy for 5 seconds.",
            "")
        {
        }

        public override void Apply(Champion target)
        {
            ApiEventManager.OnDealDamage.AddListener(this, target, OnDealDamage, false);
        }

        private void OnDealDamage(DamageData data)
        {
            if (data.Target == null || data.Target.IsDead) return;

            bool isAbilityDamage =
                data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELL ||
                data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELLAOE ||
                data.DamageSource == DamageSource.DAMAGE_SOURCE_SPELLPERSIST;

            if (isAbilityDamage)
            {
                ApiFunctionManager.AddBuff("NearSight", 5f, 1, null, data.Target, (ObjAIBase)data.Attacker);
            }
        }
    }
}