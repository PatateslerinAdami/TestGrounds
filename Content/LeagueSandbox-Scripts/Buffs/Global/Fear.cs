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

            // Feared is DERIVED from BuffType.FEAR (AttackableUnit.RecomputeBuffEffects) — overlap-safe.

            // Record the CC source + flavour on the unit so an AI-driven crowd-control component can
            // drive the movement (Riot's model: buff = flag, AI = movement). RandomDirection = wander
            // (AI_FEARED) vs flee straight away from the source.
            if (unit is ObjAIBase cc)
            {
                cc.CrowdControlSource = _owner;
                cc.CrowdControlWander = RandomDirection;

                // Migration bridge: only fall back to buff-driven movement for units WITHOUT an AI
                // crowd-control driver (e.g. a player champion still on EmptyAIScript). Units with
                // the CrowdControlComponent (minions today) get their movement from the AI layer.
                if (!cc.AICrowdControlActive)
                {
                    if (RandomDirection)
                    {
                        MoveRandomly();
                    }
                    else
                    {
                        Flee();
                    }
                }
            }

            ApplyAssistMarker(unit, _owner, 10.0f);
            StatsModifier.MoveSpeed.PercentBonus = -slowPercent;
            unit.AddStatModifier(StatsModifier);
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            if (_particle != null) _particle.SetToRemove();

            if (unit is ObjAIBase cc)
            {
                cc.CrowdControlSource = null;
            }
            unit.StopMovement();
        }

        public void OnUpdate(float diff)
        {
            if (_unit == null || _unit.IsDead) return;

            if (_unit is ObjAIBase ai)
            {
                // AI-driven units have the CrowdControlComponent re-issuing the wander/flee; the buff
                // only kept driving here for the legacy (non-AI-driven) path.
                if (ai.AICrowdControlActive) return;

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