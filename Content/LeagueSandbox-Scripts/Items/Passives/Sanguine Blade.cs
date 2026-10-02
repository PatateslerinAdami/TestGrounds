using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemHelpers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3181 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            ApiEventManager.OnHitUnit.AddListener(this, owner, OnHit);
            ApiEventManager.OnDeath.AddListener(this, owner, OnDeath);
        }

        private void OnHit(DamageData data)
        {
            if (ItemEffectHelpers.IsSuccessfulBasicAttack(_owner, data) && data.Target is ObjAIBase
                && data.Target is not BaseTurret
                && data.Target is not Minion { IsWard: true })
            {
                AddBuff("OdinBloodbursterBuff", 8.0f, 1, null, _owner, _owner);
            }
        }

        private void OnDeath(DeathData data)
        {
            RemoveBuff(_owner, "OdinBloodbursterBuff");
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveBuff(owner, "OdinBloodbursterBuff");
        }
    }
}
