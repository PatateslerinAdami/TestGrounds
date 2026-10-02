using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    public class ElixirOfIron : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
        {
            BuffType = BuffType.COMBAT_ENCHANCER,
            BuffAddType = BuffAddType.REPLACE_EXISTING
        };
        public StatsModifier StatsModifier { get; } = new StatsModifier();

        private ObjAIBase _owner;
        private float _time;
        private Vector2 _last;
        private readonly List<(Vector2 Position, float Expires, Particle Effect)> _trail = new List<(Vector2, float, Particle)>();
        public void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            _owner = buff.SourceUnit;
            _last = unit.Position;
            StatsModifier.Size.PercentBonus = 0.25f;
            StatsModifier.SlowResistPercent = 0.25f;
            StatsModifier.Tenacity.FlatBonus = 0.25f;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnUpdate(float diff)
        {
            _time += diff;
            foreach (var point in _trail)
            {
                if (point.Expires <= _time)
                {
                    RemoveParticle(point.Effect);
                }
            }

            _trail.RemoveAll(point => point.Expires <= _time);
            if (!_owner.IsDead)
            {
                if (Vector2.DistanceSquared(_last, _owner.Position) >= 50 * 50)
                {
                    _last = _owner.Position;
                    var particle = AddParticlePos(
                        _owner,
                        "global_item_elixirofiron_trail.troy",
                        _last,
                        _last,
                        4f,
                        ignoreCasterVisibility: true
                    );
                    _trail.Add((_last, _time + 4000, particle));
                }
            }

            foreach (var point in _trail)
            {
                foreach (var ally in EnumerateUnitsInRange(point.Position, 100, true))
                {
                    if (ally is Champion && ally != _owner && ally.Team == _owner.Team)
                    {
                        AddBuff("ElixirIronPath", 0.3f, 1, null, ally, _owner);
                    }
                }
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            foreach (var point in _trail)
            {
                RemoveParticle(point.Effect);
            }

            _trail.Clear();
        }
    }

    public class ElixirIronPath : ItemDistortionHaste
    {
        public ElixirIronPath()
        {
            BuffMetaData.BuffAddType = BuffAddType.RENEW_EXISTING;
        }

        public override void OnActivate(AttackableUnit unit, Buff buff, Spell spell)
        {
            buff.Variables.Set("speed", 0.15f);
            base.OnActivate(unit, buff, spell);
        }
    }
}
