using GameMaths;
using GameServerCore;
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
    public class ThreshRPentaBuff : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
            MaxStacks = 100,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; set; } = new StatsModifier();

        bool[] _wallBroken = new bool[5];
        Vector2[] _vertices = new Vector2[5];
        Particle[] _wallParticles = new Particle[5];
        Particle[] _greenWarnings = new Particle[5];
        Particle[] _redWarnings = new Particle[5];
        Particle[] _greenBarbs = new Particle[5];
        Particle[] _redBarbs = new Particle[5];

        bool _firstWallBroken = false;
        Spell _spell;
        ObjAIBase _owner;

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            _spell = ownerSpell;
            _owner = unit as ObjAIBase;
            var radius = 450f;
            var duration = buff.Duration;
            Vector2 centerPos = _owner.Position;

            Vector2 forward = new Vector2(_owner.Direction.X, _owner.Direction.Z);
            float baseAngle = (float)Math.Atan2(forward.Y, forward.X);
            float offset = baseAngle + (float)Math.PI;

            for (int i = 0; i < 5; i++)
            {
                float angle = offset + i * (float)Math.PI * 2 / 5;
                _vertices[i] = new Vector2(
                    centerPos.X + radius * (float)Math.Cos(angle),
                    centerPos.Y + radius * (float)Math.Sin(angle)
                );
            }

            for (int i = 0; i < 5; i++)
            {
                Vector2 start = _vertices[i];
                Vector2 end = _vertices[(i + 1) % 5];
                Vector2 wallCenter = (start + end) / 2.0f;

                Vector2 toCenterWall = centerPos - wallCenter;
                Vector2 wallDir2D = toCenterWall.LengthSquared() > 0 ? Vector2.Normalize(toCenterWall) : new Vector2(1, 0);
                Vector3 wallDir3D = new Vector3(wallDir2D.X, 0, wallDir2D.Y);

                _wallParticles[i] = AddParticlePos(_owner, "thresh_r_wall.troy", start, end, duration, 1.0f, "", "", wallDir3D);

                Vector2 toCenterNode = _owner.Position - _vertices[i];
                Vector2 diro2d = toCenterNode.LengthSquared() > 0 ? Vector2.Normalize(toCenterNode) : new Vector2(1, 0);
                Vector3 diro = new Vector3(diro2d.X, 0, diro2d.Y);

                _greenWarnings[i] = AddParticlePos(_owner, "thresh_r_warning_green.troy", start, end, duration, direction: diro, teamOnly:_owner.Team);
                _redWarnings[i] = AddParticlePos(_owner, "thresh_r_warning_red.troy", start, end, duration, direction: diro, teamOnly: CustomConvert.GetEnemyTeam(_owner.Team));
                AddParticlePos(_owner, "thresh_r_wall_teeth.troy", start, end, duration, 1.0f, "", "", diro);

                _greenBarbs[i] = AddParticlePos(_owner, "thresh_r_wall_barbs_green.troy", start, end, duration, direction: diro, teamOnly: _owner.Team);
                _redBarbs[i] = AddParticlePos(_owner, "thresh_r_wall_barbs_red.troy", start, end, duration, direction: diro, teamOnly: CustomConvert.GetEnemyTeam(_owner.Team));
            }
        }

        public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            for (int i = 0; i < 5; i++)
            {
                if (!_wallBroken[i])
                {
                    _wallParticles[i]?.SetToRemove();
                    _greenWarnings[i]?.SetToRemove();
                    _redWarnings[i]?.SetToRemove();
                }
                _greenBarbs[i]?.SetToRemove();
                _redBarbs[i]?.SetToRemove();
            }
        }

        public void OnUpdate(float diff)
        {
            for (int i = 0; i < 5; i++)
            {
                if (_wallBroken[i]) continue;

                Vector2 start = _vertices[i];
                Vector2 end = _vertices[(i + 1) % 5];
                Vector2 wallCenter = (start + end) / 2.0f;

                var enemies = GetChampionsInRange(wallCenter, 300f, true);
                foreach (var enemy in enemies)
                {
                    if (enemy.Team != _owner.Team && !enemy.IsDead && !HasBuff(enemy, "ThreshRWallImmunity"))
                    {
                        float dist = DistanceToLineSegment(enemy.Position, start, end);
                        if (dist <= enemy.CollisionRadius + 20f)
                        {
                            _wallBroken[i] = true;
                            _wallParticles[i]?.SetToRemove();
                            _greenWarnings[i]?.SetToRemove();
                            _redWarnings[i]?.SetToRemove();
                            _greenBarbs[i]?.SetToRemove();
                            _redBarbs[i]?.SetToRemove();

                            AddParticlePos(_owner, "thresh_r_wall_break.troy", start, end, 1.0f, 1.0f);
                            AddParticleTarget(_owner, enemy, "thresh_r_wall_hit.troy", enemy, 1.0f);

                            float damage = 0;
                            float slowDuration = 1.0f;

                            if (!_firstWallBroken)
                            {
                                _firstWallBroken = true;
                                damage = 250f + 150f * (_spell.CastInfo.SpellLevel - 1) + _owner.Stats.AbilityPower.Total * 1.0f;
                                slowDuration = 2.0f;
                            }

                            if (damage > 0)
                            {
                                enemy.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
                            }

                            AddBuff("ThreshRSlow", slowDuration, 1, _spell, enemy, _owner);
                            AddBuff("ThreshRWallImmunity", 1.0f, 1, _spell, enemy, _owner);

                            break;
                        }
                    }
                }
            }
        }

        private float DistanceToLineSegment(Vector2 p, Vector2 v, Vector2 w)
        {
            float l2 = Vector2.DistanceSquared(v, w);
            if (l2 == 0) return Vector2.Distance(p, v);
            float t = Math.Max(0, Math.Min(1, Vector2.Dot(p - v, w - v) / l2));
            Vector2 projection = v + t * (w - v);
            return Vector2.Distance(p, projection);
        }
    }
    public class ThreshRSlow : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.SLOW,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1
        };

        public StatsModifier StatsModifier { get; set; } = new StatsModifier();

        public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
        {
            StatsModifier.MoveSpeed.PercentBonus = -0.99f;
            unit.AddStatModifier(StatsModifier);
        }
    }
    public class ThreshRWallImmunity : IBuffGameScript
    {
        public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
        {
            BuffType = BuffType.INTERNAL,
            BuffAddType = BuffAddType.REPLACE_EXISTING,
            MaxStacks = 1,
            IsHidden = true
        };

        public StatsModifier StatsModifier { get; set; } = new StatsModifier();
    }
}