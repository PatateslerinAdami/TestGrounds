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
        // bLineMissileBounces return leg (S4 SpellLineMissile.cpp:826-900): instead of dying at
        // endpoint/target/hit-budget, the missile turns around and homes on the caster, re-hitting
        // everything on the way back (Lux W boomerang; Draven R adds UsesAccelerationForBounce).
        private bool _hasBounced;

        // Total XZ distance flown (S4 mDistanceTraveled). Only consumed by the
        // LineMissileTrackUnitsAndContinues continuation (UpdateContinueThroughMissle,
        // SpellLineMissile.cpp:916): distanceLeft = CastRange - mDistanceTraveled.
        private float _distanceTraveled;

        // Read by MissileReplication construction: an enter-vision replication of a returning
        // missile must carry the wire Bounced flag so the client aims it at the caster.
        public bool HasBounced => _hasBounced;

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

            // Reaching the endpoint with bLineMissileBounces set starts the return leg instead
            // of ending the missile (a bounced missile that arrives back at the caster ends
            // normally below).
            if (_atDestination && !_hasBounced
                && SpellOrigin?.SpellData != null && SpellOrigin.SpellData.LineMissileBounces != 0)
            {
                Bounce();
            }
            else if (!HasDestination() || _atDestination)
            {
                // Reaching the max-range endpoint is client-INFERABLE (the client knows
                // launch + direction + range + speed), so Riot sends NO DestroyClientMissile
                // for it — replay-verified on Jinx W (d756cd43: 27/27 max-range misses carry
                // no 0x5A; only unit hits, which remove the missile early/off-schedule, get a
                // destroy). Unit-hit removal goes through CheckFlagsForUnit / the script's
                // SetToRemove instead and keeps the destroy.
                //
                // EXCEPTION — arrival at a LIVE tracked unit (LineMissileTrackUnits homing onto
                // a moving unit, e.g. Talon W return blade catching Talon): NOT client-inferable.
                // The client homes its own copy onto its own copy of the unit, whose position
                // lags the server's while the unit moves, so the client missile never reaches it
                // until the unit stops — the blade visibly trails the unit for as long as it keeps
                // moving. Keep the destroy so the client removes the missile the instant the
                // server's copy arrives.
                // "Tracks a live target" for the destroy decision: a missile still homing on a
                // living unit. Once an AndContinues missile has hit its initial target it flies
                // STRAIGHT (no longer homing), so it reverts to the normal endpoint rule.
                bool tracksLiveTarget = TargetUnit != null && !TargetUnit.IsDead
                    && SpellOrigin?.SpellData != null && SpellOrigin.SpellData.LineMissileTrackUnits
                    && !(SpellOrigin.SpellData.LineMissileTrackUnitsAndContinues && _hitInitialTarget);

                // Arrival at the live tracked target IS the target's hit — Riot registers it on
                // the destroy tick, never at en-route radius contact (replay 9c0533a1: the
                // LineMissileHitList carrying the tracked target coincides with the missile's
                // DestroyClientMissile in 40/40 AhriOrbReturn and 60/60 TalonRakeMissileTwo
                // catches; zero contact-early hits). OnCollision excludes the tracked target for
                // exactly this reason, so ObjectsHit can't have it yet. Applied DIRECTLY via
                // ApplyEffects, NOT CheckFlagsForUnit: the designated target is hit regardless of
                // the spell's Affect flags (TalonRakeMissileTwo's flags exclude allies, yet
                // Talon's catch hit is on the wire 60/60) — ApplyEffects' own designated-ally and
                // spell-shield gates still apply.
                if (_atDestination && tracksLiveTarget && !ObjectsHit.Contains(TargetUnit))
                {
                    ObjectsHit.Add(TargetUnit);
                    SpellOrigin?.ApplyEffects(TargetUnit, this);
                    if (CastInfo.Owner is ObjAIBase arrivalAi && SpellOrigin != null && SpellOrigin.CastInfo.IsAutoAttack)
                    {
                        arrivalAi.AutoAttackHit(TargetUnit, CastInfo.Targets.Count > 0 ? CastInfo.Targets[0].HitResult : (HitResult?)null);
                    }

                    // LineMissileTrackUnitsAndContinues (S4 UpdateContinueThroughMissle,
                    // SpellLineMissile.cpp:916): reaching the tracked target does NOT end the
                    // missile. It marks the initial target hit, then re-aims to fly STRAIGHT for
                    // the REMAINING CastRange along a blend of the contact heading and the
                    // caster->target direction, re-hitting pass-through units the rest of the way;
                    // it ends only when that straight endpoint is reached (or the range is already
                    // used up). No 4.20 spell sets this flag, so nothing exercises this today —
                    // kept faithful for a future port. (Guarded by !_hitInitialTarget so it fires
                    // once; the far-endpoint arrival falls through to the normal end below.)
                    if (SpellOrigin.SpellData.LineMissileTrackUnitsAndContinues && !_hitInitialTarget)
                    {
                        _hitInitialTarget = true;

                        var castRange = SpellOrigin.SpellData.CastRange;
                        int wireLevel = Math.Clamp(CastInfo.SpellLevel - 1, 0, castRange.Length - 1);
                        float distanceLeft = castRange[wireLevel] - _distanceTraveled;
                        // Contact heading (S4 mPlaneDirection = current flight direction) and the
                        // launch->target direction, both flattened to XZ.
                        var contactDir = new Vector2(Direction.X, Direction.Z);
                        var launch = new Vector2(CastInfo.SpellCastLaunchPosition.X, CastInfo.SpellCastLaunchPosition.Z);
                        var casterToTarget = TargetUnit.Position - launch;
                        if (distanceLeft > 0f && contactDir.LengthSquared() > 0f && casterToTarget.LengthSquared() > 0f)
                        {
                            contactDir = Vector2.Normalize(contactDir);
                            casterToTarget = Vector2.Normalize(casterToTarget);
                            var newDir = contactDir == casterToTarget
                                ? contactDir
                                : Vector2.Normalize(contactDir + casterToTarget);
                            Destination = Position + newDir * distanceLeft;
                            _atDestination = false;
                            return; // keep flying straight; do NOT end here
                        }
                    }

                    _hitInitialTarget = true;
                }

                if (_atDestination && !tracksLiveTarget)
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
                    // S4 SpellLineMissile.cpp:576: a LineMissileUsesAccelerationForBounce missile
                    // ignores its normal acceleration and decelerates at -|accel| once it can no
                    // longer stop before the endpoint (v²/2|a| >= remaining distance), so it
                    // arrives at ~0 speed for the turn-around (Draven R blades slowing into the
                    // reversal). Floored at 0: physically v reaches 0 exactly at the endpoint;
                    // the floor only absorbs discretization error (next tick re-accelerates).
                    if (!_hasBounced && SpellOrigin.SpellData.LineMissileUsesAccelerationForBounce
                        && _moveSpeed > 0
                        && _moveSpeed * _moveSpeed / (2f * Math.Abs(accel)) >= Vector2.Distance(Position, Destination))
                    {
                        _moveSpeed = Math.Max(0f, _moveSpeed - Math.Abs(accel) * (diff / 1000f));
                    }
                    else
                    {
                        _moveSpeed += accel * (diff / 1000.0f);

                        // The S4 clamp is one-sided by accel sign (SpellLineMissile.cpp:600):
                        // accelerating clamps only at MissileMaxSpeed, decelerating only at
                        // MissileMinSpeed. Matters post-bounce: a reversed (negative) speed must
                        // rise back through 0 without snapping to the minimum.
                        if (accel > 0)
                        {
                            float maxSpeed = SpellOrigin.SpellData.MissileMaxSpeed;
                            if (maxSpeed > 0 && _moveSpeed > maxSpeed)
                            {
                                _moveSpeed = maxSpeed;
                            }
                        }
                        else
                        {
                            float minSpeed = SpellOrigin.SpellData.MissileMinSpeed;
                            if (_moveSpeed < minSpeed)
                            {
                                _moveSpeed = minSpeed;
                            }
                        }
                    }
                }
            }

            // LineMissileTrackUnits homing (S4 SpellLineMissile.cpp:923-930): re-aim the
            // missile at its target unit EVERY update — the original Destination (far end
            // point) only remains as the fallback once the target is gone/dead. With the
            // AndContinues variant, tracking stops after the initial target was hit and the
            // missile continues straight. E.g. Talon W return blades (TalonRakeMissileTwo).
            if (TargetUnit != null && !TargetUnit.IsDead && !_hasBounced
                && SpellOrigin?.SpellData != null && SpellOrigin.SpellData.LineMissileTrackUnits
                && !(SpellOrigin.SpellData.LineMissileTrackUnitsAndContinues && _hitInitialTarget))
            {
                Destination = TargetUnit.Position;
            }

            // S4 SpellLineMissile.cpp:706: a bounced missile re-aims its endpoint at the caster's
            // position EVERY frame, so the return leg follows a moving caster. (The client gates
            // the re-aim on caster visibility because it may not know the position in fog — the
            // server always does.)
            if (_hasBounced && CastInfo?.Owner != null)
            {
                Destination = CastInfo.Owner.Position;
            }

            _timeSinceCreation += diff;
            UpdateTimedSpeedChange();
            var positionBeforeMove = Position;
            Move(diff);
            _distanceTraveled += Vector2.Distance(positionBeforeMove, Position);
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

            // The tracked TARGET is hit at ARRIVAL (Update's _atDestination branch), never at
            // en-route radius contact — wire-verified: the hit-list carrying the tracked target
            // rides the destroy tick in 100/100 tracked-return catches (Ahri Q returns + Talon W
            // return blades, replay 9c0533a1). A contact hit here fired LineWidth + unit radius
            // (~165u) early, applying effects and (via scripts) destroying the missile visibly
            // before it reached the target. This holds for AndContinues too: the decomp marks the
            // initial target hit at ENDPOINT arrival (UpdateContinueThroughMissle sets
            // mHitInitialTarget on isAtEndPoint, SpellLineMissile.cpp:919), not on en-route radius
            // contact — the missile is chasing the target, so it only overlaps it at the catch.
            if (tracksUnits && collider == TargetUnit)
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
        /// Starts the bLineMissileBounces return leg (S4 SpellLineMissile.cpp:858-899): the missile
        /// turns around at its endpoint (or when its hit budget fills), clears the already-hit list
        /// so the way back re-hits everything, and homes on the caster from now on (see Update).
        /// </summary>
        private void Bounce()
        {
            _hasBounced = true;
            _atDestination = false;
            ObjectsHit.Clear();
            Destination = CastInfo.Owner.Position;

            // S4: with UsesAccelerationForBounce the speed is negated at the turn-around (it
            // decelerated to ~0 on approach, then the positive accel drives it back up toward the
            // caster — Draven R). Without it, Riot only zeroes the velocity for one frame and the
            // next frame re-derives it from the unchanged speed toward the new endpoint (Lux W
            // returns at constant speed) — our scalar speed model needs no change for that.
            if (SpellOrigin?.SpellData != null && SpellOrigin.SpellData.LineMissileUsesAccelerationForBounce)
            {
                _moveSpeed = -_moveSpeed;
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
            // the missile ends right after — or, with bLineMissileBounces, starts its
            // return leg instead (filling the budget is a bounce trigger alongside
            // endpoint/target arrival, S4:838; Bounce() clears the budget again).
            int maxHits = SpellOrigin?.Script?.ScriptMetadata?.MissileParameters?
                .GetMaximumHits(CastInfo.SpellLevel) ?? 0;
            if (maxHits > 0 && ObjectsHit.Count >= maxHits)
            {
                if (!_hasBounced && SpellOrigin?.SpellData != null && SpellOrigin.SpellData.LineMissileBounces != 0)
                {
                    Bounce();
                }
                else
                {
                    SetToRemove();
                }
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
