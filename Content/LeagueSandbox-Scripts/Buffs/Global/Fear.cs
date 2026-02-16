using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs
{
    internal class Fear : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.FEAR,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
        };

        public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

        public bool RandomDirection { get; set; } = false;
        public float slowPercent = 0f;
        private AttackableUnit _unit;
        private ObjAIBase _owner;
        private Particle _particle;
        private Random _rng = new Random();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _unit = unit;
            _owner = buff.SourceUnit;
            AddParticleTarget(_owner, unit, "LOC_fear", unit, buff.Duration, bone: "head");

            unit.SetStatus(StatusFlags.Feared, true);

            if (RandomDirection)
            {
                MoveRandomly();
            }
            else
            {
                Flee();
            }
            StatsModifier.MoveSpeed.PercentBonus = -slowPercent;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_particle != null) _particle.SetToRemove();

            unit.SetStatus(StatusFlags.Feared, false);
            unit.StopMovement();
        }

        public void OnUpdate(float diff)
        {
            if (_unit == null || _unit.IsDead) return;

            if (_unit is ObjAIBase ai)
            {
                if (RandomDirection)
                {
                    if (ai.IsPathEnded())
                    {
                        MoveRandomly();
                    }
                }
                else
                {
                    if (ai.IsPathEnded())
                    {
                        Flee();
                    }
                }
            }
        }

        private void MoveRandomly()
        {
            if (_unit is ObjAIBase ai)
            {
                var currentPos = _unit.Position;
                var angle = _rng.NextDouble() * Math.PI * 2;
                var distance = 400f;

                var targetX = currentPos.X + (float)(Math.Cos(angle) * distance);
                var targetY = currentPos.Y + (float)(Math.Sin(angle) * distance);
                var targetPos = new Vector2(targetX, targetY);

                var path = GetPath(currentPos, targetPos);
                ai.SetWaypoints(path);
                ai.UpdateMoveOrder(OrderType.MoveTo);
            }
        }

        private void Flee()
        {
            if (_unit is ObjAIBase ai && _owner != null)
            {
                var dir = Vector2.Normalize(_unit.Position - _owner.Position);
                if (float.IsNaN(dir.X) || float.IsNaN(dir.Y)) dir = new Vector2(1, 0); 

                var targetPos = _unit.Position + dir * 1000; 

                var path = GetPath(_unit.Position, targetPos);
                ai.SetWaypoints(path);
                ai.UpdateMoveOrder(OrderType.MoveTo);
            }
        }
    }
}