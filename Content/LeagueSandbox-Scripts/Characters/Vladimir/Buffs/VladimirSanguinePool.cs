using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;


namespace Buffs
{
    internal class VladimirSanguinePool : IBuffGameScript {
        private ObjAIBase _vladimir;
        private PeriodicTicker _periodicTicker;
        private const float MaxTickAmount = 4;
        private const float MaxMovespeedAmount = 37.5f;
        private int _tickAmount = 4;
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType    = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks   = 1
        };
        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _vladimir = buff.SourceUnit;
            unit.SetStatus(StatusFlags.Targetable, false);
            HideHealthBar(unit, -1, true);
            StatsModifier.MoveSpeed.PercentBonus += MaxMovespeedAmount;
            _vladimir.AddStatModifier(StatsModifier);
        }

        public void OnUpdate(Buff buff, float diff)
        {
            var tick = _periodicTicker.ConsumeTicks(diff, 1000f, false, 1, (int)MaxTickAmount);
            if (tick != 1) return;
            _tickAmount--;
            _vladimir.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.PercentBonus = MaxMovespeedAmount/MaxTickAmount * _tickAmount;
            _vladimir.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.SetStatus(StatusFlags.Targetable, true);
            HideHealthBar(unit, -1, false);
        }
    }
}