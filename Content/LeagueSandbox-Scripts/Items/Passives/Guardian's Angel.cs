using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives
{
    public class ItemID_3026 : IItemScript
    {
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        public void OnActivate(ObjAIBase owner)
        {
            _owner = owner;
            ApiEventManager.OnPreDeathDamage.AddListener(this, owner, Hit);
            OnUpdate(0);
        }

        private void Hit(DamageData data)
        {
            if (_owner.IsDead || !_owner.CanTakeDamage(data.DamageType) || _owner.HasBuff("HasBeenRevived")
                || _owner.Stats.CurrentHealth > data.PostMitigationDamage)
            {
                return;
            }

            data.PostMitigationDamage = 0;
            AddBuff("HasBeenRevived", 300, 1, null, _owner, _owner);
            AddBuff("GuardianAngel", 4, 1, null, _owner, _owner);
            RemoveBuff(_owner, "WillRevive");
        }

        public void OnUpdate(float diff)
        {
            if (!_owner.HasBuff("HasBeenRevived") && !_owner.HasBuff("WillRevive"))
            {
                AddBuff("WillRevive", 0, 1, null, _owner, _owner, true);
            }
        }

        public void OnDeactivate(ObjAIBase owner)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
            RemoveBuff(owner, "WillRevive");
        }
    }
}
