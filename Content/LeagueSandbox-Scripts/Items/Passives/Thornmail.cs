using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public class ItemID_3075 : IItemScript
    {
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(ObjAIBase owner)
        {
            ApiEventManager.OnTakeDamage.AddListener(this, owner, OnTakeDamage, false);
            owner.AddStatModifier(StatsModifier);
        }

        public void OnTakeDamage(DamageData damageData)
        {
            var ReturnedIncomingDamage = damageData.Damage * 0.3f;
            var Attacker = damageData.Attacker;
            var ThornmailOwner = damageData.Target;

            if (damageData.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK)
            {
                Attacker.TakeDamage(ThornmailOwner, ReturnedIncomingDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false);
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.OnTakeDamage.RemoveListener(this, owner);
        }
    }
}

