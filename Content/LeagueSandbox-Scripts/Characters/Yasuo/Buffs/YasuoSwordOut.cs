using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class YasuoSwordOut : IBuffGameScript
    {
        internal const float IdleDelay = 3f;
        internal const float TransitionTime = 0.25f;

        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData()
        {
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private AttackableUnit _unit;
        private Buff _buff;
        private bool _sheathing;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _buff = buff;
            Reset();
        }

        internal void Reset()
        {
            _sheathing = false;
            StopAnimation(_unit, "Sheath");
            StopAnimation(_unit, "sheath_run_haste");
            _unit.SetAnimStates(new Dictionary<string, string> { { "IDLE1", "IDLE_OUT" }, { "RUN1_RAW", "RUN_OUT" } }, _buff);
        }

        public void OnUpdate(float diff)
        {
            if (_unit.IsDead || _unit.IsToRemove())
            {
                RemoveBuff(_buff);
                return;
            }

            if (_sheathing || _buff.TimeElapsed < IdleDelay)
            {
                return;
            }

            _sheathing = true;
            _unit.SetAnimStates(new Dictionary<string, string> { { "IDLE1", "" } }, _buff);
            PlayAnimation(_unit, "Sheath", scaleTime: 0, flags: (AnimationFlags)226);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            unit.RemoveAnimStates(buff);
            if (_sheathing && (buff.TimeElapsed < buff.Duration || unit.IsDead || unit.IsToRemove()))
            {
                StopAnimation(unit, "Sheath");
                StopAnimation(unit, "sheath_run_haste");
            }
        }
    }
}
