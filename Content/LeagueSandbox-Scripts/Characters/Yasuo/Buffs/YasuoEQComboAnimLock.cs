using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoEQComboAnimLock : IBuffGameScript
    {
        internal const float Duration = 0.5f;

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private Buff _buff = null!;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _buff = buff;
            buff.SetStatusEffect(StatusFlags.CanAttack, false);
            ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
        }

        private void OnDeath(DeathData data)
        {
            RemoveBuff(_buff);
        }

        public void OnUpdate(float diff)
        {
            if (_buff.TargetUnit.IsDead || _buff.TargetUnit.IsToRemove())
            {
                RemoveBuff(_buff);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
