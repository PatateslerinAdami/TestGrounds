using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells
{
    public class AatroxQ : ISpellScript
    {
        private const float MaxDashRange = 650;
        private const float LandingPositionTolerance = 0.01f;
        private const float JumpToDashDelaySeconds = 0.3f;
        private const float LandingDashDurationSeconds = 0.3f;
        private const float DesiredJumpHeight = 500;
        private const float JumpDistance = 10;
        private ObjAIBase _aatrox;
        private Vector2 _castStartPos2D;
        private Vector2 _endPos2D;
        private float _damage;
        private ForceMovementParameters _movement;
        private Buff _stage;
        private Particle _marker;
        private string _animation;

        public SpellScriptMetadata ScriptMetadata { get; } = new SpellScriptMetadata()
        {
            TriggersSpellCasts = true
        };

        public void OnActivate(ObjAIBase owner, Spell spell)
        {
            _aatrox = owner;
            ApiEventManager.OnUpdateStats.AddListener(this, owner, OnUpdateStats);
        }

        public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
        {
            AatroxHelpers.PayHealth(owner, owner.Stats.CurrentHealth * 0.1f);
            _damage = 70 + 45 * (spell.CastInfo.SpellLevel - 1) + 0.6f * (owner.Stats.AttackDamage.Total - owner.Stats.AttackDamage.BaseValue);
            _castStartPos2D = owner.Position;
            _endPos2D = end != Vector2.Zero ? end : start;
            var direction = _endPos2D - _castStartPos2D;
            if (direction.Length() > MaxDashRange)
            {
                _endPos2D = _castStartPos2D + Vector2.Normalize(direction) * MaxDashRange;
            }

            _endPos2D = ResolveLandingPosition(owner, _endPos2D);
        }

        private Vector2 ResolveLandingPosition(ObjAIBase owner, Vector2 destination)
        {
            if (owner.MovementRestrictionRadius > 0f)
            {
                var offset = destination - owner.MovementRestrictionCenter;
                if (offset.LengthSquared() > owner.MovementRestrictionRadius * owner.MovementRestrictionRadius)
                {
                    destination = owner.MovementRestrictionCenter + Vector2.Normalize(offset) * Math.Max(0f, owner.MovementRestrictionRadius - LandingPositionTolerance);
                }
            }

            var landingPosition = GetClosestTerrainExit(destination, owner.PathfindingRadius + 1f);
            if (IsValidLandingPosition(owner, landingPosition))
            {
                return landingPosition;
            }

            var offsetToStart = _castStartPos2D - destination;
            var distance = offsetToStart.Length();
            for (float retreat = 10f; retreat < distance; retreat += 10f)
            {
                landingPosition = destination + offsetToStart * (retreat / distance);
                if (IsValidLandingPosition(owner, landingPosition))
                {
                    return landingPosition;
                }
            }

            return _castStartPos2D;
        }

        private bool IsValidLandingPosition(ObjAIBase owner, Vector2 position)
        {
            const float range = MaxDashRange + LandingPositionTolerance;
            return Vector2.DistanceSquared(_castStartPos2D, position) <= range * range
                && (owner.MovementRestrictionRadius <= 0f
                || Vector2.DistanceSquared(owner.MovementRestrictionCenter, position) <= owner.MovementRestrictionRadius * owner.MovementRestrictionRadius)
                && IsWalkable(position.X, position.Y, owner.PathfindingRadius + 1f);
        }

        public void OnSpellPostCast(Spell spell)
        {
            var landing = _endPos2D;
            float damage = _damage;
            var direction = landing - _aatrox.Position;
            if (direction.LengthSquared() < 0.001f)
            {
                direction = new Vector2(_aatrox.Direction.X, _aatrox.Direction.Z);
            }

            if (direction.LengthSquared() < 0.001f)
            {
                direction = Vector2.UnitX;
            }

            direction = Vector2.Normalize(direction);
            FaceDirection(_aatrox.Position + direction, _aatrox, true);
            _animation = "Spell1";
            _marker = AddParticle(_aatrox, null, AatroxHelpers.Effect(_aatrox, "Q_Tar_Green"), landing,
                enemyParticle: AatroxHelpers.Effect(_aatrox, "Q_Tar_Red"));
            _stage = AddBuff("AatroxQ", 10, 1, spell, _aatrox, _aatrox);
            var jumpParameters = GetDashParameters(JumpDistance, 0.6f, DesiredJumpHeight);
            var takeoff = new ForceMovementParameters
            {
                TargetPosition = _aatrox.Position,
                ParabolicStartPoint = _aatrox.Position - direction * JumpDistance,
                PathSpeedOverride = jumpParameters.Speed,
                ParabolicGravity = jumpParameters.Gravity,
                Duration = JumpToDashDelaySeconds,
                IsStationaryJump = true,
                KeepFacingDirection = true,
                MovementOrdersFacing = ForceMovementOrdersFacing.KEEP_CURRENT_FACING,
                IgnoreTerrain = true,
                Caster = _aatrox,
                MovementName = "AatroxQJump",
                Animation = _animation,
                AnimationFlags = AnimationFlags.UniqueOverride | AnimationFlags.Unknown6,
                AnimationTimeScale = 0,
                AnimationSpeedScale = 1,
                SetStatus = StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast,
                BlockedSpellSlots = new HashSet<byte>() { 0, 1, 2, 3 },
                BufferSpellCasts = true
            };
            Move(takeoff, () =>
            {
                if (_stage == null || _stage.Remove || _stage.Name != "AatroxQDescent")
                {
                    RemoveStage();
                    _stage = AddBuff("AatroxQDescent", 5, 1, spell, _aatrox, _aatrox);
                }

                var descent = new ForceMovementParameters
                {
                    TargetPosition = landing,
                    ParabolicStartPoint = _aatrox.Position,
                    Duration = LandingDashDurationSeconds,
                    ParabolicGravity = 0,
                    IgnoreTerrain = true,
                    Caster = _aatrox,
                    MovementName = "AatroxQDash",
                    SetStatus = StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast,
                    BlockedSpellSlots = new HashSet<byte>() { 0, 1, 2, 3 },
                    BufferSpellCasts = true
                };
                Move(descent, () => Land(spell, damage));
            });
        }

        private void Move(ForceMovementParameters movement, Action complete)
        {
            _movement = movement;
            Action<AttackableUnit, ForceMovementParameters> success = null, failure = null;
            void Unsubscribe()
            {
                ApiEventManager.OnMoveSuccess.RemoveListener(this, _aatrox, success);
                ApiEventManager.OnMoveFailure.RemoveListener(this, _aatrox, failure);
            }

            success = (_, ended) =>
            {
                if (ended != movement)
                {
                    return;
                }

                Unsubscribe();
                _movement = null;
                if (_aatrox.IsDead || _aatrox.IsToRemove())
                {
                    Cleanup();
                    return;
                }

                complete();
            };
            failure = (_, ended) =>
            {
                if (ended != movement)
                {
                    return;
                }

                Unsubscribe();
                _movement = null;
                Cleanup();
            };
            ApiEventManager.OnMoveSuccess.AddListener(this, _aatrox, success);
            ApiEventManager.OnMoveFailure.AddListener(this, _aatrox, failure);
            _aatrox.StartForcedMovement(movement);
            if (_aatrox.MovementParameters != movement)
            {
                Unsubscribe();
                _movement = null;
                Cleanup();
            }
        }

        private void Land(Spell spell, float damage)
        {
            Cleanup();
            AddParticle(_aatrox, null, AatroxHelpers.Effect(_aatrox, "Q_Land"), _aatrox.Position);
            foreach (var enemy in GetUnitsInRange(_aatrox, _aatrox.Position, 300, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral))
            {
                if (enemy.IsDead || enemy.IsToRemove() || !spell.SpellData.IsValidTarget(_aatrox, enemy))
                {
                    continue;
                }

                if (Vector2.Distance(enemy.Position, _aatrox.Position) <= 150 + enemy.CollisionRadius)
                {
                    AddBuff("AatroxQKnockup", 1, 1, spell, enemy, _aatrox);
                }

                AddParticleTarget(_aatrox, enemy, AatroxHelpers.Effect(_aatrox, "Q_Hit"), enemy, 0.5f);
                enemy.TakeDamage(_aatrox, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            }
        }

        private void RemoveStage()
        {
            var stage = _stage;
            _stage = null;
            if (stage != null && !stage.Remove)
            {
                RemoveBuff(stage);
            }
        }

        private void Cleanup()
        {
            RemoveStage();
            RemoveParticle(_marker);
            _marker = null;
            if (_animation != null)
            {
                StopAnimation(_aatrox, _animation, fade: true);
            }

            _animation = null;
        }

        private void OnUpdateStats(AttackableUnit unit, float diff)
        {
            SetSpellToolTipVar(_aatrox, 2, _aatrox.Stats.CurrentHealth * 0.1f, SpellbookType.SPELLBOOK_CHAMPION, 0,
                SpellSlotType.SpellSlots);
        }

        public void OnDeactivate(ObjAIBase owner, Spell spell)
        {
            if (_movement != null && owner.MovementParameters == _movement)
            {
                CancelDash(owner);
            }

            Cleanup();
            ApiEventManager.RemoveAllListenersForOwner(this);
        }
    }
}
