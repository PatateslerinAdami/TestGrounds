using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class EzrealRisingSpellForce : IBuffGameScript
    {
        private Buff _buff;
        private readonly List<Particle> _particles = new List<Particle>();

        public BuffScriptMetaData BuffMetaData { get; set; } = new()
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.STACKS_AND_RENEWS,
            MaxStacks = 5
        };

        public StatsModifier StatsModifier { get; } = new();
        public StatsModifier StatsModifier2 { get; } = new();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_buff == null)
            {
                _buff = buff;
                ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
            }

            unit.RemoveStatModifier(StatsModifier2);
            StatsModifier2.AttackSpeed.PercentBonus = 0.1f * buff.StackCount;
            unit.AddStatModifier(StatsModifier2);

            buff.SetToolTipVar(0, buff.StackCount * 10f);

            while (_particles.Count < buff.StackCount)
            {
                var particle = AddParticleTarget(unit,unit,$"Ezreal_glow{_particles.Count + 1}",unit,lifetime: 2500000f,bone: "L_hand",flags: FXFlags.BindDirection);
                _particles.Add(particle);
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            foreach (var particle in _particles)
            {
                RemoveParticle(particle);
            }
            _particles.Clear();
            unit.RemoveStatModifier(StatsModifier2);
        }

        private void OnDeath(DeathData data)
        {
            _buff.DeactivateBuff();
        }
    }
}