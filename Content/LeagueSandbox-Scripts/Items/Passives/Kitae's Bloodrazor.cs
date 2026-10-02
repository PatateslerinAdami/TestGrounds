using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace ItemPassives
{
    public class ItemID_3186 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            ApiEventManager.OnHitUnit.AddListener(this, owner, OnHit);
        }

        private void OnHit(DamageData data)
        {
            if (!ItemEffectHelpers.IsSuccessfulBasicAttack(_owner, data) || data.Target is not ObjAIBase
                || data.Target is BaseTurret
                || data.Target is Minion { IsWard: true })
            {
                return;
            }

            data.Target.TakeDamage(
                _owner,
                data.Target.Stats.HealthPoints.Total * 0.025f,
                DamageType.DAMAGE_TYPE_MAGICAL,
                DamageSource.DAMAGE_SOURCE_PROC,
                false
            );
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
