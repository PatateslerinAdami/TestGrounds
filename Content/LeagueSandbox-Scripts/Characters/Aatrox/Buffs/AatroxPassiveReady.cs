using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class AatroxPassiveReady : IBuffGameScript
    {
        private ObjAIBase _owner;
        private Spell _spell;
        private Buff _buff;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = (ObjAIBase)unit;
            _spell = spell;
            _buff = buff;
            SetPARState(_owner, 0);
            ApiEventManager.OnPreDeathDamage.AddListener(this, _owner, Intercept);
        }

        private void Intercept(DamageData data)
        {
            if (_buff.Remove)
            {
                return;
            }

            if (_owner.IsDead)
            {
                return;
            }

            if (data.PostMitigationDamage <= 0)
            {
                return;
            }

            if (data.PostMitigationDamage < _owner.Stats.CurrentHealth)
            {
                return;
            }

            _buff.DeactivateBuff();
            data.PostMitigationDamage = 0;
            AddBuff("AatroxPassiveDeath", 3.25f, 1, _spell, _owner, _owner);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
