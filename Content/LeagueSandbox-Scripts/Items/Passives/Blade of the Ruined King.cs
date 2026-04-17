using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.API;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using GameServerCore.Enums;

namespace ItemPassives
{
    public class ItemID_3153 : IItemScript
    {
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();
        private ObjAIBase _owner;

        public void OnActivate(ObjAIBase owner)
        {   _owner = owner;
            ApiEventManager.OnHitUnit.AddListener(this, owner, TargetExecute, false);
            owner.AddStatModifier(StatsModifier);
        }
        public void TargetExecute(DamageData data)
        {
            if (data.Target is not LaneTurret)
            {
                var TargetHealth = data.Target.Stats.CurrentHealth;
                float TargetHealthPercentDamage = 0.08f;
                float AppliedBORKDamage = TargetHealth * TargetHealthPercentDamage;

                data.Target.TakeDamage(_owner, AppliedBORKDamage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC, false);
                // TODO: cap the bonus damage to minions and monsters to 60.
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.OnHitUnit.RemoveListener(this);
        }
    }
}
