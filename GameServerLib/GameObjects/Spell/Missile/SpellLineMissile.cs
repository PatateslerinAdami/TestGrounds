using System;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS.Missile
{
    public class SpellLineMissile : SpellMissile
    {
        // Function Vars.
        private bool _atDestination;
        // Whether the tracked initial target was already hit (LineMissileTrackUnitsAndContinues:
        // tracking stops after the first target hit and the missile continues straight).
        private bool _hitInitialTarget;

        // Arc = the client's LINE missile class (S4 CreateArcMissile instantiates
        // SpellLineMissileClient). Derives directly from SpellMissile like Riot's flat
        // hierarchy — keep it that way so circle-only features (polar replication,
        // MissileLifetime self-expiry) can never leak into line missiles again.
        public override MissileType Type { get; protected set; } = MissileType.Arc;

        public override int HitCount => ObjectsHit.Count;

        public SpellLineMissile(
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

            if (SpellOrigin?.SpellData != null && SpellOrigin.SpellData.MissileFixedTravelTime > 0)
            {
                float distance = Vector2.Distance(Position, Destination);
                _moveSpeed = distance / SpellOrigin.SpellData.MissileFixedTravelTime;
            }
        }

        public override void Update(float diff)
        {
            if (IsToRemove())
            {
                return;
            }

            if (!HasDestination() || _atDestination)
            {
                // Reaching the max-range endpoint is client-INFERABLE (the client knows
                // launch + direction + range + speed), so Riot sends NO DestroyClientMissile
                // for it — replay-verified on Jinx W (d756cd43: 27/27 max-range misses carry
                // no 0x5A; only unit hits, which remove the missile early/off-schedule, get a
                // destroy). Unit-hit removal goes through CheckFlagsForUnit / the script's
                // SetToRemove instead and keeps the destroy.
                if (_atDestination)
                {
                    SuppressDestroyNotify = true;
                }
                SetToRemove();
                return;
            }

            if (SpellOrigin != null && SpellOrigin.SpellData != null)
            {
                float accel = SpellOrigin.SpellData.MissileAccel;
                if (accel != 0)
                {
                    _moveSpeed += accel * (diff / 1000.0f);

                    float minSpeed = SpellOrigin.SpellData.MissileMinSpeed;
                    float maxSpeed = SpellOrigin.SpellData.MissileMaxSpeed;

                    if (minSpeed > 0 && _moveSpeed < minSpeed)
                    {
                        _moveSpeed = minSpeed;
                    }
                    else if (maxSpeed > 0 && _moveSpeed > maxSpeed)
                    {
                        _moveSpeed = maxSpeed;
                    }
                }
            }

            // LineMissileTrackUnits homing (S4 SpellLineMissile.cpp:923-930): re-aim the
            // missile at its target unit EVERY update — the original Destination (far end
            // point) only remains as the fallback once the target is gone/dead. With the
            // AndContinues variant, tracking stops after the initial target was hit and the
            // missile continues straight. E.g. Talon W return blades (TalonRakeMissileTwo).
            if (TargetUnit != null && !TargetUnit.IsDead
                && SpellOrigin?.SpellData != null && SpellOrigin.SpellData.LineMissileTrackUnits
                && !(SpellOrigin.SpellData.LineMissileTrackUnitsAndContinues && _hitInitialTarget))
            {
                Destination = TargetUnit.Position;
            }

            _timeSinceCreation += diff;
            UpdateTimedSpeedChange();
            var positionBeforeMove = Position;
            Move(diff);
            CheckSweptCollision(positionBeforeMove);
            PublishOnSpellMissileUpdate(diff, positionBeforeMove);
        }

        public override void OnCollision(GameObject collider, bool isTerrain = false)
        {
            // Tracking missiles (LineMissileTrackUnits) home onto TargetUnit but still
            // collide with valid units along the way (Talon W return blades damage every
            // enemy they pass through) — only non-tracking targeted missiles are exclusive
            // to their target.
            bool tracksUnits = SpellOrigin?.SpellData != null && SpellOrigin.SpellData.LineMissileTrackUnits;
            if (IsToRemove() || (TargetUnit != null && collider != TargetUnit && !tracksUnits) || (Destination != Vector2.Zero && collider is ObjBuilding))
            {
                return;
            }

            if (isTerrain)
            {
                // Script-side opt-in: CollisionHandler calls this every tick the missile
                // position is unwalkable; spells that care about terrain (Nautilus Q) listen
                // for OnCollisionTerrain keyed on the missile and handle the wall hit there
                // (kill missile + pull). Riot did the same check in the spell's server Lua,
                // paced by LuaOnMissileUpdateDistanceInterval (Naut Q: 50u). Everything else
                // ignores the event and keeps flying over walls.
                API.ApiEventManager.OnCollisionTerrain.Publish(this);
                return;
            }

            if (Destination != Vector2.Zero)
            {
                CheckFlagsForUnit(collider as AttackableUnit);
            }
        }

        /// <summary>
        /// Moves this projectile to either its target unit, or its destination, and updates its coordinates along the way.
        /// </summary>
        /// <param name="diff">The amount of milliseconds the AI is supposed to move</param>
        public override void Move(float diff)
        {
            // current position
            var cur = Position;

            var next = Destination;

            // 3D direction is kept for replication packets only (the client wants the
            // height-aware vector). Movement itself MUST step in 2D: stepping with the
            // 3D-normalized direction shortens the XZ step whenever Direction.Y != 0
            // (|Direction.XZ| < 1), so the missile asymptotically approaches a moving
            // tracked target without ever reaching it — hovering at the target as a
            // damage aura. Surfaced once heights became smoothly interpolated (bilinear
            // fix); the old floor-sampled flat plateaus masked it with Y-diff = 0.
            var goingTo = new Vector3(next.X, _game.Map.NavigationGrid.GetHeightAtLocation(next), next.Y)
                        - new Vector3(cur.X, _game.Map.NavigationGrid.GetHeightAtLocation(cur), cur.Y);
            var dirTemp = Vector3.Normalize(goingTo);

            // usually doesn't happen
            if (float.IsNaN(dirTemp.X) || float.IsNaN(dirTemp.Y) || float.IsNaN(dirTemp.Z))
            {
                dirTemp = new Vector3(0, 0, 0);
            }

            Direction = dirTemp;

            var moveSpeed = GetSpeed();

            var dist = Vector2.Distance(cur, next);

            var deltaMovement = moveSpeed * 0.001f * diff;

            // Reached (or would step past) the destination this tick: snap onto it and
            // flag arrival robustly instead of relying on exact float equality.
            if (deltaMovement >= dist || dist <= MOVEMENT_EPSILON)
            {
                Position = next;
                _atDestination = true;
                return;
            }

            var dir2D = (next - cur) / dist;
            Position = new Vector2(cur.X + dir2D.X * deltaMovement, cur.Y + dir2D.Y * deltaMovement);
        }

        public override void CheckFlagsForUnit(AttackableUnit unit)
        {
            // ObjectsHit.Contains: one hit per unit per missile — same dedup as the
            // SpellCircleMissile base version. This override (added for _hitInitialTarget
            // homing bookkeeping) originally dropped the check, making piercing line
            // missiles re-apply effects EVERY collision tick while overlapping a unit
            // (Irelia R multi-damage; Talon W/R and Aatrox E only masked it via their
            // script-side dedup trackers).
            if (unit == null || !HasDestination() || ObjectsHit.Contains(unit) || !SpellOrigin.SpellData.IsValidTarget(CastInfo.Owner, unit))
            {
                return;
            }

            ObjectsHit.Add(unit);

            if (unit == TargetUnit)
            {
                _hitInitialTarget = true;
            }

            if (SpellOrigin != null)
            {
                SpellOrigin.ApplyEffects(unit, this);
            }

            if (CastInfo.Owner is ObjAIBase ai && SpellOrigin.CastInfo.IsAutoAttack)
            {
                ai.AutoAttackHit(TargetUnit, CastInfo.Targets.Count > 0 ? CastInfo.Targets[0].HitResult : (HitResult?)null);
            }

            // Per-level hit budget (S4 SpellLineMissile.cpp:437+763: mi_MaximumHits
            // [SpellLevel], LINE rule 0 = uncapped — the OPPOSITE of the chain-missile
            // "0 = never bounce"). The budget-filling hit still applies its effects;
            // the missile ends right after. S4's bLineMissileBounces return-flight
            // (endpoint reset to caster + mAlreadyHit.clear() so the way back re-hits
            // everything — Sivir Q boomerang) is NOT ported: no consumer in the roster.
            int maxHits = SpellOrigin?.Script?.ScriptMetadata?.MissileParameters?
                .GetMaximumHits(CastInfo.SpellLevel) ?? 0;
            if (maxHits > 0 && ObjectsHit.Count >= maxHits)
            {
                SetToRemove();
            }
        }

        // RESOLVED (2026-06-07): line missiles need NO own IsValidTarget — they filter
        // solely via the spell JSON's Affect flags (SpellData.IsValidTarget in
        // CheckFlagsForUnit above). Verified two ways: the S4 client's SpellLineMissile
        // never reads the mi_CanHit* fields (only the mi_MaximumHits budget), and the
        // full S1 Lua corpus (3437 spell scripts) shows non-default CanHit* values
        // EXCLUSIVELY on chain missiles (CanHitCaster=1 only SpellFlux,
        // CanHitSameTarget=1 only DarkWind+SpellFlux, CanHitFriends=1 nobody). The
        // chain-specific filters live in SpellChainMissile.IsValidTarget.
    }
}
