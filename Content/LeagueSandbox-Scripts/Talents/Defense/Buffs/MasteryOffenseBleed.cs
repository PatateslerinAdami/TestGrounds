using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemPassives;
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
    public class MasteryOffenseBleed : IBuffGameScript
    {
        private ObjAIBase _owner;
        private AttackableUnit _unit;
        private PeriodicTicker _periodicTicker;
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.DAMAGE,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _owner = buff.SourceUnit;
            _unit = unit;
        }

        public void OnUpdate(Buff buff, float diff)
        {
            var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, false, 1, 2);
            if (ticks != 1) return;
            _unit.TakeDamage(_owner, _unit.Stats.CurrentHealth * 0.01f, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PERIODIC, DamageResultType.RESULT_NORMAL);
        }
    }
}