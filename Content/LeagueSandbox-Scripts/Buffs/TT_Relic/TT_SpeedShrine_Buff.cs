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
    // The Ghost Relic's move-speed boost: +20% on pickup, decaying linearly to 0 over the buff's
    // duration (wiki: "20% bonus movement speed that decays over 5 seconds"). Relic-only — the actual
    // Speed Shrine uses OdinSpeedShrineAura, so the decay here doesn't affect that.
    internal class TT_SpeedShrine_Buff : IBuffGameScript
    {
        private const float MS_BONUS = 0.2f;

        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.HASTE,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private AttackableUnit _unit;
        private Buff _buff;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _buff = buff;
            StatsModifier.MoveSpeed.PercentBonus += MS_BONUS;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnUpdate(float diff)
        {
            if (_unit == null || _buff == null || _buff.Duration <= 0f)
            {
                return;
            }

            // Linear decay over the buff duration. Positive move-speed bonuses sum into a running total
            // (no in-place update like slows), so re-apply: remove the current bonus, lower it, add it back.
            float fraction = Math.Max(0f, 1f - _buff.TimeElapsed / _buff.Duration);
            _unit.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.PercentBonus = MS_BONUS * fraction;
            _unit.AddStatModifier(StatsModifier);
        }
    }
}

