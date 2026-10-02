using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs
{
    public class ItemCrystalFlask : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType= BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _owner;
        private Buff _buff;
        private float _pending;
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = unit;
            _buff = buff;
        }

        public void OnUpdate(float diff)
        {
            float seconds = Math.Min(diff / 1000, Math.Max(0, _buff.Duration - _buff.TimeElapsed + diff / 1000));
            if (_owner.IsDead)
            {
                LeagueSandbox.GameServer.API.ApiFunctionManager.RemoveBuff(_buff);
                return;
            }

            _pending += seconds;
            while (_pending + 0.00001f >= 0.5f)
            {
                _pending -= 0.5f;
                _owner.TakeHeal(_owner, 5, HealType.HealthRegeneration);
                _owner.IncreasePAR(_owner, 2.5f);
            }

            if (_buff.TimeElapsed >= _buff.Duration && _pending > 0)
            {
                _owner.TakeHeal(_owner, 10 * _pending, HealType.HealthRegeneration);
                _owner.IncreasePAR(_owner, 5 * _pending);
                _pending = 0;
            }
        }
    }
}
