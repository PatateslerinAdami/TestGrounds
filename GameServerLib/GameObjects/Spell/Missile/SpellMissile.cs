using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.GameObjects.SpellNS.Missile
{
    public class SpellMissile : GameObject
    {
        // Function Vars.
        protected float _moveSpeed;
        protected float _timeSinceCreation;

        /// <summary>
        /// Information about this missile's path.
        /// </summary>
        public CastInfo CastInfo { get; protected set; }
        /// <summary>
        /// What kind of behavior this missile has.
        /// </summary>
        public virtual MissileType Type { get; protected set; } = MissileType.Target;
        /// <summary>
        /// Current unit this projectile is homing in on and moving towards. Projectile is destroyed on contact with this unit unless it has more than one target.
        /// </summary>
        public AttackableUnit TargetUnit { get; protected set; }
        /// <summary>
        /// Spell which created this projectile.
        /// </summary>
        public Spell SpellOrigin { get; protected set; }
        /// <summary>
        /// Position this projectile is moving towards (location-targeted skillshots:
        /// line/arc and circle classes). Vector2.Zero for plain targeted missiles.
        /// Projectile is destroyed once it reaches this destination.
        /// </summary>
        public Vector2 Destination { get; protected set; }
        /// <summary>
        /// Units hit by this projectile (piercing skillshots accumulate, chains carry
        /// the list across segments as their no-revisit exclusion set).
        /// </summary>
        public List<GameObject> ObjectsHit { get; }
        /// <summary>
        /// Whether or not this projectile's visuals should not be networked to clients.
        /// </summary>
        public bool IsServerOnly { get; }
        /// <summary>
        /// True if the client already received cast info for this missile via CastSpellAns or
        /// Basic_Attack_Pos. When true, the visibility-spawn flow skips the heavy
        /// MissileReplication packet because the lightweight S2C_ForceCreateMissile (sent at
        /// windup-end via Spell.CreateSpellMissile) is enough to trigger ExecuteCastFrame
        /// client-side. Set to false for chain-bounce / sub-missiles that have no cast
        /// announcement (BounceToNextTarget); those still need MissileReplication for the
        /// client to know about them at all. Default true; only Bounce/sub paths flip it.
        /// </summary>
        /// <summary>
        /// Script-side override for SpellData.MissileTargetHeightAugment (see
        /// MissileParameters.OverrideHeightAugment). Null = use the JSON value.
        /// </summary>
        public float? HeightAugmentOverride { get; set; }

        public bool HasClientCastInfo { get; set; } = true;

        /// <summary>
        /// Set by subclasses BEFORE an on-arrival removal: Riot sends DestroyClientMissile
        /// ONLY when the server-side removal does NOT coincide with the client-inferable
        /// end (arrival at the target, or MissileLifetime expiry — see the CastType-4 rule
        /// above). Replay-proven on chain segments: 0/967 KatarinaQMis, 0/320 FiddleE,
        /// 0/237 NamiW, 0/318 SpellFlux, 0/327 SivirWAttackBounce carry a destroy; the
        /// exceptions are off-schedule removals (target died mid-flight, Brand R's
        /// inter-bounce-delay lifecycle 57/57, Diana orb early detonations 92/117).
        /// </summary>
        protected bool SuppressDestroyNotify { get; set; }

        public override bool IsAffectedByFoW => true;
        public override bool SpawnShouldBeHidden => true;

        public SpellMissile(
            Game game,
            int collisionRadius,
            Spell originSpell,
            CastInfo castInfo,
            float moveSpeed,
            uint netId = 0,
            bool serverOnly = false
        ) : base(game, new Vector2(castInfo.SpellCastLaunchPosition.X, castInfo.SpellCastLaunchPosition.Z), collisionRadius, 0, 0, netId)
        {
            _moveSpeed = moveSpeed;
            _timeSinceCreation = 0.0f;

            SpellOrigin = originSpell;

            CastInfo = castInfo;

            // TODO: Implement full support for multiple targets.
            if (castInfo.Targets.Count > 0 && castInfo.Targets[0].Unit != null)
            {
                TargetUnit = castInfo.Targets[0].Unit;

                // Non-zero spawn Direction toward the target (2026-06-07). The spawn
                // MissileReplication carries Velocity = Direction × Speed and the client derives
                // the missile's initial orientation from it; (0,0,0) leaves a targeted missile
                // mis-oriented until its first homing step. At oblique firing angles that shows
                // as the missile visually launching along a wrong heading and then snapping onto
                // the homing line (reported on the nexus turret's AA "path changes at certain
                // angles"). Location-targeted classes (line/circle) already set this in
                // InitializeDestination for the same reason; targeted missiles skipped it.
                var launch2D = new Vector2(castInfo.SpellCastLaunchPosition.X, castInfo.SpellCastLaunchPosition.Z);
                var toTarget = TargetUnit.Position - launch2D;
                if (toTarget.LengthSquared() > float.Epsilon)
                {
                    var d = Vector2.Normalize(toTarget);
                    Direction = new Vector3(d.X, 0, d.Y);
                }
            }

            VisionRadius = SpellOrigin.SpellData.MissilePerceptionBubbleRadius;

            Team = CastInfo.Owner.Team;

            IsServerOnly = serverOnly;

            ObjectsHit = new List<GameObject>();
        }

        /// <summary>
        /// Shared skillshot destination setup for location-targeted missile classes
        /// (line/arc and circle — both derive directly from SpellMissile, mirroring the
        /// flat S4 hierarchy): aims from the launch position toward TargetPositionEnd,
        /// clamps to the spell's current cast range, honors script end-position
        /// overrides and falls back to the owner's facing when the cast direction
        /// degenerates.
        /// </summary>
        protected void InitializeDestination(Vector2 overrideEndPos)
        {
            // Location-targeted classes: a unit in Targets[0] is kept only as a
            // homing/orbit anchor by the subclasses that use it.
            if (CastInfo.Targets.Count <= 0 || CastInfo.Targets[0].Unit == null)
            {
                TargetUnit = null;
            }

            Position = new Vector2(CastInfo.SpellCastLaunchPosition.X, CastInfo.SpellCastLaunchPosition.Z);

            var goingTo = new Vector2(CastInfo.TargetPositionEnd.X, CastInfo.TargetPositionEnd.Z) - Position;
            var dirTemp = Vector2.Normalize(goingTo);
            var endPos = Position + (dirTemp * SpellOrigin.GetCurrentCastRange());

            // usually doesn't happen
            if (float.IsNaN(dirTemp.X) || float.IsNaN(dirTemp.Y))
            {
                if (float.IsNaN(CastInfo.Owner.Direction.X) || float.IsNaN(CastInfo.Owner.Direction.Y))
                {
                    dirTemp = new Vector2(1, 0);
                }
                else
                {
                    dirTemp = new Vector2(CastInfo.Owner.Direction.X, CastInfo.Owner.Direction.Z);
                }

                endPos = Position + (dirTemp * SpellOrigin.GetCurrentCastRange());
                CastInfo.TargetPositionEnd = new Vector3(endPos.X, 0, endPos.Y);
            }

            if (overrideEndPos != default)
            {
                endPos = overrideEndPos;
                var overrideDir = endPos - Position;
                if (overrideDir.LengthSquared() > float.Epsilon)
                {
                    dirTemp = Vector2.Normalize(overrideDir);
                }
            }

            Destination = endPos;

            // Direction MUST be non-zero at spawn: the spawn MissileReplication carries
            // Velocity = Direction × Speed — (0,0,0) gives the client a stationary missile
            // with broken oriented emitters (only some sub-particles render). Orbit
            // missiles override this with the tangent direction afterwards.
            // (Replay-verified fix, see memory: spell_missile_direction_at_spawn.)
            Direction = new Vector3(dirTemp.X, 0, dirTemp.Y);
        }

        /// <summary>
        /// Whether or not this projectile has a destination; if it is a valid skillshot.
        /// </summary>
        public bool HasDestination()
        {
            return Destination != Vector2.Zero && Destination.X != float.NaN && Destination.Y != float.NaN;
        }

        // Override base GetPosition3D (which returns ground level) to include the spell's
        // MissileTargetHeightAugment. Missile visuals fly at bow level (= ground + augment),
        // not at terrain level. Mirrors Averdrian's SpellMissile.GetPosition3D.
        public override Vector3 GetPosition3D()
        {
            float baseHeight = _game.Map.NavigationGrid.GetHeightAtLocation(Position);
            float augment = SpellOrigin?.SpellData?.MissileTargetHeightAugment ?? 0f;
            return new Vector3(Position.X, baseHeight + augment, Position.Y);
        }

        public override void Update(float diff)
        {
            // Don't move or re-collide a missile already marked for removal this tick (the
            // global collision phase may have set it just before ObjectManager.Update ran).
            if (IsToRemove())
            {
                return;
            }

            if (HasTarget() && !TargetUnit.IsDead && TargetUnit.Status.HasFlag(StatusFlags.Targetable))
            {
                _timeSinceCreation += diff;
                UpdateTimedSpeedChange();
                var positionBeforeMove = Position;
                Move(diff);
                CheckSweptCollision(positionBeforeMove);
                PublishOnSpellMissileUpdate(diff, positionBeforeMove);
            }
            else
            {
                // Destroy any missiles which are targeting an untargetable unit.
                // TODO: Verify if this should apply to SpellSector.
                //Direction = new Vector3();
                SetToRemove();
            }
        }

        public override void OnCollision(GameObject collider, bool isTerrain = false)
        {
            if (IsToRemove() || (TargetUnit != null && collider != TargetUnit))
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
        }

        /// <summary>
        /// Swept (continuous) collision check over the segment the missile traversed THIS
        /// tick. Fixes fast-missile tunnelling/overshoot: the global CollisionHandler runs in
        /// Map.Update BEFORE missiles move in ObjectManager.Update, so its point-in-circle
        /// test always lags the missile by a full tick — at Jinx W's 3300 speed that is ~110u
        /// (30Hz), more than the combined hit radius, so the hit registers (and the destroy
        /// packet goes out) only after the missile has visibly flown through the target.
        /// Running the check here, right after Move, registers the hit in the SAME tick along
        /// the actual path. Routes through OnCollision so each class's hit/dedup/budget logic
        /// is reused; the lagged global pass becomes a harmless backstop (ObjectsHit / target
        /// / IsToRemove guards make a double call a no-op).
        /// </summary>
        protected void CheckSweptCollision(Vector2 from)
        {
            if (IsToRemove())
            {
                return;
            }

            var to = Position;
            var seg = to - from;
            var segLenSq = seg.LengthSquared();

            // A bounding circle over the segment, padded for the largest plausible target
            // radius, gathers candidate units; the precise segment-distance test below
            // decides actual contact.
            const float maxUnitRadius = 200.0f;
            var mid = (from + to) * 0.5f;
            var queryRadius = (MathF.Sqrt(segLenSq) * 0.5f) + CollisionRadius + maxUnitRadius;

            var candidates = _game.Map.CollisionHandler.GetNearestObjects(new System.Activities.Presentation.View.Circle(mid, queryRadius));
            for (var i = 0; i < candidates.Count; i++)
            {
                if (IsToRemove())
                {
                    return;
                }

                if (candidates[i] is not AttackableUnit unit || unit.IsToRemove())
                {
                    continue;
                }

                // Closest distance from the unit centre to the traversed segment.
                float t = 0f;
                if (segLenSq > float.Epsilon)
                {
                    t = Math.Clamp(Vector2.Dot(unit.Position - from, seg) / segLenSq, 0f, 1f);
                }
                var closest = from + seg * t;
                var hitRange = CollisionRadius + unit.CollisionRadius;

                if (Vector2.DistanceSquared(unit.Position, closest) <= hitRange * hitRange)
                {
                    OnCollision(unit);
                }
            }
        }

        /// <summary>
        /// Gets the server-side speed that this Projectile moves at in units/sec.
        /// </summary>
        /// <returns>Units travelled per second.</returns>
        public float GetSpeed()
        {
            return _moveSpeed;
        }
        
        //TODO: Find out if this causes issues with replication?
        /// <summary>
        ///     Sets the server-side speed that this Projectile moves at in units/sec.
        /// </summary>
        public void SetSpeed(float speed) { _moveSpeed = speed; }

        /// <summary>
        /// Gets the time since this projectile was created.
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// Number of units hit so far. Scripting convenience: chain missiles report their
        /// chain-wide hit counter (carried across bounce segments — Nami W's per-bounce
        /// heal/damage decay keys off this), piercing circle/line missiles report how many
        /// units they passed through, plain targeted missiles 0/1.
        /// </summary>
        public virtual int HitCount => 0;

        /// <summary>
        /// Scheduled one-shot speed change (wire: MissileReplication.TimedSpeedDelta /
        /// TimedSpeedDeltaTime; client mirror: SpellMissileClient mTimedSpeedChange — the
        /// client adds the delta to its speed once the time elapses). Replay-verified on
        /// Jinx R: spawn packet carries Speed=1700, TimedSpeedDelta=+500,
        /// TimedSpeedDeltaTime=0.75 (rocket boosts to 2200 after 0.75s); vision-acquire
        /// packets after the boost carry Speed=2200 + TimedSpeedDeltaTime=FLT_MAX.
        /// </summary>
        public float TimedSpeedDelta { get; set; }
        /// <summary>Seconds after spawn at which TimedSpeedDelta is applied.</summary>
        public float TimedSpeedDeltaTime { get; set; }
        public bool TimedSpeedChangeApplied { get; private set; }

        // --- Distance-paced script updates (Riot: LuaOnMissileUpdateDistanceInterval) ---
        // Riot's server fires the spell script's OnMissileUpdate every N units of travel
        // (JSON field; ~105 spells use it: 50-100u for proximity/retarget logic like Ahri
        // FoxFire, 1350u as Jinx R's one-shot boost trigger). interval <= 0 (the 1305
        // default spells) keeps the legacy per-tick publish. Mirrors the
        // ChargeUpdateInterval pacing pattern used for channel ticks.
        private float _distanceSinceScriptUpdate;
        private float _timeSinceScriptUpdateMs;

        /// <summary>
        /// Publishes OnSpellMissileUpdate — per-tick when the spell has no
        /// LuaOnMissileUpdateDistanceInterval, otherwise once per N units traveled.
        /// The diff passed to listeners is the accumulated ms since the last publish.
        /// Example consumer: Jinx R (Characters/Jinx/R.cs) — JinxR.json interval=1350
        /// paces its OnMissileUpdate to Riot's boost-trigger cadence.
        /// </summary>
        protected void PublishOnSpellMissileUpdate(float diff, Vector2 positionBeforeMove)
        {
            float interval = SpellOrigin?.SpellData.LuaOnMissileUpdateDistanceInterval ?? 0f;
            if (interval <= 0f)
            {
                API.ApiEventManager.OnSpellMissileUpdate.Publish(this, diff);
                return;
            }

            _timeSinceScriptUpdateMs += diff;
            _distanceSinceScriptUpdate += Vector2.Distance(positionBeforeMove, Position);
            if (_distanceSinceScriptUpdate >= interval)
            {
                // Subtract instead of zeroing so the cadence stays distance-stable across
                // ticks of varying length.
                _distanceSinceScriptUpdate -= interval;
                API.ApiEventManager.OnSpellMissileUpdate.Publish(this, _timeSinceScriptUpdateMs);
                _timeSinceScriptUpdateMs = 0f;
            }
        }

        protected void UpdateTimedSpeedChange()
        {
            if (!TimedSpeedChangeApplied && TimedSpeedDelta != 0f
                && _timeSinceCreation / 1000.0f >= TimedSpeedDeltaTime)
            {
                SetSpeed(GetSpeed() + TimedSpeedDelta);
                TimedSpeedChangeApplied = true;
            }
        }

        public float GetTimeSinceCreation()
        {
            return _timeSinceCreation;
        }

        /// <summary>
        /// Moves this projectile to either its target unit, or its destination, and updates its coordinates along the way.
        /// </summary>
        /// <param name="diff">The amount of milliseconds the AI is supposed to move</param>
        public virtual void Move(float diff)
        {
            // current position
            var cur = Position;

            var next = GetTargetPosition();

            // 3D direction is kept for replication packets only (the client wants the
            // height-aware vector). Movement itself MUST step in 2D — stepping with the
            // 3D-normalized direction shortens the XZ step whenever Direction.Y != 0
            // (|Direction.XZ| < 1), so the missile flies below its nominal speed on any
            // terrain height difference AND asymptotically crawls into the target at the
            // end (the old hit check additionally required EXACT float equality with the
            // target position). Compounded per chain segment, this made Kata Q bounces
            // visibly slower than their 1200 data speed. Same fix as SpellLineMissile.Move.
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

            // Reached (or would step past) the target this tick: snap onto it and apply
            // the hit instead of relying on exact float equality.
            if (deltaMovement >= dist || dist <= MOVEMENT_EPSILON)
            {
                Position = next;
                if (TargetUnit != null)
                {
                    CheckFlagsForUnit(TargetUnit);
                }
                return;
            }

            var dir2D = (next - cur) / dist;
            Position = new Vector2(cur.X + dir2D.X * deltaMovement, cur.Y + dir2D.Y * deltaMovement);
        }

        public virtual void CheckFlagsForUnit(AttackableUnit unit)
        {
            if (unit == null || !HasTarget() || !SpellOrigin.SpellData.IsValidTarget(CastInfo.Owner, unit) || TargetUnit != unit)
            {
                return;
            }

            // Targeted Spell (including auto attack spells)
            if (SpellOrigin != null)
            {
                SpellOrigin.ApplyEffects(TargetUnit, this);
            }

            if (CastInfo.Owner is ObjAIBase ai && SpellOrigin.CastInfo.IsAutoAttack)
            {
                ai.AutoAttackHit(TargetUnit, CastInfo.Targets.Count > 0 ? CastInfo.Targets[0].HitResult : (HitResult?)null);
            }

            SetToRemove();
        }

        public override void SetToRemove()
        {
            if (!IsToRemove())
            {
                API.ApiEventManager.OnSpellMissileEnd.Publish(this);

                base.SetToRemove();

                // Natural MissileLifetime expiry needs NO destroy packet — but ONLY for
                // CastType-4 (circle/orbit) spells: of the four client missile classes,
                // exclusively SpellCircleMissileClient consumes mMissileLifetime
                // (mEndLifetime = now + lifetime - TimeFromCreation, S4
                // SpellCircleMissileClient.cpp:187-200). Replay-proven on Diana W
                // (426a49ca + enemy-POV d667082f: 40 vision-verified expiry orbs, zero
                // 0x5A; destroys only accompany EARLY detonation). CastType-3 spells
                // also carry lifetime values in their JSONs (NamiQDummyMissile 0.667s,
                // YasuoWMovingWallMis* 3.75s) but their client class (line) does NOT
                // self-expire — those MUST keep the destroy packet or they orphan.
                // 50ms epsilon covers tick granularity of the timed removal.
                float clientLifetime = SpellOrigin?.SpellData.MissileLifetime ?? 0f;
                bool clientSelfExpired = SpellOrigin?.SpellData.CastType == 4
                    && clientLifetime > 0f
                    && GetTimeSinceCreation() / 1000f >= clientLifetime - 0.05f;
                if (!clientSelfExpired && !SuppressDestroyNotify)
                {
                    _game.PacketNotifier.NotifyDestroyClientMissile(this);
                }
            }
        }

        /// <summary>
        /// Whether or not this projectile has a target unit or a destination; if it is a valid projectile.
        /// </summary>
        /// <returns>True/False.</returns>
        public bool HasTarget()
        {
            return TargetUnit != null;
        }

        /// <summary>
        /// Gets the position of this projectile's target (unit or destination).
        /// </summary>
        /// <returns>Vector2 position of target. Vector2(float.NaN, float.NaN) if projectile has no target.</returns>
        public Vector2 GetTargetPosition()
        {
            if (TargetUnit != null)
            {
                return TargetUnit.Position;
            }

            return Vector2.Zero;
        }
    }
}
