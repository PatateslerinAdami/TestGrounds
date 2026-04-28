using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class SionEKnockback : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.STUN,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        private List<uint> _hitUnits = new List<uint>();
        private AttackableUnit _unit;
        private Spell _ownerSpell;
        private Particle p;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _ownerSpell = ownerSpell;

            _hitUnits.Add(unit.NetId);
            p = AddParticle(ownerSpell.CastInfo.Owner, unit, "sion_base_e_minion", default, lifetime: 5f, size: 2f);
        }

        public void OnUpdate(float diff)
        {
            if (_unit == null || _ownerSpell == null) return;

            var nearbyUnits = EnumerateValidUnitsInRange(_ownerSpell.CastInfo.Owner, _unit.Position, 150f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                SpellDataFlags.AffectNeutral);
            foreach (var target in nearbyUnits)
            {
                if (_hitUnits.Contains(target.NetId)) continue;
                _hitUnits.Add(target.NetId);

                AddParticleTarget(_ownerSpell.CastInfo.Owner, target, "sion_base_e_buf_champ.troy", target,
                    lifetime: 4f);

                // TODO: Apply the pass-through damage and armor shred/slow buff here
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            CancelDash(unit);
        }
    }
}