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

namespace Buffs
{
    public class ElixirOfWrath : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private Buff _buff;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = buff.SourceUnit;
            _buff = buff;
            StatsModifier.AttackDamage.FlatBonus = 25;
            unit.AddStatModifier(StatsModifier);
            ApiEventManager.OnDealDamage.AddListener(this, _owner, Damage);
            ApiEventManager.OnKill.AddListener(this, _owner, Kill);
            ApiEventManager.OnAssist.AddListener(this, _owner, (owner, data) => Kill(data));
        }

        private void Damage(DamageData data)
        {
            if (data.Target is Champion && data.Target.Team != _owner.Team
                && data.DamageType == DamageType.DAMAGE_TYPE_PHYSICAL
                && data.PostMitigationDamage > 0)
            {
                _owner.TakeHeal(_owner, data.PostMitigationDamage * 0.1f, HealType.PhysicalVamp);
            }
        }

        private void Kill(DeathData data)
        {
            if (data.Unit is Champion)
            {
                _buff.ExtendDuration(30);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
