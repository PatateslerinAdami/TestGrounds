using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS.Missile
{
    public class SpellCircleMissile : SpellMissile
    {
        // Function Vars.
        private readonly float _circleAngularVelocity;
        private readonly float _circleRadialVelocity;
        private readonly Vector2 _circleCenter;

        private float _circleRadius;
        private float _circleAngle;
        private float _lifeElapsedMs;
        private float _targetSyncAccumulatorMs;

        public SpellCircleMissile(
            Game game,
            int collisionRadius,
            Spell originSpell,
            CastInfo castInfo,
            float moveSpeed,
            Vector2 overrideEndPos,
            uint netId = 0,
            bool serverOnly = false
        ) : base(game, collisionRadius, originSpell, castInfo, moveSpeed, netId, serverOnly)
        {
            InitializeDestination(overrideEndPos);

            _circleAngularVelocity = SpellOrigin.SpellData.CircleMissileAngularVelocity;
            _circleRadialVelocity = SpellOrigin.SpellData.CircleMissileRadialVelocity;

            _circleCenter = new Vector2(CastInfo.SpellCastLaunchPosition.X, CastInfo.SpellCastLaunchPosition.Z);
            var circleReferenceEnd = overrideEndPos != default
                ? overrideEndPos
                : new Vector2(CastInfo.TargetPositionEnd.X, CastInfo.TargetPositionEnd.Z);
            var circleOffset = circleReferenceEnd - _circleCenter;

            if (circleOffset.LengthSquared() <= float.Epsilon)
            {
                var fallbackDir = new Vector2(CastInfo.Owner.Direction.X, CastInfo.Owner.Direction.Z);
                if (fallbackDir.LengthSquared() <= float.Epsilon)
                {
                    fallbackDir = new Vector2(1.0f, 0.0f);
                }

                fallbackDir = Vector2.Normalize(fallbackDir);
                circleOffset = fallbackDir * SpellOrigin.GetCurrentCastRange();
            }

            _circleRadius = circleOffset.Length();
            _circleAngle = MathF.Atan2(circleOffset.Y, circleOffset.X);

            // S1-faithful unconditional polar placement (obj_SpellCircleMissile::
            // UpdateProjectile: pos = center + cos/sin*radius, no straight-line mode).
            // With both velocities 0 the offset never changes — the missile sits at a
            // fixed offset / attaches to the (tracked) center, which is exactly the
            // zero-velocity attachment class (StyleBatteringRam, jungle mushrooms).
            Position = _circleCenter + new Vector2(MathF.Cos(_circleAngle), MathF.Sin(_circleAngle)) * _circleRadius;

            var tangentSign = (float)MathF.Sign(_circleAngularVelocity);
            if (MathF.Abs(tangentSign) <= float.Epsilon)
            {
                tangentSign = 1.0f;
            }
            var tangent = new Vector2(-MathF.Sin(_circleAngle), MathF.Cos(_circleAngle)) * tangentSign;
            Direction = new Vector3(tangent.X, 0.0f, tangent.Y);
        }

        // Circle — orbit missiles (e.g. Diana W orbs). This Type routes the missile into
        // PacketNotifier's polar GetReplicationDirection branch (radius/angle encoding the
        // client needs to reconstruct the orbit).
        public override MissileType Type { get; protected set; } = MissileType.Circle;

        public override int HitCount => ObjectsHit.Count;

        public override void OnAdded()
        {
            base.OnAdded();

            // Orbit missiles like Diana W orbs are visually attached to a moving unit.
            // A post-spawn target sync binds the orbit center correctly.
            if (HasTarget())
            {
                _game.PacketNotifier.NotifyS2C_ChangeMissileTarget(this);
            }
        }

        public Vector3 GetReplicationDirection()
        {
            // The client's circle class reads MISREP.direction as (radius, phase)
            // UNCONDITIONALLY (SpellCircleMissileClient::OnNetworkPacket: mRotateRadius
            // = direction.x, mRotatePhase = direction.y) — so polar data always.
            //
            // Return the CURRENT (radius, phase), not the spawn-time values: a missile that
            // enters a client's vision mid-flight is re-replicated (PacketNotifier:845,
            // GetTimeSinceCreation() > 0) and the client sets mRotateRadius/mRotatePhase raw
            // from these — it fast-forwards only the lifetime via timeFromCreation, NOT the
            // polar coords. For non-zero radial/angular velocity the radius/phase have already
            // advanced, so spawn-time values would place the orbit at the wrong spot. At spawn
            // (before any Move) these equal the initial values, so spawn replication is
            // unchanged. Verified against SpellCircleMissileClient.cpp (mac decomp 4.17).
            return new Vector3(_circleRadius, _circleAngle, 0.0f);
        }

        public override void Update(float diff)
        {
            if (IsToRemove())
            {
                return;
            }

            _timeSinceCreation += diff;
            UpdateTimedSpeedChange();
            _lifeElapsedMs += diff;

            // Lifetime self-expiry (client mirrors this locally from MissileLifetime,
            // see the destroy-packet suppression in SpellMissile.SetToRemove). Missiles
            // without a lifetime live until a script/hit removes them (attachment class).
            var lifetimeSeconds = SpellOrigin.SpellData.MissileLifetime;
            if (lifetimeSeconds > 0.0f && _lifeElapsedMs >= lifetimeSeconds * 1000.0f)
            {
                SetToRemove();
                return;
            }

            var positionBeforeMove = Position;
            Move(diff);
            CheckSweptCollision(positionBeforeMove);
            PublishOnSpellMissileUpdate(diff, positionBeforeMove);
        }

        public override void OnCollision(GameObject collider, bool isTerrain = false)
        {
            if (IsToRemove() || (Destination != Vector2.Zero && collider is ObjBuilding))
            {
                return;
            }

            if (isTerrain)
            {
                // Script-side opt-in via OnCollisionTerrain keyed on the missile — see
                // SpellLineMissile.OnCollision for the full rationale (Nautilus Q pattern).
                API.ApiEventManager.OnCollisionTerrain.Publish(this);
                return;
            }

            if (Destination != Vector2.Zero)
            {
                CheckFlagsForUnit(collider as AttackableUnit);
            }
        }

        /// <summary>
        /// Unconditional polar motion (S1 obj_SpellCircleMissile::UpdateProjectile):
        /// position = center + (cos/sin angle) * radius, angle/radius advanced by the
        /// spell's angular/radial velocities. Zero velocities = fixed offset from the
        /// (live) center — the attachment behavior, NOT straight-line travel.
        /// </summary>
        /// <param name="diff">The amount of milliseconds the AI is supposed to move</param>
        public override void Move(float diff)
        {
            var deltaSeconds = diff * 0.001f;
            _circleRadius = MathF.Max(0.0f, _circleRadius + _circleRadialVelocity * deltaSeconds);
            _circleAngle += _circleAngularVelocity * deltaSeconds;

            // Orbital center selection:
            // • If a CastTarget is passed (TargetUnit != null), orbit around its live
            //   position — matches Riot's client behavior (SpellCircleMissileClient reads
            //   mCastInfo.Targets[0] as the rotation anchor for live-tracking orbits like
            //   Diana W's orbs).
            // • Otherwise fall back to the static launch position (e.g. Ahri W ball,
            //   which orbits a fixed spawn point, not a unit).
            var center = TargetUnit != null ? TargetUnit.Position : _circleCenter;
            var next = center + new Vector2(MathF.Cos(_circleAngle), MathF.Sin(_circleAngle)) * _circleRadius;
            var movement = next - Position;

            if (movement.LengthSquared() > float.Epsilon)
            {
                var moveDir = Vector2.Normalize(movement);
                Direction = new Vector3(moveDir.X, 0.0f, moveDir.Y);
            }

            Position = next;

            // Periodic ChangeMissileTarget broadcast to keep the client's missile
            // target-anchor in sync — matches OnAdded's initial sync, refreshed every
            // 100ms while we have a tracked target.
            if (HasTarget())
            {
                _targetSyncAccumulatorMs += diff;
                if (_targetSyncAccumulatorMs >= 100.0f)
                {
                    _targetSyncAccumulatorMs = 0.0f;
                    _game.PacketNotifier.NotifyS2C_ChangeMissileTarget(this);
                }
            }
        }

        public override void CheckFlagsForUnit(AttackableUnit unit)
        {
            if (unit == null || !HasDestination() || ObjectsHit.Contains(unit) || !SpellOrigin.SpellData.IsValidTarget(CastInfo.Owner, unit))
            {
                return;
            }

            ObjectsHit.Add(unit);

            if (SpellOrigin != null)
            {
                SpellOrigin.ApplyEffects(unit, this);
            }

            if (CastInfo.Owner is ObjAIBase ai && SpellOrigin.CastInfo.IsAutoAttack)
            {
                ai.AutoAttackHit(TargetUnit, CastInfo.Targets.Count > 0 ? CastInfo.Targets[0].HitResult : (HitResult?)null);
            }

            // Per-level hit budget, same family as the line-missile rule (0 = uncapped).
            // S4's SpellCircleMissile::UpdateCircleMissile demonstrably READS
            // mi_MaximumHits[SpellLevel] (DWARF locals numMaxHitsAtCurrentLevel + the
            // >= 0 assert) — the reconstructed body is still a decomp stub, so the exact
            // consumption is presumed line-equivalent; revisit when the body lands.
            int maxHits = SpellOrigin?.Script?.ScriptMetadata?.MissileParameters?
                .GetMaximumHits(CastInfo.SpellLevel) ?? 0;
            if (maxHits > 0 && ObjectsHit.Count >= maxHits)
            {
                SetToRemove();
            }
        }

    }
}
