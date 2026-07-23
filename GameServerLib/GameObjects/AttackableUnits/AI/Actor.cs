using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Content.Navigation;
using LeagueSandbox.GameServer.Logging;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    /// <summary>
    /// Port of Riot's <c>Actor_Common</c> (<c>Game/LoL/AI/Actor/Actor.cpp</c>, <c>: ActorInterface</c>) —
    /// the composed movement / collision / pathing engine object. In Riot it is owned by value inside
    /// <c>AIManager_Common::AI_actor</c> (offset 0x4c) and reached from a unit via
    /// <c>obj_AI_Base::PTR_AIManager-&gt;AI_actor</c>; here it is owned by <see cref="AIManager"/>, reached
    /// via <see cref="ObjAIBase.AIManager"/>.
    ///
    /// <para><b>STAGE 0 (scaffold).</b> This holds only the owner back-reference. All pathing / collision /
    /// movement / stuck-recovery / force-move logic still lives on <see cref="AttackableUnit"/> and migrates
    /// here stage by stage — see <c>docs/ACTOR_CLASS_EXTRACTION_PLAN.md</c> and memory
    /// <c>reference_actor_class_ownership_architecture</c>. Introducing this class is a pure addition with
    /// zero behavior change.</para>
    /// </summary>
    public class Actor
    {
        /// <summary>The <see cref="AIManager"/> that owns this actor (Riot: the manager holding
        /// <c>AI_actor</c> by value).</summary>
        public AIManager Manager { get; }

        /// <summary>
        /// The unit this actor drives (Riot: the <c>obj_AI_Base</c> whose <c>PTR_AIManager</c> owns it).
        /// The actor reads its radius data, team, and <c>CanMove</c> from the owner and — once movement
        /// integration migrates here (Stage 5) — writes the mirrored <c>Position</c> back onto it.
        /// </summary>
        public ObjAIBase Owner { get; }

        public Actor(AIManager manager)
        {
            Manager = manager;
            Owner = manager.Owner;

            // Seed the path with a single point at the owner's spawn position (Riot: the actor's m_Path
            // starts inactive at m_Position). Replaces the old AttackableUnit-ctor init, which now runs
            // before this actor exists. CurrentWaypointKey==1 == IsPathEnded (a single-point stationary path).
            Waypoints = NavigationPath.OfSingle(Owner.Position);
            CurrentWaypointKey = 1;
        }

        // ---- Path / waypoint storage (Stage 2) — Riot Actor_Common::m_Path (NavigationPath @0x114) ----

        /// <summary>Waypoints that make up the path this unit is walking (Riot <c>Actor_Common::m_Path</c>).</summary>
        public NavigationPath Waypoints { get; set; }

        /// <summary>Index of the waypoint the unit is currently moving toward.</summary>
        public int CurrentWaypointKey { get; set; }

        /// <summary>The waypoint currently being walked toward.</summary>
        public Vector2 CurrentWaypoint => Waypoints[CurrentWaypointKey];

        /// <summary>Whether a distinct "true end" goal (separate from the possibly-clamped path end) is set.</summary>
        public bool PathHasTrueEnd { get; set; }

        /// <summary>The stored true-end goal position (valid only while <see cref="PathHasTrueEnd"/>).</summary>
        public Vector2 PathTrueEnd { get; set; }

        /// <summary>Whether the unit has reached the last waypoint in its path.</summary>
        public bool IsPathEnded() => CurrentWaypointKey >= Waypoints.Count;

        /// <summary>Collapses the path to a single stationary point at the owner's position (stop).</summary>
        public void ResetWaypoints()
        {
            Waypoints = NavigationPath.OfSingle(Owner.Position);
            CurrentWaypointKey = 1;
            PathHasTrueEnd = false;
        }

        /// <summary>True iff <paramref name="location"/> is the current true-end goal.</summary>
        public bool PathTrueEndIs(Vector2 location) => PathHasTrueEnd && PathTrueEnd == location;

        // ---- Radius (Stage 1) — Riot Actor_Common::GetRadius / GetHardRadius / GetSoftRadius ----

        /// <summary>
        /// The movement/pathing-collision radius (Riot <c>Actor_Common::GetRadius()</c> = <c>mRadius</c>).
        /// Riot's actor IS the nav-mesh actor — <c>SetRadius</c> re-registers it in the NavMesh — so
        /// <c>mRadius</c> is the PATHFINDING footprint, fed from
        /// <c>CharacterRecord.pathfindingCollisionRadius</c> (≈35.74), NOT <c>gameplayCollisionRadius</c>.
        /// The owner's <see cref="GameObject.CollisionRadius"/> holds GameplayCollisionRadius (spell/ability
        /// collision) and must NOT drive movement collision: every actor-collision term
        /// (<see cref="GetHardRadius"/>/<see cref="GetSoftRadius"/>, the neighbour radius, group/pair radii,
        /// thresholds, the broadphase) reads this instead (resolves the old D3/D4 "verify mRadius" item,
        /// 2026-06-21).
        /// </summary>
        public float ActorRadius => Owner.PathfindingRadius;

        /// <summary>
        /// Self hard-collision radius (Riot <c>Actor_Common::GetHardRadius</c>, Actor.cpp:2743-2755). The
        /// collision response is ASYMMETRIC: the SELF term uses this type-scaled radius, the NEIGHBOR term
        /// always uses full <see cref="ActorRadius"/> (the neighbour's PathfindingRadius). The gating flag
        /// <c>mUseSlowerButMoreAccurateSearch == !UsesFastPath</c> (same flag drives the NavGrid travelFactor
        /// branch). Minions &amp; non-AI default (fast): hard = <c>r</c>, soft = <c>2r</c> — waves engage the
        /// full body. Champions/pets (slower-accurate): hard = <c>0.2r</c>, soft = <c>0.3r</c> — heroes slip
        /// through crowds. Used for SELF only. (Polarity was inverted until the F1 fix 2026-07-19,
        /// docs/PATHING_AUDIT_2026_07_19.md — minions collided at 0.2r.)
        /// </summary>
        public float GetHardRadius() => Owner.UsesFastPath ? ActorRadius : ActorRadius * 0.2f;

        /// <summary>Self soft-avoidance radius (Riot <c>Actor_Common::GetSoftRadius</c>). Fast-path units:
        /// <c>2r</c>; slower-accurate: <c>0.3r</c>. See <see cref="GetHardRadius"/>.</summary>
        public float GetSoftRadius() => Owner.UsesFastPath ? ActorRadius * 2f : ActorRadius * 0.3f;

        // --- Stage 3 unit/base bridges (shadow accessors) — let the ported collision methods below
        // reference owner/base state by its ORIGINAL bare name so the bodies stay verbatim. The single
        // Position WRITE goes through `Owner.Position = ...` directly. These collapse as the backing state
        // migrates onto the Actor (Stages 4-5). ---
        private Game _game => Owner.Game;
        private Vector2 Position { get => Owner.Position; set => Owner.Position = value; }
        private uint NetId => Owner.NetId;
        private TeamId Team => Owner.Team;
        private StatusFlags Status => Owner.Status;
        private float PathfindingRadius => Owner.PathfindingRadius;
        private float GetMoveSpeed() => Owner.GetMoveSpeed();
        // Actor-owned collision output (Stage 5c): body-routing trigger + contact-keepalive flag. Set by
        // RunCollisionResponse, consumed by Move / UpdateStuckRecovery — all on the Actor.
        private bool _inHardCollision;
        private bool _inBodyContact;
        // Temp-ghost escape counter (Riot Actor_Common::mGettingOutOfCollisionGhosted, Stage 5c): past
        // the threshold the unit's pathing queries ignore actors so a wedged unit can escape. Read
        // externally (pathing predicates) through the unit's IsTemporarilyGhosted forwarding shim.
        private int _stuckGhostFrames;
        private int TempGhostThreshold => Owner.UsesFastPath ? 45 : 15;
        public bool IsTemporarilyGhosted => _stuckGhostFrames > TempGhostThreshold;
        private Vector2 _unreplicatedDrift { get => Owner.UnreplicatedDrift; set => Owner.UnreplicatedDrift = value; }
        private float _traveledSinceLastSync { get => Owner.TraveledSinceLastSync; set => Owner.TraveledSinceLastSync = value; }
        private float _timeSinceLastSync { get => Owner.TimeSinceLastSync; set => Owner.TimeSinceLastSync = value; }
        private Vector2 _smoothedSeparationPush = Vector2.Zero;  // Stage 5a: Move-exclusive, moved here

        /// <summary>
        /// Collects the hard-collider set ahead of a MOVING unit and reduces it to a single group
        /// circle. Shared by the per-tick waypoint steer (S4 CheckActorCollisionResponse) and the
        /// gated position-push fallback (HandleActorCollision) so both consume the EXACT same set —
        /// the steer's collision flag is what gates the push (Actor.cpp:1722). Classification mirrors
        /// the client neighbor collection (Actor.cpp:240-285): self/dead/ghosted filter, forward
        /// direction gate, 10u deep-overlap floor, and the hard-radius + [12,20] buffer threshold
        /// (SELF uses GetHardRadius, the NEIGHBOR term uses full mRadius = PathfindingRadius — the asymmetry is
        /// faithful). Fills <paramref name="barycenter"/> (plain average of collider positions) and
        /// <paramref name="groupRadius"/> (max enclosing radius); returns the collider count.
        /// </summary>
        private int CollectHardColliders(List<GameObject> nearby, Vector2 objFwd,
            out Vector2 barycenter, out float groupRadius)
        {
            const float MinColliderDistSq = 100f; // deep overlaps (<10u) are NOT colliders here;
                                                  // the gated stuck layer handles them.
            barycenter = Vector2.Zero;
            groupRadius = 0f;

            float selfHard = GetHardRadius();
            var colliders = new List<AttackableUnit>(4);
            foreach (var other in nearby)
            {
                if (other == Owner || other.IsToRemove()) continue;
                if (!(other is AttackableUnit otherUnit)) continue;
                // Buff-ghost only — the escalated temp-ghost does NOT exempt body collision in
                // 4.17 (CanCollide == !ShouldIgnoreCollisionDueToGhost; P3 2026-07-19).
                if (otherUnit.IsDead || ShouldIgnoreCollisionDueToGhost(otherUnit)) continue;
                if (Vector2.Dot(objFwd, other.Position - Position) <= 0f) continue;

                float distSq = Vector2.DistanceSquared(Position, other.Position);
                if (distSq < MinColliderDistSq) continue;

                // NEIGHBOUR term = full mRadius = the neighbour's PathfindingRadius (Actor GetRadius()),
                // NOT its gameplay CollisionRadius.
                float pairBuffer = Math.Clamp(
                    Math.Min(selfHard, otherUnit.PathfindingRadius) * 0.25f, 12f, 20f);
                float pairRadius = selfHard + otherUnit.PathfindingRadius + pairBuffer;
                if (distSq >= pairRadius * pairRadius) continue;

                colliders.Add(otherUnit);
                barycenter += other.Position;
            }
            if (colliders.Count == 0) return 0;
            barycenter /= colliders.Count;

            // Enclosing group radius: max over colliders of (dist to barycenter + their radius)
            // (S4 Actor.cpp:356-370). Their radius = mRadius = PathfindingRadius.
            foreach (var c in colliders)
            {
                float r = Vector2.Distance(c.Position, barycenter) + c.PathfindingRadius;
                if (r > groupRadius) groupRadius = r;
            }
            return colliders.Count;
        }

        /// <summary>
        /// DEFAULT per-tick collision responder for a MOVING unit — faithful port of S4
        /// <c>Actor_Common::CheckActorCollisionResponse</c> (Actor.cpp:310-414). Instead of pushing
        /// the unit's POSITION off its path (the old behaviour, now gated to the wedge case), this
        /// SHIFTS the unit's FUTURE path waypoints sideways around the collider barycenter, in place.
        /// The unit then follows the bent path normally with its Position always ON the path, so the
        /// replicated 0x61 WaypointGroup (wp0 == Position) keeps client and server in agreement — no
        /// off-path snap. Returns whether any hard collider was present (= the decomp's
        /// <c>isInCollision</c>, which gates the position-push fallback).
        ///
        /// Per the decomp the loop runs over waypoints <c>[GetNextWaypointIndex .. GetSize()-2]</c>
        /// — the FINAL destination waypoint is never shifted, and a 2-point path (the common minion
        /// path) is therefore a no-op (early return). The slide sign is recomputed every tick from
        /// <c>signTable[dot(side, m_Movement) &gt; 0]</c> with NO hysteresis (D11).
        ///
        /// Replication: this mutates the LIVE <see cref="Waypoints"/> in place but deliberately does
        /// NOT force <c>_movementUpdated</c> every tick. The bent path is re-broadcast by the EXISTING
        /// travel keepalive (<c>GetCenteredWaypoints</c> reads the live, now-bent waypoints seeded at
        /// Position) at Riot's measured cadence — 100u for minions / 57u for champions. Forcing a
        /// per-tick WaypointGroup would flood 0x61 and reintroduce the champion arc-jitter the
        /// keepalive cadence was tuned to avoid. Because Position stays ON the (bent) path, every
        /// re-anchor carries wp0 == true Position, so the periodic resend is smooth — there is no
        /// off-path drift to release as a snap (that off-path drift was exactly the old position-push
        /// bug). CLIENT-MODEL CORRECTION 2026-07-19: the former claim that "the 4.x client runs the
        /// same per-tick responder and bends identically between resends" was WRONG — moving client
        /// actors are ghosted (no collision response); the client walks the path it last RECEIVED
        /// verbatim. Between resends the client therefore follows the pre-bend geometry and only
        /// picks the bend up with the next keepalive/resync anchor — a small, bounded visual lag
        /// (sub-keepalive-window), acceptable because the drift-resync catches any >12u divergence
        /// immediately.
        /// </summary>
        private bool SteerPathAroundColliders(List<GameObject> nearby, Vector2 movementDelta)
        {
            const float Epsilon = 1e-9f; // S4 epsilon for the per-waypoint forward normalize (D21)

            if (nearby.Count == 0) return false;

            float movementMag = movementDelta.Length();
            if (movementMag <= 0.001f) return false;
            Vector2 objFwd = movementDelta / movementMag;

            int count = CollectHardColliders(nearby, objFwd, out Vector2 barycenter, out float groupRadius);
            if (count == 0) return false;

            // Nothing to bend once the next waypoint is already the final destination
            // (decomp: GetNextWaypointIndex() >= GetSize()-1 returns immediately).
            if (CurrentWaypointKey >= Waypoints.Count - 1) return true;

            float selfHard = GetHardRadius();
            float minDistanceBuffer = Math.Clamp(Math.Min(groupRadius, selfHard) * 0.25f, 12f, 20f);
            float threshold = groupRadius + selfHard + minDistanceBuffer;

            // Shift every FUTURE waypoint up to (but excluding) the final goal. The loop STOPS at
            // the first waypoint whose distance to the barycenter is >= threshold (it and the rest
            // are far enough that no bend is needed). sideVec = yAxis(0,1,0) x fwd = (fwd.z,-fwd.x).
            for (int i = CurrentWaypointKey; i < Waypoints.Count - 1; i++)
            {
                Vector2 w = Waypoints[i];
                Vector2 fwd = w - Position;
                float fwdLen = fwd.Length();
                if (fwdLen <= Epsilon) continue; // coincident waypoint: degenerate normalize, skip
                fwd /= fwdLen;
                Vector2 side = new Vector2(fwd.Y, -fwd.X);

                float d = Vector2.Distance(barycenter, w);
                if (d >= threshold) break;

                float push = MathF.Max(threshold - d, 0f);
                float sign = Vector2.Dot(side, movementDelta) > 0f ? 1f : -1f;
                float f = 2f * push * sign;
                // F7 (2026-07-19): never write a steered waypoint into unpassable terrain.
                // Riot's equivalent guard sits on the collision-modified MOVEMENT (in-collision
                // cell passability revert, Actor.cpp:739-757: a step entering an unpassable cell
                // is reverted and flags isStuck). In our position-first model the bent waypoint
                // PERSISTS on the path and is walked LATER, outside collision processing, so the
                // write itself is the right gate placement. An in-wall bend is dropped — the
                // waypoint keeps its A*-validated position; push/reroute channels still separate.
                Vector2 bent = w + side * f;
                if (_game.Map.PathingHandler.IsWalkable(bent, 0f))
                {
                    Waypoints.Replace(i, bent);
                }
            }
            return true;
        }

        private Vector2 ComputeGroupCollisionResponse(List<GameObject> nearby, Vector2 originalPos, Vector2 movementDelta, out bool hadHardColliders, out Vector2 barycenter)
        {
            const float ReflectionIndex = 5.1f;   // S4 Actor.cpp:314
            const float AngleThreshold = 0.707f;  // S4 Actor.cpp:315
            const float Epsilon = 1e-6f;          // Riot::Vector3f::kfThreshold class

            hadHardColliders = false;
            barycenter = Vector2.Zero;
            if (nearby.Count == 0) return Vector2.Zero;

            float movementMag = movementDelta.Length();
            if (movementMag <= 0.001f) return Vector2.Zero;
            Vector2 objFwd = movementDelta / movementMag;

            float selfHard = GetHardRadius();
            int count = CollectHardColliders(nearby, objFwd, out barycenter, out float groupRadius);
            if (count == 0) return Vector2.Zero;
            hadHardColliders = true;

            float minDistanceBuffer = Math.Clamp(
                Math.Min(groupRadius, selfHard) * 0.25f, 12f, 20f);
            float totalThreshold = groupRadius + selfHard + minDistanceBuffer;

            // collisionNormal from the POST-move position (client: info.NextPosition);
            // toCenter from the PRE-move position (client: m_Position).
            Vector2 rel = barycenter - Position;
            float relLenSq = rel.LengthSquared();
            Vector2 collisionNormal = relLenSq <= Epsilon ? objFwd : rel / MathF.Sqrt(relLenSq);
            float pushDistance = Math.Max(totalThreshold - MathF.Sqrt(relLenSq), 0f);
            if (pushDistance <= 0f) return Vector2.Zero;

            Vector2 toCenter = barycenter - originalPos;
            Vector2 side = new Vector2(objFwd.Y, -objFwd.X); // client's (fwd.z, -fwd.x) perpendicular

            float proj = Vector2.Dot(objFwd, collisionNormal);
            float projAbs = Math.Abs(proj);

            if (projAbs <= Epsilon)
            {
                // Perpendicular degenerate cases (S4 Actor.cpp:407-432).
                float sideDotAbs = Math.Abs(Vector2.Dot(side, collisionNormal));
                if (sideDotAbs <= Epsilon)
                {
                    return collisionNormal * (-ReflectionIndex * pushDistance);
                }
                float slide = pushDistance / sideDotAbs;
                float slideFactor = slide <= totalThreshold ? Math.Max(0.01f, slide) : totalThreshold;
                float slideSign = Vector2.Dot(toCenter, side) > 0f ? 1f : -1f;
                return side * (slideFactor * ReflectionIndex * slideSign);
            }

            float factor = pushDistance / projAbs;
            float clampedFactor = factor <= totalThreshold ? Math.Max(0.01f, factor) : totalThreshold;

            if (proj <= 0f)
            {
                // Group behind the movement: accelerate forward, away from it (no 5.1).
                return objFwd * clampedFactor;
            }
            if (proj >= AngleThreshold)
            {
                // Head-on: pure side-slide with RAW pushDistance (S4 Actor.cpp:440-447).
                float sign = Vector2.Dot(toCenter, side) > 0f ? 1f : -1f;
                return side * (pushDistance * ReflectionIndex * sign);
            }
            // Glancing: reflect the movement across the group tangent (S4 Actor.cpp:448-462):
            // 2*(fwd - n*proj) - fwd, scaled by 5.1 * clampedFactor.
            Vector2 tangential = objFwd - collisionNormal * proj;
            Vector2 reflected = tangential * 2f - objFwd;
            return reflected * (ReflectionIndex * clampedFactor);
        }

        /// <summary>
        /// Per-tick collision control-flow structure for a MOVING unit — mirrors the collision tail
        /// of S4 <c>Actor_Common::Update</c> (Actor.cpp:1714-1741). Runs the default path STEER
        /// (<see cref="SteerPathAroundColliders"/> = CheckActorCollisionResponse) every tick, then —
        /// only when <c>Waypoints.Count >= MAX_NUMREPATH(4) &amp;&amp; isInCollision</c> (Actor.cpp:1722) —
        /// the gated position-push (<see cref="HandleActorCollision"/>) inside the HARDSTOPLOOPCOUNT(3)
        /// intra-tick relaxation loop: re-resolve from the (progressively separated) Position each
        /// iteration, break once out of collision, with a 2nd push pass on the final iteration
        /// (Actor.cpp:1724). Drives the temp-ghost counter and clears it on a collision-free tick
        /// (Actor.cpp:1738-1740). Returns whether any push was applied this tick.
        ///
        /// Stage B keeps Stage A's position-based application. The persistent <c>m_Movement</c>
        /// velocity model and the inline <c>forceRepath</c> → actor-aware repath land in Stage C: in
        /// the position model the natural walk is applied BEFORE collision, so the decomp's
        /// "barely moved" forceRepath gate isn't yet meaningful and the actor-aware wedge repath is
        /// handled by <see cref="UpdateStuckRecovery"/> / <see cref="TryUnstuckRepath"/> (B1).
        /// </summary>
        /// <summary>
        /// Faithful port of Riot ActorCollisionState::ShouldIgnoreCollisionDueToGhost(this, other):
        /// returns true when the two units must NOT body-collide because of ghost state. Consulted
        /// per neighbour in place of the old simple "skip if other is Ghosted" test — for normal units
        /// (no GhostProof* flags on either side) it reduces to exactly that test, so ordinary minion/
        /// champion pathing is unchanged; only ghosted wall units (Azir soldiers set Ghosted +
        /// GhostProofForEnemies) get the correct team-directional blocking.
        ///
        /// "GhostProof" (fully collidable while ghosted) is DERIVED, matching the decomp
        /// (Actor_Common::IsGhostProof == GhostProofForAllies &amp;&amp; GhostProofForEnemies).
        /// </summary>
        private bool ShouldIgnoreCollisionDueToGhost(AttackableUnit other)
        {
            bool aGhosted = Status.HasFlag(StatusFlags.Ghosted);
            bool oGhosted = other.Status.HasFlag(StatusFlags.Ghosted);

            // Neither ghosted → normal collision (the common case, and the old behaviour).
            if (!aGhosted && !oGhosted)
            {
                return false;
            }

            bool aGpAllies = Status.HasFlag(StatusFlags.GhostProofForAllies);
            bool aGpEnemies = Status.HasFlag(StatusFlags.GhostProofForEnemies);
            bool oGpAllies = other.Status.HasFlag(StatusFlags.GhostProofForAllies);
            bool oGpEnemies = other.Status.HasFlag(StatusFlags.GhostProofForEnemies);
            bool aGhostProof = aGpAllies && aGpEnemies;
            bool oGhostProof = oGpAllies && oGpEnemies;

            // L1 gate (mirrors the decomp's control flow): the actor's own GhostProof cancels the
            // ignore. Reached when the actor is NOT ghosted (only other is), or when BOTH are ghosted.
            if (aGhosted)
            {
                if (oGhostProof)
                {
                    return false;
                }
                if (oGhosted && aGhostProof)
                {
                    return false;
                }
                // actor ghosted, other not ghosted → skip L1, fall to the team check.
            }
            else if (aGhostProof) // !aGhosted && oGhosted
            {
                return false;
            }

            // Team-directional gate: a ghosted unit still collides with the side it is not proofed
            // against (allies pass, enemies blocked → GhostProofForEnemies; and vice-versa).
            if (Team == other.Team)
            {
                if (aGpAllies || oGpAllies)
                {
                    return false;
                }
                return true;
            }

            if (aGpEnemies)
            {
                return false;
            }
            return !oGpEnemies;
        }

        // Entry point from the still-on-unit Move() (via the AttackableUnit forwarding shim).
        public bool RunCollisionResponse(List<GameObject> nearby, Vector2 originalPos, Vector2 originalDelta, float delta)
        {
            const int MaxNumRepath = 4;      // S4 MAX_NUMREPATH (Actor.cpp:163)
            const int HardStopLoopCount = 3; // S4 HARDSTOPLOOPCOUNT (Actor.cpp:1719)

            bool pushed = false;

            // DEFAULT responder: steer FUTURE waypoints around the collider group (no-op on a
            // 2-point path). Returns isInCollision (hard colliders present), which gates the push.
            bool hadHardColliders = SteerPathAroundColliders(nearby, originalDelta);

            // Feed the body-routing trigger (S4 forceRepath): "in collision" → UpdateStuckRecovery
            // reroutes actor-aware on a throttle. True even on a 2-point clash path (the steer is a
            // no-op there but still reports hard colliders) — which is exactly the clip-through case.
            _inHardCollision = hadHardColliders;

            // Body-contact WITHOUT the 10u deep-overlap floor (keepalive cadence + de-fusion
            // seed below). Also finds the nearest partner INSIDE the collection's blind floor.
            AttackableUnit fusedPartner = null;
            float fusedDistSq = float.MaxValue;
            foreach (var o in nearby)
            {
                if (o == Owner || o is not AttackableUnit au || au.IsDead
                    || ShouldIgnoreCollisionDueToGhost(au))
                {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(au.Position, Position);
                float rr = PathfindingRadius + au.PathfindingRadius;
                if (distSq < rr * rr)
                {
                    _inBodyContact = true;
                }
                if (distSq < 100f && distSq < fusedDistSq && au.MovementParameters == null)
                {
                    fusedPartner = au;
                    fusedDistSq = distSq;
                }
            }

            // DE-FUSION SEED (DOCUMENTED INVENTION, 2026-07-19, sr131 wave-stack test): the
            // collision collection's faithful 10u deep-overlap floor (distSq >= 100,
            // Actor.cpp:296) makes a FUSED pair mutually invisible to every responder. Riot
            // never reaches this state — its movement model hard-stops bodies before full
            // overlap — but our position-first walk lets compressed units pass through each
            // other to ~0u (body-blocked waves, forced spawns), and from an identical position
            // the pair can never separate again: identical reroute lines (the touching partner
            // is start-proximity-exempt in the A*), no steer response (n=2), no push (mutually
            // invisible). Riot has no equivalent code because it has no equivalent state; the
            // measured consequence is our NN-spacing floor (sr131: p25=0u vs Riot map1 p10=48u).
            // The seed ONLY breaks the symmetry: while a MOVING unit's nearest neighbour sits
            // inside the blind floor, nudge a quarter-stride per tick along a deterministic
            // axis — away from the partner, or the NetId angle when exactly co-located (same
            // convention as the degenerate barycenter case in HandleActorCollision) — until the
            // pair exits the floor and the real machinery (steer / reroutes / reservations)
            // takes over. Terrain-gated via ApplyCollisionPush; ~3-4 ticks from 0u to >10u.
            if (fusedPartner != null)
            {
                Vector2 away = Position - fusedPartner.Position;
                Vector2 dir;
                if (away.LengthSquared() < 0.01f)
                {
                    float angle = (NetId * 2.39996f) % 6.28318f;
                    dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                }
                else
                {
                    dir = Vector2.Normalize(away);
                }
                float seedMag = 0.25f * GetMoveSpeed() * (delta * 0.001f);
                pushed |= ApplyCollisionPush(dir * seedMag, false, logLabel: "defuse");
            }

            // Push gate = Riot's `m_Path.GetSize() >= MAX_NUMREPATH && isInCollision`
            // (Actor.cpp:1721): NO active separation for n=2 marchers (spacing is preserved
            // passively — spawn stagger + equal speeds); in the CLASH phase paths are n>=4
            // (in-collision actor-aware reroutes) and the damped push/avoidance stack resolves
            // compression.
            //
            // HISTORY: two invented suppressions used to sit here and are both gone —
            // "|| this is Minion" (2026-07-03, reverted 2026-07-05: removed the combat-phase
            // separation entirely, fusion ratchet) and `_pathFromBodyRouting` (removed 2026-07-19
            // with F1: the "push fights the routed path" glitching was measured with 0.2r minion
            // bodies; the decomp runs pushes and in-collision repaths concurrently with no such
            // flag). If sideways resync storms return in clumps, re-measure with COLLISION_LOG
            // (push rate/magnitude/resolution) before reinventing a gate.
            if (Waypoints.Count >= MaxNumRepath && hadHardColliders)
            {
                float speedPerTick = GetMoveSpeed() * (delta * 0.001f);

                // LOOP SEMANTICS CORRECTED 2026-07-05 (tt118 push-storm anatomy): the decomp loop
                // (Actor.cpp:1718-1730) is `responseCheck = HandleActorCollision(info); if
                // (responseCheck == 0 && loopCount >= 2) responseCheck = HandleActorCollision(info);
                // if (responseCheck) break;` — it BREAKS as soon as a response was handled
                // (nonzero = hard branch taken), i.e. AT MOST ONE applied push per tick; the loop
                // exists to RETRY THE DETECTION when nothing was handled, and the second call
                // fires only when the FIRST returned zero on the final iteration. Our old port
                // had it inverted (apply every iteration, continue WHILE in collision) → up to 4
                // chained pushes per tick (~45-60u): logged as multiple same-timestamp group
                // events, visible as the forward "dash" (net displacement +153u/1.5s, same-
                // direction chain) and the ±15u ping-pong storm (134 events/3s, net 47u) the
                // moment the minion push suppression was lifted. One clamped push per tick is
                // what makes Riot's response converge.
                //
                // KNOWN LIMITATION (F7c, 2026-07-19, deliberate): in our position-first model
                // this loop can never actually iterate — HandleActorCollision recomputes from
                // the SAME inputs (Position isn't mutated until `handled`), so the first call
                // always returns true and breaks at iteration 0. Riot's retry works because
                // each iteration re-runs CheckActorCollisionResponse (the steer) against the
                // mutated NextPosition/m_Movement state (Actor.cpp:1719-1731). Faking the retry
                // without that state model would just accumulate waypoint bends. Revisit with
                // Stage C (P0) — the single-commit integrator makes the retry meaningful.
                for (int loopCount = 0; loopCount < HardStopLoopCount; loopCount++)
                {
                    Vector2 outMovement = originalDelta;
                    bool handled = HandleActorCollision(nearby, originalPos, originalDelta,
                        speedPerTick, ref outMovement);
                    if (handled)
                    {
                        // Position already holds the natural walk (originalDelta); apply the residual.
                        pushed |= ApplyCollisionPush(outMovement - originalDelta, true);
                        break; // decomp: if (responseCheck) break;
                    }

                    if (loopCount >= 2)
                    {
                        // 2nd HandleActorCollision pass ONLY when the first returned zero on the
                        // final iteration (Actor.cpp:1723-1725).
                        handled = HandleActorCollision(nearby, originalPos, originalDelta,
                            speedPerTick, ref outMovement);
                        if (handled)
                        {
                            pushed |= ApplyCollisionPush(outMovement - originalDelta, true);
                        }
                    }
                }

            }

            // Temp-ghost counter, S1-anchored lifecycle (P3 rework 2026-07-19). The 4.17
            // increment site is unrecovered (the repathTimings block at Actor.cpp:1877-1920 is
            // FIXME-garbled; all resets + the threshold read survived), but S1
            // actor_client.cpp:5044 shows `++mGettingOutOfCollisionGhosted` firing PER TICK
            // inside the in-collision repath-pending block — NOT only on collapsed movement (the
            // former "genuine stuck <25%" gate here was our invention and made the ghost nearly
            // unreachable). Resets (4.17, recovered): not-in-collision (Actor.cpp:1739), the
            // constrained path rebuild (:1858 — mirrored in UpdateStuckRecovery's collision
            // repath), end-of-path recovery (:2100/:2184) ≈ our stationary reset, forced movement
            // ≈ our dash reset. Riot's escalating repath backoff (repathTimings table, lost)
            // meant the counter could outrun the rebuild cadence after repeated failed repaths;
            // with our flat 250ms rebuild reset the ghost fires only when the reroute channel
            // CANNOT run (CC lock, path ended in contact) — rarer than Riot's ladder, escape
            // still guaranteed by the TryUnstuckRepath escalation. Cap removed (Riot: unbounded,
            // resets do the work).
            if (hadHardColliders && Waypoints.Count > 1)
            {
                _stuckGhostFrames++;
            }
            else if (!hadHardColliders)
            {
                // Not in collision this tick (Actor.cpp:1738-1740) -> clear temp-ghost escalation.
                _stuckGhostFrames = 0;
            }

            return pushed;
        }

        /// <summary>
        /// Applies a collision-response position delta, terrain-gated by <c>IsWalkable</c> (the only
        /// surviving cell check in 4.20 — see <see cref="HandleActorCollision"/>). Returns whether it
        /// moved the unit.
        /// </summary>
        private bool ApplyCollisionPush(Vector2 push, bool isInCollision, string logLabel = null)
        {
            if (push.LengthSquared() <= 0.0001f) return false;
            Vector2 candidate = Position + push;
            if (!_game.Map.NavigationGrid.IsWalkable(candidate, 0f)) return false;
            Owner.Position = candidate;
            _unreplicatedDrift += push;
            if (CollisionLogger.Enabled)
            {
                CollisionLogger.Log(_game.GameTime, NetId, logLabel ?? (isInCollision ? "group" : "avoid"), push.Length(), 0f, Position);
            }
            return true;
        }

        /// <summary>
        /// Faithful unified port of S4 <c>Actor_Common::HandleActorCollision</c> (Actor.cpp:420-984):
        /// the gated position-push responder, composed as ONE control-flow structure that modifies a
        /// SINGLE movement vector (the decomp's <c>outMov</c>) instead of our former scattered
        /// group-push + separate unclamped stuck delta. Mutates <paramref name="outMovement"/> in
        /// place (starts at <paramref name="originalDelta"/>) and returns <c>isInCollision</c>
        /// (= hard colliders present; the decomp only sets it true in the hard branch, Actor.cpp:572).
        ///
        /// Structure (decomp): hard branch (group reflection/slide, Actor.cpp:447-570) — OR — soft
        /// avoidance branch (Actor.cpp:814-976) when no hard colliders; then, in the hard branch only,
        /// the stuck-with-repulse push folded into <c>outMov</c> (Actor.cpp:578-599) BEFORE a SINGLE
        /// length clamp (Actor.cpp:607-619). The stuck push reuses the group barycenter (decomp
        /// reuses baryCenter, line 586) and is min(95, speed*1.5)·normalize(pos−bary) — the /sepDist
        /// cancels because it multiplies the full (pos−bary) vector whose length IS sepDist (B2).
        /// <paramref name="speedPerTick"/> = info.max_distance = moveSpeed·dt.
        ///
        /// Terrain (Actor.cpp:632-757): the cell-border slide + per-cell <c>mActorList</c> occupancy
        /// hard-stop are BOTH gated by <c>s_CanActorsSlideIntoOccupiedGridSquares</c>, which is =1 in
        /// 4.20 (disabled) — so only the <c>IsPassable</c> revert survives. The caller's
        /// <c>IsWalkable</c> gate on the applied position already does exactly that, so we keep it
        /// there rather than duplicating the cell math here.
        /// </summary>
        private bool HandleActorCollision(List<GameObject> nearby, Vector2 originalPos, Vector2 originalDelta,
            float speedPerTick, ref Vector2 outMovement)
        {
            const float StuckGateRatio = 1.5f; // s_MinionMaxCollisionAvoidanceRatio (Actor.cpp:469/578)
            const float StuckHardCap = 95.0f;  // per-tick distance cap (Actor.cpp:592) — see B2

            outMovement = originalDelta;

            Vector2 response = ComputeGroupCollisionResponse(nearby, originalPos, originalDelta,
                out bool hadHardColliders, out Vector2 barycenter);

            if (hadHardColliders)
            {
                outMovement = originalDelta + response;

                // Fold the stuck-with-repulse push into outMov BEFORE the clamp (Actor.cpp:578-599):
                // if the post-response movement collapsed to <= 1.5*speed, add an escape push away
                // from the SAME group barycenter, magnitude min(95, speed*1.5). The decomp's /sepDist
                // multiplies the full (pos-bary) vector so it cancels to a unit vector × magnitude.
                //
                // ON THE OMITTED RAMP TERM (F4 verdict 2026-07-19, docs/PATHING_AUDIT_2026_07_19.md):
                // the full 4.17 formula is min(95, min(speed*1.5, s_ExtraSeparationSpeed *
                // (stuckSecs/s_timeBetweenPathCorrections + 1)^2 * dt)) — but
                // s_timeBetweenPathCorrections is a REAL, SEPARATE static (dsym-verified, distinct
                // from s_TimeBetweenRepathsInSeconds) that has NO writer anywhere in the recovered
                // 4.17 source (not in ReadConfigVariables; grep-clean). C++ zero-init ⇒ 0 ⇒ the
                // ratio term is +inf and the inner min degenerates to speed*1.5 — i.e. the live 4.x
                // push IS min(95, speed*1.5), exactly this code. S1 had a live ramp (denominator
                // s_TimeBetweenRepathsInSeconds, S1 actor_client.cpp:2112, ExtraSeparationSpeed=50);
                // 4.x refactored it onto the dead static — an N1-class parameter fossil. Do NOT
                // "restore" the ramp without new evidence: it would be an S1 backport, not a port.
                float gate = StuckGateRatio * speedPerTick;
                if (outMovement.LengthSquared() <= gate * gate)
                {
                    Vector2 away = Position - barycenter;
                    Vector2 dir;
                    if (away.LengthSquared() < 0.01f)
                    {
                        // Perfectly symmetrical crowd (centroid on the unit): deterministic NetId
                        // fallback so the same setup doesn't jitter frame-to-frame.
                        float angle = (NetId * 2.39996f) % 6.28318f;
                        dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    }
                    else
                    {
                        dir = Vector2.Normalize(away);
                    }
                    float pushMag = Math.Min(StuckHardCap, speedPerTick * StuckGateRatio);
                    outMovement += dir * pushMag;
                }

                // SINGLE hard clamp on the combined (response + stuck) movement (Actor.cpp:607-619),
                // referenced against the original intended movement so the escape can still exceed the
                // (near-zero) post-collision movement.
                outMovement = ClampCollisionMovement(outMovement, originalDelta, 0.75f, 0.625f, 1.375f);
                return true;
            }

            // Soft avoidance branch (no hard colliders): pre-contact veer + its own tight clamp. With
            // the standard gate (called only when the steer found hard colliders) this is normally
            // unreachable — kept for structural fidelity with HandleActorCollision's two branches.
            Vector2 avoid = ComputeAvoidanceResponse(nearby, originalPos, originalDelta);
            if (avoid.LengthSquared() <= 0.0001f)
            {
                outMovement = originalDelta;
                return false;
            }
            outMovement = ClampCollisionMovement(originalDelta + avoid, originalDelta, 0.25f, 0.875f, 1.125f);
            return false;
        }

        /// <summary>
        /// S4 movement-length clamp after a collision/avoidance response: when the modified
        /// movement deviates by more than <paramref name="trigger"/> (in squared length) from
        /// the original, rescale it into [lo, hi] x |original|². Bounds the responses to a
        /// modest per-tick magnitude while keeping their DIRECTION — the response is a steering
        /// change, not a teleport. For a zero original movement this zeroes the response (the
        /// client's responses only exist for moving actors). Two parameter sets in the client:
        /// hard collision (Actor.cpp:498-510) trigger 0.75, [0.625, 1.375]; avoidance
        /// (Actor.cpp:850-859) trigger 0.25, [0.875, 1.125].
        /// </summary>
        private static Vector2 ClampCollisionMovement(Vector2 newMovement, Vector2 originalMovement,
            float trigger, float lo, float hi)
        {
            float origLenSq = originalMovement.LengthSquared();
            float newLenSq = newMovement.LengthSquared();
            if (Math.Abs(newLenSq - origLenSq) > trigger * origLenSq && newLenSq > 1e-6f)
            {
                float targetLenSq = newLenSq > hi * origLenSq
                    ? hi * origLenSq
                    : Math.Max(lo * origLenSq, newLenSq);
                return newMovement * MathF.Sqrt(targetLenSq / newLenSq);
            }
            return newMovement;
        }

        /// <summary>
        /// Client soft-radius avoidance for MOVING units (S4 Actor.cpp:705-870) — the
        /// pre-contact layer. Runs ONLY when there are no hard colliders. Actors inside the
        /// soft band (otherR + 2*selfR; GetSoftRadius fast-mode — slow-mode actors unverified,
        /// see audit memory) with relative movement form a group (barycenter + enclosing radius
        /// + average velocity), and the unit veers SIDEWAYS before contact:
        ///   * group behind the movement -> nothing
        ///   * same-direction traffic (parallelness &gt; 0.707) and group is as fast -> nothing
        ///     (follow, don't overtake); if we're faster -> overtake side by the group heading
        ///   * head-on / crossing -> side picked by which side the group center is on
        /// Magnitude = min(0.4 x |movement|, pushDistance / |dot(normal, side)|) — a gentle veer,
        /// further bounded by the tight avoidance length clamp (0.25 / [0.875, 1.125]).
        /// </summary>
        private Vector2 ComputeAvoidanceResponse(List<GameObject> nearby, Vector2 originalPos, Vector2 movementDelta)
        {
            const float AngleThreshold = 0.707f;  // S4 Actor.cpp:315
            const float Epsilon = 1e-6f;

            if (nearby.Count == 0) return Vector2.Zero;

            float movementMag = movementDelta.Length();
            if (movementMag <= 0.001f) return Vector2.Zero;
            Vector2 objFwd = movementDelta / movementMag;

            // Our per-second velocity for the lockstep gate / speed comparison. The client uses
            // m_Movement on both sides; we derive the neighbor's from its waypoint state.
            Vector2 myVelocity = objFwd * GetMoveSpeed();

            // Soft-band collection (S4 Actor.cpp:274-285): inside (.., otherR + softRadius),
            // moving relative to us (lockstep formations don't trigger), in front of the
            // movement (the direction gate precedes both classifications).
            var members = new List<AttackableUnit>(4);
            Vector2 barycenter = Vector2.Zero;
            Vector2 groupVelocity = Vector2.Zero;
            float softRadius = GetSoftRadius(); // SELF soft term (S4 Actor.cpp:766): minion 2r, champion/pet 0.3r (F1-corrected)
            foreach (var other in nearby)
            {
                if (other == Owner || other.IsToRemove()) continue;
                if (!(other is AttackableUnit otherUnit)) continue;
                // Buff-ghost only — the escalated temp-ghost does NOT exempt body collision in
                // 4.17 (CanCollide == !ShouldIgnoreCollisionDueToGhost; P3 2026-07-19).
                if (otherUnit.IsDead || ShouldIgnoreCollisionDueToGhost(otherUnit)) continue;
                if (Vector2.Dot(objFwd, other.Position - Position) <= 0f) continue;

                float distSq = Vector2.DistanceSquared(Position, other.Position);
                if (distSq <= Epsilon) continue;
                // NEIGHBOUR term = full mRadius = the neighbour's PathfindingRadius (Actor GetRadius()).
                float softThreshold = otherUnit.PathfindingRadius + softRadius;
                if (distSq >= softThreshold * softThreshold) continue;

                Vector2 otherVelocity = Vector2.Zero;
                if (!otherUnit.IsPathEnded())
                {
                    Vector2 otherDir = otherUnit.CurrentWaypoint - otherUnit.Position;
                    float otherDirLenSq = otherDir.LengthSquared();
                    if (otherDirLenSq > Epsilon)
                    {
                        otherVelocity = otherDir * (otherUnit.GetMoveSpeed() / MathF.Sqrt(otherDirLenSq));
                    }
                }
                // Relative-movement gate (S4 Actor.cpp:281-284): identical vectors = lockstep
                // formation, no avoidance between its members. The client threshold is a per-axis
                // 1e-5 on m_Movement (essentially exact equality) — pairs with ANY relative drift
                // are collected and then filtered by the parallelness/speed branch in the
                // response. Mirror that: near-exact equality only.
                if ((myVelocity - otherVelocity).LengthSquared() <= 0.0001f) continue;

                members.Add(otherUnit);
                barycenter += other.Position;
                groupVelocity += otherVelocity;
            }
            if (members.Count == 0) return Vector2.Zero;
            barycenter /= members.Count;
            groupVelocity /= members.Count;

            // Group behind the movement: nothing to avoid (S4 Actor.cpp:755-758).
            Vector2 toCenter = barycenter - originalPos;
            if (Vector2.Dot(objFwd, toCenter) < 0f) return Vector2.Zero;

            float groupRadius = 0f;
            foreach (var m in members)
            {
                float r = Vector2.Distance(m.Position, barycenter) + m.PathfindingRadius;
                if (r > groupRadius) groupRadius = r;
            }

            // Avoidance buffer caps at 15, not 20 (S4 Actor.cpp:760-764).
            float minDistanceBuffer = Math.Clamp(
                Math.Min(groupRadius, GetHardRadius()) * 0.25f, 12f, 15f);

            Vector2 rel = barycenter - Position;
            float relLenSq = rel.LengthSquared();
            Vector2 collisionNormal = relLenSq <= Epsilon ? objFwd : rel / MathF.Sqrt(relLenSq);
            float pushDistance = Math.Max(
                groupRadius + softRadius + minDistanceBuffer - MathF.Sqrt(relLenSq), 0f);
            if (pushDistance <= 0f) return Vector2.Zero;

            float groupVelLenSq = groupVelocity.LengthSquared();
            Vector2 inObjFwd = groupVelLenSq <= Epsilon
                ? (toCenter.LengthSquared() > Epsilon ? Vector2.Normalize(toCenter) : objFwd)
                : groupVelocity / MathF.Sqrt(groupVelLenSq);

            Vector2 side = new Vector2(objFwd.Y, -objFwd.X);
            float parallelness = Vector2.Dot(objFwd, inObjFwd);

            float sign;
            if (parallelness > AngleThreshold)
            {
                // Same-direction traffic (S4 Actor.cpp:807-817): if the group is as fast as us,
                // follow instead of overtaking — no response. Otherwise pick the overtake side
                // from the group's heading relative to our axis.
                if (myVelocity.LengthSquared() <= groupVelLenSq)
                {
                    return Vector2.Zero;
                }
                sign = Vector2.Dot(inObjFwd, side) > 0f ? 1f : -1f;
            }
            else
            {
                // Head-on or crossing: side picked by which side the group center is on
                // (S4 Actor.cpp:800-805 / 819-823 — same literal sign convention as the hard
                // branch).
                sign = Vector2.Dot(toCenter, side) > 0f ? 1f : -1f;
            }

            float sideDotAbs = Math.Abs(Vector2.Dot(collisionNormal, side));
            float slideRatio = sideDotAbs <= Epsilon ? pushDistance : pushDistance / sideDotAbs;
            float magnitude = movementMag * 0.4f; // S4 Actor.cpp:839
            if (slideRatio <= magnitude)
            {
                magnitude = Math.Max(0.01f, slideRatio);
            }

            return side * (sign * magnitude);
        }

        // --- Stage 4 additional shadows (companion to the Stage 3 bridges above) ---
        private bool CanMove() => Owner.CanMove();
        private bool CanChangeWaypoints() => Owner.CanChangeWaypoints();
        // The force-move params live on the actor's own path (Riot: m_Path). The unit's public
        // MovementParameters property forwards to this same Waypoints.ForceMovement via the Waypoints shim.
        private ForceMovementParameters MovementParameters
        {
            get => Waypoints?.ForceMovement;
            set { if (Waypoints != null) Waypoints.ForceMovement = value; }
        }
        private bool SetWaypoints(NavigationPath p) => Owner.SetWaypoints(p);
        private void SetPosition(Vector2 vec, bool repath) => Owner.SetPosition(vec, repath);
        private void SetForceMovementState(bool state, MoveStopReason reason = MoveStopReason.Finished) => Owner.SetForceMovementState(state, reason);
        private int _forceMoveBlockedTicks { get => Owner.ForceMoveBlockedTicks; set => Owner.ForceMoveBlockedTicks = value; }
        private const float ForceMoveSpeedScale = AttackableUnit.ForceMoveSpeedScale;
        private const int FORCE_MOVE_BLOCK_TIMEOUT_TICKS = 15;
        private bool _movementUpdated { get => Owner.MovementUpdated; set => Owner.MovementUpdated = value; }
        private bool IsDisplacementImmune => Owner.IsDisplacementImmune;
        private bool IsCrowdControlImmune => Owner.IsCrowdControlImmune;
        private bool SetWaypoints(System.Collections.Generic.List<Vector2> wps, bool isForced) => Owner.SetWaypoints(wps, isForced);
        private const float NEAREST_SNAP_CAP = 70.0f;

        // ---- Stuck recovery / body-routing state (Stage 4) — Riot Actor_Common m_StuckTimer /
        // m_RepathedCount / m_RepathTimer. (The temp-ghost counter _stuckGhostFrames + IsTemporarilyGhosted
        // stay on the unit until Stage 5, reached via the Stage-3 shadow, because Move() still resets them.)
        private float _stuckTimerMs = 0f;
        private int _stuckRepathCount = 0;
        private Vector2 _stuckLastCheckPos = Vector2.Zero;
        private float _collisionRepathMs;
        private int _collisionRepathCount;

        /// <summary>
        /// Per tick stuck detection + escalating repath. Mirrors client
        /// <c>Actor_Common::m_StuckTimer</c> + <c>m_RepathedCount</c> logic at S1 actor_client.cpp:5040-5078.
        /// "Stuck" = actual per tick distance is less than <c>MinSpeedRatioBeforeStuck</c> of
        /// expected (movespeed * diff). Constants from playable_client_420 NSEAI.cfg defaults.
        ///
        /// On trigger: snap position to nearest walkable cell (handles dynamic blocker overlap),
        /// then re-issue path to the existing goal. Each repath escalates the next trigger threshold by
        /// <c>TimeBetweenRepathsInMS</c>, capped at 15 (= S1:5034). The ghost fallback layer
        /// (S1:5044 <c>++mGettingOutOfCollisionGhosted</c> → temporary <c>mIgnoreCollisions</c>
        /// after 15 45 stuck ticks) is intentionally not ported it would conflict with the
        /// player facing <c>StatusFlags.Ghosted</c>
        /// </summary>
        public void UpdateStuckRecovery(float diff)
        {
            // New per-tick-per-unit work added by the pathing port. Cheap on the early-out path,
            // but on trigger it calls TryUnstuckRepath -> full A*. Scoped separately from
            // AttackableUnit.Move so the trace attributes its cost (and its A* fan-out) directly.
            using var _scope = Profiler.Scope("AttackableUnit.StuckRecovery", "pathing");
            // Skip cases where stuck detection isn't meaningful.
            // Use !IsPathEnded() (NOT Waypoints.Count > 1): a unit that ARRIVED at its goal keeps
            // its full waypoint list (arrival only advances CurrentWaypointKey to Count, never
            // clears Waypoints — see Move() line ~1502), so Count stays >1 while IsPathEnded() is
            // true. The old Count>1 check treated an arrived-at-goal unit as still wanting to move:
            // actualDist≈0 vs expected>0 → "stuck" → TryUnstuckRepath does GetPath(pos→goal) where
            // goal==pos → degenerate null → escalates repathCount 0→15 over ~33s, eventually
            // ResetWaypoints (the "stopped then teleported far" bug, runtime-confirmed:
            // goal==pos, newPathCount=0, posChanged=False every event). Triggered by very short
            // paths (pathLen~26) where the unit reaches the goal. The degenerate in-blocker case
            // the old comment cared about (HandleMove [pos,pos] on a NOT_PASSABLE cell) is still
            // recovered via the !IsWalkable branch.
            bool wantsToMove = !IsPathEnded() || !_game.Map.PathingHandler.IsWalkable(Position, 0f);
            if (!CanMove() || MovementParameters != null || !wantsToMove)
            {
                _stuckTimerMs = 0f;
                _stuckRepathCount = 0;
                _stuckLastCheckPos = Position;
                _collisionRepathMs = 0f;
                return;
            }

            // BODY-ROUTING (S4 forceRepath, Actor.cpp:1817-1871): while IN HARD COLLISION and still
            // pathing toward a goal, continually rebuild the path actor-aware (skip the straight-line
            // LOS fast-path so the grid A* routes AROUND the bodies) on a throttle — so clash units
            // curve n>=3 around the wave instead of clipping straight through on an n=2 path. This is
            // Riot's per-tick `forceRepath -> BuildNavGridPath` whenever in collision and not making
            // waypoint progress, gated by the RepathTimer; our 0.25-speed-ratio genuine-stuck watchdog
            // below is far tighter (a freely-clipping minion never trips it), so it alone left units
            // clipping. Distinct from that watchdog: this only REROUTES, never gives up / ResetWaypoints.
            // The skip-LOS GetPath + actor-aware smoothing + the predicate's start-proximity / near-goal
            // exemptions keep it routing around the NEAR side, not wrapping behind the wave.
            const float COLLISION_REPATH_INTERVAL_MS = 250f; // Riot s_TimeBetweenRepathsInSeconds base (NSEAI TimeBetweenRepathsInMS=250)
            if (_inHardCollision && !IsPathEnded() && CanChangeWaypoints())
            {
                _collisionRepathMs += diff;
                // ESCALATING BACKOFF (sr132 "blocked crowd stutters/twitches backwards",
                // 2026-07-19): a flat 250ms cadence re-broadcasts a fresh detour around a
                // persistent blocker four times a second; every broadcast hard-snaps the client
                // to wp0 and the detour side can alternate — the visible stutter. Riot backs
                // off progressively: each in-collision rebuild bumps m_RepathedCount and the
                // next threshold is count·repathTimings[min(count,15)]·s_TimeBetweenRepaths +
                // s_StuckDelay (S1 actor_client.cpp:5034-5051; the 4.17 repathTimings table is
                // unrecovered garble, so the per-step factor is approximated as 1 — linear
                // growth 250,500,750…ms, capped at count 15 ≈ 4s). The count resets on any
                // collision-free tick (else-branch below), so the FIRST reroute of an encounter
                // keeps the responsive 250ms.
                float repathThreshold = COLLISION_REPATH_INTERVAL_MS * (1 + Math.Min(_collisionRepathCount, 15));
                if (_collisionRepathMs >= repathThreshold)
                {
                    _collisionRepathMs = 0f;
                    _collisionRepathCount++;
                    // F3 (docs/PATHING_AUDIT_2026_07_19.md, decomp Actor.cpp:1758-1872): Riot's
                    // forceRepath fires for ANY in-collision pathing unit on the repath-timer
                    // cadence and rebuilds the path to the SAME goal actor-aware
                    // (BuildNavGridPath, output accepted unconditionally). Two former inventions
                    // removed 2026-07-19 with the F1 radius fix in place:
                    //  - the 0.5×-expected-travel progress gate (tt119 "wizard curves behind own
                    //    wave") — Riot has no progress gate; a full-speed FOLLOWER stays straight
                    //    because its touching neighbour is start-proximity-exempt in HasStuckActor
                    //    (the ported dual-dot exemption) so the rebuilt path is identical and the
                    //    IsPathTheSame dedup drops it. The tt119 curving happened in the inverted-
                    //    flag era (minion bodies at 0.2r/×1 saw wrong blocker geometry).
                    //  - the PathThreadsThroughBodies clearance floor (wire107, clr=6 gap-threading)
                    //    — the full-size predicate (r + ×2 stuck-actor size) now prices those gaps
                    //    correctly in the A* itself.
                    // This is the wire-visible per-minion reroute channel (Riot map1: 17-26/1000
                    // pkts, goal kept, middle shifted ~1 cell around a body ~110u ahead).
                    Vector2 goal = Waypoints[Waypoints.Count - 1];
                    var routed = _game.Map.PathingHandler.GetPath(Owner, goal, skipLineOfSight: true);
                    if (routed != null && routed.Count >= 2
                        && !routed.IsPathTheSame(Waypoints, Position, 0, CurrentWaypointKey))
                    {
                        SetWaypoints(routed);
                    }
                    // Riot resets the temp-ghost escalation whenever the constrained in-collision
                    // rebuild RAN (Actor.cpp:1858, unconditional — not gated on the path having
                    // changed): a fresh routing attempt restarts the escalation clock.
                    _stuckGhostFrames = 0;
                }
            }
            else
            {
                _collisionRepathMs = 0f;
                // Collision-free tick → the backoff ladder resets (Riot resets m_RepathedCount
                // with its collision-free bookkeeping); the next encounter's first reroute is
                // responsive again.
                _collisionRepathCount = 0;
            }

            float dx = Position.X - _stuckLastCheckPos.X;
            float dy = Position.Y - _stuckLastCheckPos.Y;
            float actualDist = MathF.Sqrt(dx * dx + dy * dy);
            float expectedDist = GetMoveSpeed() * (diff / 1000f);
            _stuckLastCheckPos = Position;

            // S1 NSEAI.cfg `MinSpeedRatioBeforeStuck = 0.25` actual < 25% of expected = stuck.
            // Naturally handles slow effects since GetMoveSpeed already accounts for them.
            // Special case `expectedDist <= 0` only when the unit ALSO has no path; otherwise
            // a path ended unit on a blocked cell would never get unstuck.
            const float MIN_SPEED_RATIO = 0.25f;
            if (expectedDist <= 0.001f && Waypoints.Count <= 1)
            {
                _stuckTimerMs = 0f;
                _stuckRepathCount = 0;
                return;
            }
            if (expectedDist > 0.001f && actualDist >= expectedDist * MIN_SPEED_RATIO)
            {
                _stuckTimerMs = 0f;
                _stuckRepathCount = 0;
                return;
            }

            _stuckTimerMs += diff;

            // S1 NSEAI.cfg defaults: StuckDelayInMS=200, TimeBetweenRepathsInMS=250, max-cap=15
            // (S1:5034). Threshold escalates so a unit stuck and repath loop doesn't spam.
            const float STUCK_DELAY_MS = 200f;
            const float STUCK_REPATH_INTERVAL_MS = 250f;
            const int STUCK_MAX_REPATHS = 15;

            int countCapped = Math.Min(_stuckRepathCount, STUCK_MAX_REPATHS);
            float threshold = STUCK_DELAY_MS + countCapped * STUCK_REPATH_INTERVAL_MS;

            if (_stuckTimerMs > threshold)
            {
                bool unstuck = TryUnstuckRepath();
                _stuckTimerMs = 0f;
                if (unstuck)
                {
                    // Reset escalation and treat next stuck-event as fresh.
                    _stuckRepathCount = 0;
                }
                else
                {
                    // Repath made no change then escalate so we don't spam, and after the cap give
                    // up entirely (clear waypoints) so subsequent player orders aren't shadowed.
                    _stuckRepathCount++;
                    if (_stuckRepathCount > STUCK_MAX_REPATHS)
                    {
                        ResetWaypoints();
                        _stuckRepathCount = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Stuck recovery action meaning we snap position to nearest walkable cell (escapes dynamic blocker
        /// overlap, e.g. Inhibitor/Nexus respawn or knockback into terrain), then issue a fresh
        /// actor aware path to the existing goal. <see cref="SetPosition"/> with <c>repath: false</c>
        /// avoids recursing through the SafePath logic. If repath fails, position is at least
        /// snapped to walkable so the next tick's path-following starts from a clean state.
        /// </summary>
        // NOTE (F3 2026-07-19): the former `PathThreadsThroughBodies` clearance-floor rejection
        // for actor-aware reroutes (wire106/107 "clr=6 gap-threading") was removed: it was
        // compensating for the inverted-radius era (F1) in which the stuck-actor predicate saw
        // half-size minion bodies and priced pack gaps as walkable. With full radii + the ×2
        // minion stuck-actor size the A* itself prices those gaps; the decomp accepts
        // BuildNavGridPath output unconditionally (Actor.cpp:1864-1872).

        private bool TryUnstuckRepath()
        {
            // Stuck-recovery action: GetClosestTerrainExit + a full actor-aware A*. Scoped so the
            // trace shows how often stuck units force an unplanned repath.
            using var _scope = Profiler.Scope("AttackableUnit.TryUnstuckRepath", "pathing");
            // Snap to nearest walkable cell. Use radius=0 (= cell walkable check, ignore
            // PathfindingRadius clearance) this is for stuck recovery we just need to escape the
            // blocking cell, even if the destination has tighter clearance than usual. This is
            // what makes the stuck fix work for cases where the unit is wedged in narrow gaps
            // (Inhibitor edges, lane wall corners) where no PathfindingRadius clear position
            // exists nearby.
            Vector2 snappedFrom = _game.Map.NavigationGrid.GetClosestTerrainExit(Position, 0f);
            bool positionChanged = snappedFrom != Position;
            if (positionChanged)
            {
                SetPosition(snappedFrom, repath: false);
            }

            // No goal to repath to (degenerate waypoints) at least the position snap counts as
            // progress if it happened.
            if (Waypoints == null || Waypoints.Count <= 1)
            {
                return positionChanged;
            }
            Vector2 goal = Waypoints[Waypoints.Count - 1];

            // B1: skip the straight-line LOS fast-path so this recovery repath runs the actor-aware
            // grid A* and smooths actor-aware — mirroring the client's stuck/in-collision
            // BuildNavGridPath (Actor.cpp:1866), which is the ONLY place Riot routes a path AROUND
            // bodies (the normal approach/chase path goes through the LOS-first CreatePath →
            // BuildNavigationPath, so n=2 there is faithful). A unit wedged against bodies now gets a
            // bent detour around the clump instead of the LOS-straight n=2 path back into it. The
            // actor predicate is team-AGNOSTIC (Riot's server A* probes with a zeroed collisionState,
            // PathingHandler.cs:654), so it routes around allied AND enemy bodies; lane-wave clumping
            // is preserved by the start-proximity + near-goal exemptions in the predicate/GetPath.
            var newPath = _game.Map.PathingHandler.GetPath(Owner, goal, skipLineOfSight: true);
            if (newPath != null && newPath.Count >= 2)
            {
                // Only count this as genuine "unstuck" progress if the recompute actually
                // rerouted (or we snapped off a blocked cell). With the actor-aware skip-LOS repath
                // above, an enemy-wedge now genuinely reroutes (bent path) → reported as progress →
                // backoff resets, which is correct (the unit escaped by routing around). If the
                // recompute STILL returns the same path (e.g. the only blockers are allies, which
                // don't block, or the wedge is purely the near-goal exemption zone), we report NO
                // progress so Riot's escalating backoff (NSEAI TimeBetweenRepathsInMS=250) engages
                // and the temp-ghost counter (Move(): _stuckGhostFrames → IsTemporarilyGhosted at
                // 45/15) becomes the escape of last resort.
                bool reroutedOrSnapped = positionChanged
                    || !newPath.IsPathTheSame(Waypoints, Position, 0, CurrentWaypointKey);
                SetWaypoints(newPath);
                return reroutedOrSnapped;
            }

            return positionChanged;
        }

        // Per-tick movement integrator (Riot Actor_Common::Update path-follow + collision tail).
        // Entry from the still-on-unit Move() shim (via ObjAIBase.Move's base call).
        public bool Move(float delta)
        {
            Vector2 originalPos = Position;
            bool walked = false;
            // Reset the body-routing trigger each tick; only the moving+collision path
            // (RunCollisionResponse) sets it true, so stationary / ghosted / dashing leave it false.
            _inHardCollision = false;
            _inBodyContact = false;

            if (CurrentWaypointKey < Waypoints.Count)
            {
                float speed = GetMoveSpeed() * 0.001f;
                var maxDist = speed * delta;

                while (true)
                {
                    var dir = CurrentWaypoint - Position;
                    var dist = dir.Length();

                    if (maxDist < dist)
                    {
                        Position += dir / dist * maxDist;
                        break;
                    }
                    else
                    {
                        // F7 (2026-07-19) — gated END-snap, decomp-literal (Actor_Common::
                        // AssembleWaypointList final commit, Actor.cpp:2131-2137): the exact
                        // stored end position is committed ONLY if the remaining hop is
                        // terrain-LOS-clear AND the end cell is passable; otherwise the walk
                        // finishes where the unit stands and the path still counts as consumed.
                        // A bad final vertex (stale path, steered remnant, force-move leftover)
                        // must not teleport the unit into geometry it could never walk to.
                        // Intermediate waypoints stay ungated exactly like Riot's advance loop.
                        if (CurrentWaypointKey == Waypoints.Count - 1)
                        {
                            Vector2 end = CurrentWaypoint;
                            if (_game.Map.NavigationGrid.IsGridLineOfSightClear(Position, end)
                                && _game.Map.PathingHandler.IsWalkable(end, 0f))
                            {
                                Position = end;
                            }
                            CurrentWaypointKey++;
                            break;
                        }

                        Position = CurrentWaypoint;
                        maxDist -= dist;

                        CurrentWaypointKey++;
                        if (maxDist == 0)
                        {
                            break;
                        }
                    }
                }
                walked = true;

                // No terrain check on the natural waypoint walk because the path was produced by the
                // actor aware A* (PathingHandler.GetPath, A1) which only emits walkable cells, so
                // a check here would only fire on precision artifacts at blocker edges. The S1 client's terrain check at
                // actor_client.cpp:2241-2259 fires inside HandleActorCollision after the
                // collision response push i.e., the bodyblocking equivalent below but not on the
                // natural walk.
            }

            // (bodyblocking) Separation also occurs without an active waypoint walk
            // otherwise, units that reach their waypoint within an overlap get stuck.
            // Dashes completely bypass separation so as not to disrupt dash trajectories.
            bool pushed = false;
            if (MovementParameters == null)
            {
                // Broadphase query radius = 4·ActorRadius. The QuadTree match is radius-sum
                // (dist < query.r + other.r, QuadTree.IntersectsWith), so GetNearestObjects(this)
                // — query.r = self.r — only reached self.r + other.r (~130u for a champion). That
                // is SHORTER than the largest per-pair consumer threshold: soft-avoidance needs
                // out to other.r + GetSoftRadius() = other.r + 2·self.r (~195u), and hard needs
                // selfHard + other.r + buffer. Neighbours in that band were never returned, so the
                // pre-contact veer and the group barycenter were computed from a TRUNCATED set
                // (late/incomplete avoidance, wrong group centre near the edge). Riot collects
                // collision neighbours at 2·GetRadius() (hard pass) then 4·GetRadius() (avoidance
                // pass) — Actor.cpp:1543-1606 — so 4·ActorRadius matches its wider pass and
                // covers every per-pair threshold with margin. Each helper re-filters by its own
                // precise threshold, so the extra candidates are harmless (just a wider broadphase).
                // ActorRadius (= mRadius = PathfindingRadius), NOT the gameplay CollisionRadius.
                float queryRadius = 4f * MathF.Max(0.5f, ActorRadius);
                var nearby = _game.Map.CollisionHandler.GetNearestObjects(
                    new System.Activities.Presentation.View.Circle(Position, queryRadius));

                Vector2 originalDelta = Position - originalPos;
                bool moving = originalDelta.LengthSquared() > 0.0001f;

                // P3 CORRECTION (2026-07-19): the former `moving && IsTemporarilyGhosted` branch
                // (skip own collision processing entirely while temp-ghosted) was WRONG for 4.17.
                // Body collision uses CanCollide = ShouldIgnoreCollisionDueToGhost(my, test)
                // (Actor.cpp:300-304) which consults ONLY the buff-ghost flags — the escalated
                // mIgnoreCollisions is consumed exclusively by the PATHING queries (HasStuckActor/
                // HasBlockedActor via GetCollisionState; the non-raw state). S1 still checked the
                // counter in body collision (actor_client.cpp:846) — 4.17 moved the escape to the
                // path level: a temp-ghosted unit keeps colliding bodily but its A* ignores all
                // actors (BuildActorBlockedPredicate returns null) and blockers ignore it.
                if (moving)
                {
                    // MOVING units: run the per-tick collision control-flow structure (S4
                    // Actor_Common::Update collision tail, Actor.cpp:1714-1741) — steer the path,
                    // then the gated HandleActorCollision relaxation loop. Consolidated into one
                    // method so the steps compose as a single coherent flow (instead of the former
                    // scattered inline pushes).
                    _smoothedSeparationPush = Vector2.Zero;
                    pushed |= RunCollisionResponse(nearby, originalPos, originalDelta, delta);
                }
                else
                {
                    // STATIONARY units: NO separation — FAITHFUL to Riot (2026-06-18). The decomp
                    // collision response (Actor.cpp HandleActorCollision) operates on and MODIFIES
                    // m_Movement (Actor.cpp:871-873); a non-moving unit has m_Movement≈0 so it yields
                    // no response, and there is NO stationary-separation "else" branch in Riot. Our
                    // former ComputeSeparationPush here was a server-authority addition (the client's
                    // collision callback never runs for non-moving actors) — but Riot simply does NOT
                    // separate stopped units; overlaps resolve when a unit next MOVES (lane-walk / AI
                    // reposition), separating via the moving branch above. Removing it kills the
                    // stationary-drift over-emission + combat-start position churn at the root. Just
                    // reset the per-tick contact state (a stopped unit is trivially un-stuck).
                    // WATCH (in-game): if attacking minion clumps visibly STACK, that's a downstream
                    // gap — our minion AI must reposition like Riot's (which moves ~200u/1s between
                    // attacks, re-separating via the moving branch) — NOT a reason to re-add this.
                    _stuckGhostFrames = 0;
                    _smoothedSeparationPush = Vector2.Zero;
                }

                // (Stuck detection + extra push lives inside the moving branch above — it both
                // applies the centroid escape push and drives the temp-ghost counter.)

                // S0 DIAGNOSTICS (Stage C, no behavior change): measure how often the current
                // position-first integrator produces a committed step that Riot's final-step
                // sanitation would reject — so we know whether porting those guards (S3/S4) is
                // catching a real producer bug or is dead weight. Env-gated (CollisionLogger).
                //   overspeed: total committed step this tick > max(2*cellSize, intendedStep*3)
                //             (S4 Actor_Common::Update overspeed clamp, Actor.cpp:1934-1940).
                //   validatepos: |step|^2 > 6000 AND terrain LOS old->new fails
                //             (S4 Actor_Common::ValidatePosition, Actor.cpp:1490-1513, kMaxPosDeltaSq).
                if (CollisionLogger.Enabled)
                {
                    Vector2 stepDelta = Position - originalPos;
                    float stepDistSq = stepDelta.LengthSquared();
                    if (stepDistSq > 0.0001f)
                    {
                        float cellSize = _game.Map.NavigationGrid.CellSize;
                        float intendedStep = GetMoveSpeed() * 0.001f * delta;
                        float overspeedThresh = MathF.Max(2f * cellSize, intendedStep * 3f);
                        float stepDist = MathF.Sqrt(stepDistSq);
                        if (stepDist > overspeedThresh)
                        {
                            CollisionLogger.Log(_game.GameTime, NetId, "overspeed", stepDist, overspeedThresh, Position);
                        }
                        // ValidatePosition's kMaxPosDeltaSq is a squared world-unit threshold (6000).
                        if (stepDistSq > 6000f
                            && !_game.Map.NavigationGrid.IsGridLineOfSightClear(originalPos, Position))
                        {
                            CollisionLogger.Log(_game.GameTime, NetId, "validatepos", stepDist, 77.46f, Position);
                        }
                    }
                }

                // Force a movement data resync once the unreplicated drift gets large enough
                // that the client would otherwise see a visible snap on the next SetWaypoints.
                // Applies to STOPPED units too (Waypoints.Count == 1): GetCenteredWaypoints emits a
                // valid [Position] (n=1) for them, and Riot replicates a stopped unit's position
                // EVERY time it changes (AIManager_Common::PauseActor -> Actor::ServerStop,
                // AIManager.cpp:227-235, `mLastPausePosition != AI_Position`). Replay 343e3502:
                // attacking minions emit n=1 0x61 ~1/s tracking their ~200u separation jitter, and
                // Basic_Attack_Pos(0x1A) carries the matching position. WITHOUT replicating stopped
                // drift, a stationary minion's ComputeSeparationPush accumulated UNSEEN until the
                // next Basic_Attack_Pos snapped it to a scattered spot ("minions snap to different
                // positions when combat starts"). The old "can't build a valid packet" claim was
                // wrong — n=1 IS Riot's stop-packet.
                //
                // Threshold sized to match Riot's observed cadence: replay shows walking minions
                // resync every ~167u (≈ 0.5s at 325u/s movespeed). At the previous 25u threshold
                // we were emitting ~6× more keepalive WaypointGroups than Riot for steady-state
                // walking so a bandwidth waste with no visible benefit (client interpolates fine over
                // the longer interval).
                //
                // CHAMPIONS use a much tighter threshold (2026-06-07, replay-measured): the
                // client HARD-SETS m_Position to Waypoint[0] on every WaypointGroup receive
                // (ClientFollowServerPath, ActorClient.cpp:169 `m_Position = m_Path.GetStartPoint()`)
                // — the snap IS the sync mechanism. Riot therefore corrects walking heroes with
                // FREQUENT TINY updates: replay 343e3502, 471 mid-walk same-destination 0x61 for
                // the hero, median gap 96ms, wp0 correction median 19u / p90 86u — invisible.
                // Our old 175u threshold (sized off the MINION cadence) accumulated half a
                // second of divergence and released it as one visible forward teleport.
                //
                // WHY WE NOW GO BELOW RIOT'S 19u MEDIAN (2026-06-17): Riot's server and the
                // 4.x client ran the SAME collision code, so their positions agreed and the 19u
                // correction was just float/cadence noise. WE reimplement collision server-side,
                // SEPARATELY from the client's local sim, so the two genuinely diverge — and the
                // client's local push is pushDistance-based, NOT dt-scaled, so the divergence
                // grows with frame rate (uncapped FPS = larger per-second drift). At a 20u cap
                // that divergence is released as one ~20u snap, which IS visible on the player's
                // own champion in crowds/teamfights (lateral separation pushes snap sideways).
                // The cap = the max single-snap magnitude, so we set it well under the
                // perceptual floor for a champion-sized model. This is purely drift-gated, so a
                // clean straight walk (drift ≈ 0) still emits NOTHING — the extra packets only
                // appear while there is real divergence to correct, and each correction is
                // smaller. Tune CHAMPION_DRIFT_RESYNC up if teamfight bandwidth becomes an issue,
                // down if snaps are still visible. Minions/others stay at 175u (replay-verified
                // minion cadence; their snaps aren't player-focused).
                // STOPPED units (Waypoints.Count <= 1) resync change-driven, mirroring Riot's
                // AIManager_Common::PauseActor (AIManager.cpp:227-235): it emits ServerStop the
                // moment a stopped unit's position differs from mLastPausePosition (exact !=). We
                // can't use exact != because we (unlike Riot) push stopped units every tick via
                // ComputeSeparationPush (server-authority overlap fix — Riot's collision callback
                // never runs for non-moving actors), so an exact gate would emit per-tick; an 8u
                // sub-perceptual cap is the practical change-driven equivalent. This tracks a
                // stopped minion's separation jitter tightly so residual <175u drift no longer
                // surfaces as a snap at the next Basic_Attack_Pos (the "minions snap at combat
                // start" complaint). Per-frame batching folds clumped minions into one WaypointGroup
                // so the client follows the small steps smoothly. MOVING minions keep 175u (replay-
                // verified walking cadence + travel keepalive); champions use 8u in both states.
                // (Deeper faithfulness option, not done: stop separating stopped units at all, like
                // Riot — then position is stable and this rarely fires. Risk: spawn/clump overlap.)
                // 8u = sub-perceptual snap cap, used for champions (any state) and for any STOPPED
                // unit (change-driven PauseActor equivalent). Moving non-champions use 175u.
                const float TIGHT_RESYNC = 8f;
                // MOVING MINION RESYNC (tightened 2026-06-21): the 175u threshold was the root of the
                // user-observed "melee minions teleport forward when advancing after combat / snap to
                // different targets". 175u was copied from Riot's measured walking-minion cadence — but
                // that reasoning only holds for Riot, whose server and 4.x client ran the SAME collision
                // code, so their positions AGREED and the ~167u resync carried ~0 real drift (see the
                // champion block above, lines 1928-1935, which fixed the identical "visible forward
                // teleport" for heroes by dropping to 8u). WE reimplement collision server-side, so a
                // minion shoved around in a clash (ComputeSeparationPush) genuinely diverges from the
                // client's local sim by up to the threshold, and the next WaypointGroup hard-snaps
                // (ActorClient.cpp:169) that whole divergence forward in one visible jump — worst right
                // after combat, when the accumulated clash-push releases as the unit resumes advancing.
                // 30u keeps the per-snap correction sub-perceptual for a minion-sized model while still
                // far cheaper than the champion 8u (≈4 entries/list, per-frame batched). Tune down toward
                // TIGHT_RESYNC if snaps are still visible, up if minion bandwidth becomes an issue.
                // TIGHTENED 30u→12u (2026-06-28): the collision-log capture (collstats, solo Map 1) showed
                // the moving-minion drift-resync releasing a median 33u / max 62u hard-snap at 3/s — i.e.
                // resyncs fired right at the 30u cap, so the cap WAS the visible snap magnitude. A
                // marching allied wave shoves its own members ~15u/tick (group push, 10.6/s) with no
                // enemy present, so the drift is constant; 12u brings each released snap close to the
                // champion floor (sub-perceptual for a minion model) at a modest packet cost (we batch
                // per frame, and Fix 1 above cut the bulk of the redundant reanchors). Tune toward
                // TIGHT_RESYNC if still visible, up if minion bandwidth becomes an issue.
                const float MOVING_MINION_RESYNC = 12f;
                bool stopped = Waypoints.Count <= 1;
                float driftResyncThreshold = (Owner is Champion || stopped) ? TIGHT_RESYNC : MOVING_MINION_RESYNC;
                if (_unreplicatedDrift.LengthSquared() > driftResyncThreshold * driftResyncThreshold)
                {
                    if (CollisionLogger.Enabled && !(Owner is Champion))
                    {
                        // drift here = the divergence the client will hard-snap when the next
                        // WaypointGroup lands (= the visible teleport magnitude).
                        CollisionLogger.Log(_game.GameTime, NetId, "resync", 0f, _unreplicatedDrift.Length(), Position);
                    }
                    Owner.RequestMovementSync();
                }

                // Travel-cadence keepalive = a periodic Waypoint[0] re-anchor while the unit
                // walks. Each WaypointGroup the client receives hard-sets m_Position to wp0
                // (ActorClient.cpp:169), so re-anchoring frequently to the unit's TRUE position
                // keeps the client from interpolating a stale path for long — accumulated
                // FP/speed/collision divergence is then corrected in many tiny (invisible)
                // steps instead of released as one visible snap on the next path change. The
                // resend carries the trimmed route (champions get their full remaining route via
                // GetCenteredWaypoints, re-seeded at the current Position), so it is purely a
                // re-anchor, not a path change.
                //
                // CHAMPION cadence reinstated 2026-06-17 (fresh replay measurement —
                // tools/wpan.py over 343e3502 + a6db3774, champion = 0x46 sender — SUPERSEDES the
                // earlier "Riot goes SILENT for seconds on a fixed path" claim, which was WRONG):
                // Riot resends a MOVING champion's 0x61 CONTINUOUSLY — gap histogram mode at the
                // 150-200ms bucket (2046 / 2640 hits), of which 1659 / 1771 are SAME-GOAL resends
                // (goal within 40u of the prior = a genuine periodic streamer, not new orders);
                // true silence (gap > 1000ms) is rare (167 / 352 vs thousands). 57u ≈ 167ms at a
                // 340u/s champion movespeed, putting us at Riot's measured mode. Distance-gated,
                // so a stopped champion (Waypoints.Count == 1) emits nothing and a slowed one
                // resends less often — matching Riot's idle behaviour.
                // CAUTION: the OLD streamer was per-tick (~96ms) AND re-sent the full multi-
                // waypoint route, which caused arc jitter (network-latency snap-back: a resend
                // whose wp0 lags the client's in-flight interpolated position pulls it back
                // ~latency*speed). 57u (~167ms) is far less frequent; IN-GAME VERIFY that arc
                // jitter has not returned, and raise CHAMPION_KEEPALIVE if it has.
                //
                // Non-champions: 325u ≈ 1s of travel at minion speed (Riot's measured MAX walking
                // stretch between same-path updates — replay 343e3502). RAISED 100u→325u
                // (2026-07-03, overlap diagnosis).
                // MECHANISM CORRECTED 2026-07-19 (client-model adjudication, STAGE_C plan +
                // ghost-gate read): the original rationale — "dense re-anchors reset the client's
                // own local collision separation" — was WRONG; a moving client actor is mGhosted
                // per tick (AIBase.cpp UpdateMovement), there is NO client-side separation to
                // reset. The 07-03 overlap "improvement" from sparser anchors was likely partly a
                // METRIC artifact (the wire nearest-neighbour overlap metrics compare against
                // ≤2s-stale neighbour positions; denser emissions sample more artifact pairs —
                // see the 07-19 coloc staleness correction). What remains true: real off-path
                // divergence is server collision output, which accumulates in _unreplicatedDrift
                // and fires the 12u drift-RESYNC immediately regardless of this cadence — the
                // keepalive only folds sub-12u noise, so the sparse value is safe and the wire
                // cadence it produces sits at Riot's measured band (52.6 vs 53-60 re-anchors/min
                // per moving minion, map1 baseline).
                // EXPERIMENT ENDED (2026-07-05): the 2026-07-04 wiggle experiment raised this
                // 325→650 (and contact 100→250, reverted earlier). tt122 exposed the REAL wiggle
                // root instead: the Position+3 waypoint-count runway in GetCenteredWaypoints ran
                // dry between anchors (SubdividePath shortened waypoint spacing; 3 waypoints ≈
                // 250-500u vs the 650u stride) — the client copy stood mid-window and was
                // teleported forward by the next anchor, a uniform 145.9u yank on EVERY minion.
                // The runway is now DISTANCE-based (800u, see GetCenteredWaypoints) so the stride
                // can never outrun the client's path again; 325u keeps the July overlap-fix
                // benefits (client-separation windows) without the dry-run.
                const float NONCHAMPION_KEEPALIVE = 325f;
                const float CHAMPION_KEEPALIVE = 57f;
                // DENSITY-AWARE contact cadence (2026-07-04, wire105 "ranged minions teleport
                // FORWARD in marching groups"; tt117 confirmed the yank returns at sparse contact
                // anchors). NOTE 2026-07-19 (adjudicated client model): the original explanation
                // — "the client's own collision braking makes its copy fall behind" — was wrong
                // (moving client actors are ghosted; the copy is a pure path follower). The real
                // fall-behind sources in clumps were most plausibly (a) the client attack loop
                // pinning units in place with no forced ISA on disengage (FIXED 2026-07-19,
                // LaneMinionAI disengage-to-march) and (b) DeadRecon render catch-up after
                // re-anchor gaps. The observations behind this knob were real; its mechanism
                // story was not. NOTE it is DEAD for LaneMinions (unconditional 650u override
                // below) — it only drives non-lane Minions (jungle monsters, pets). Candidate
                // for simplification once a capture confirms the disengage fix removed the yank
                // class at 325u.
                const float CONTACT_KEEPALIVE = 100f;
                _traveledSinceLastSync += originalDelta.Length();
                _timeSinceLastSync += delta;
                float keepaliveDist = Owner is Champion ? CHAMPION_KEEPALIVE
                    : (_inHardCollision || _inBodyContact) ? CONTACT_KEEPALIVE : NONCHAMPION_KEEPALIVE;
                // LANE MINION CADENCE (2026-07-05, two-step calibration):
                //
                // Step 1 (wobble fix, verified in-game): the old 325u/100u keepalive PLUS the
                // rolling-segment re-issues REPLACED the client's path mid-leg several times per
                // leg — that churn was the marching wobble. Removed entirely (silence during the
                // leg, 12u drift-resync as the only net) → wobble gone.
                //
                // Step 2 (tt125 "minions accelerate dash-like the further they advance"): full
                // silence overshot — with anchors 3.4-5s apart the client copy visibly diverged
                // before the next anchor pulled it in. MECHANISM CORRECTED 2026-07-19: the
                // original reading ("the client SIMULATES its copies locally and drifts ~31u/s")
                // rests on the refuted client model — moving client actors are ghosted pure
                // followers. What Riot's 31.5u/16.5u anchor-snap medians actually show is how
                // far Riot's SERVER position walks off the previously-sent path between anchors
                // (server-side perturbation + emission composition), not client drift; our
                // client-copy divergence over long gaps comes from speed-replication timing, FP
                // accumulation and DeadRecon render catch-up. The 650u (~2s) same-path re-anchor
                // (full remaining leg, wp0 re-seeded, no truncation/replacement — the wobble
                // mechanics stay gone) bounds all of those; the resulting wire cadence sits at
                // the Riot map1 band. (The old "wobble returning would prove Stage C" clause is
                // obsolete — Stage C was closed 2026-07-19, see STAGE_C plan closure note.)
                const float LANEMINION_KEEPALIVE = 650f;
                if (Owner is LaneMinion)
                {
                    keepaliveDist = LANEMINION_KEEPALIVE;
                }
                if (Waypoints.Count > 1 && _traveledSinceLastSync >= keepaliveDist)
                {
                    Owner.RequestMovementSync();
                }
                // STOPPED-UNIT POSITION KEEPALIVE (replay-verified, NOT in the visible decomp — the
                // server-side movement encoders are stubbed there, but the 4.17 wire shows a STANDING
                // unit re-broadcasting its BYTE-IDENTICAL position ~every 0.8s, not just on change).
                // Our drift-resync above only fires when a stopped unit's position actually moves
                // (>threshold), so a PERFECTLY stationary unit went silent. During an auto-attack windup
                // the server holds the unit fixed (position delta 0) — with no re-anchor the client
                // drifts forward on the attack animation's root-motion ("slides toward the target while
                // charging the swing", melee monster repro). Re-affirming the stop position on Riot's
                // ~0.8s cadence pins the client. Time-gated (not distance), so it fires even at zero
                // drift; per-frame batching folds all standing units into one WaypointGroup.
                const float STOPPED_KEEPALIVE_MS = 800f;
                // EXCLUDE units in their client-autonomous auto-attack loop. A lane minion / jungle
                // monster (obj_AI_Minion) is put into a hardcode-attack state by the single
                // Basic_Attack_Pos packet, then re-fires its swing CLIENT-SIDE with NO further server
                // packets (see Spell.cs AA block). Sending it a movement WaypointGroup mid-attack —
                // even a same-position keepalive — breaks the client out of that loop and restarts the
                // attack animation, which presented as melee attack animations flickering in/out and
                // landing off-sync on AncientGolem / Lizard Elder. Riot never re-broadcasts position to
                // an autonomously-attacking unit; the stopped keepalive is for genuinely IDLE units.
                // HasMadeInitialAttack stays true for the whole locked-on engagement; IsAttacking covers
                // the very first windup before it flips.
                bool inAutonomousAttackLoop = Owner is Minion minion
                    && (minion.IsAttacking || minion.HasMadeInitialAttack);
                if (Waypoints.Count <= 1 && _timeSinceLastSync >= STOPPED_KEEPALIVE_MS && !inAutonomousAttackLoop)
                {
                    Owner.RequestMovementSync();
                }
            }
            else
            {
                // Dash / forced movement: no body collision, and forced movement clears any
                // accumulated stuck state.
                _smoothedSeparationPush = Vector2.Zero;
                _stuckGhostFrames = 0;
            }

            return walked || pushed;
        }

        // Per-tick force-move (dash/knockback) integrator (Riot Actor_Common force-path advance).
        public float UpdateForceMovement(float frameTime)
        {
            var MP = MovementParameters;
            Vector2 dir;
            float distToDest;
            float distRemaining = float.PositiveInfinity;
            float timeRemaining = float.PositiveInfinity;
            if (MP.FollowNetID > 0)
            {
                GameObject unitToFollow = _game.ObjectManager.GetObjectById(MP.FollowNetID);
                if (unitToFollow == null)
                {
                    SetForceMovementState(false, MoveStopReason.LostTarget);
                    return frameTime;
                }
                dir = unitToFollow.Position - Position;
                distToDest = Math.Max(0, dir.Length() - MP.MoveBackBy);
                if (MP.FollowDistance > 0)
                {
                    distRemaining = MP.FollowDistance - MP.PassedDistance;
                }
                if (MP.FollowTravelTime > 0)
                {
                    // FollowTravelTime is seconds (script param); ElapsedTime accumulates ms.
                    timeRemaining = MP.FollowTravelTime * 1000f - MP.ElapsedTime;
                }
            }
            else
            {
                if (Waypoints == null || Waypoints.Count <= 1)
                {
                    SetForceMovementState(false, MoveStopReason.ForceMovement);
                    return frameTime;
                }

                dir = Waypoints[1] - Position;
                if (float.IsNaN(dir.X) || float.IsNaN(dir.Y) || float.IsInfinity(dir.X) || float.IsInfinity(dir.Y))
                {
                    SetForceMovementState(false, MoveStopReason.ForceMovement);
                    return frameTime;
                }
                distToDest = dir.Length();
            }
            distRemaining = Math.Min(distToDest, distRemaining);

            float time = Math.Min(frameTime, timeRemaining);
            // Force-moves traverse at ForceMoveSpeedScale (Riot's "reduceSpeedSlightly", AIBase.cpp:1920);
            // the parabolic arc HEIGHT is client-only, the server moves purely horizontally and ends on
            // reaching the goal, so a dash covers its distance in distance / (speed * ForceMoveSpeedScale).
            float speed;
            if (MP.FollowNetID > 0 && MP.FollowTravelTime > 0)
            {
                // Fixed-travel-time follow: re-scale speed every tick so the unit reaches the (moving)
                // target exactly when the travel time elapses, regardless of how the target moves —
                // Riot's Actor_Common::TrackTargetUnit (Actor.cpp:2256): travelVelocity = remainDist /
                // remainTime, set as the path speed override (PathSpeedOverride is ignored in this mode).
                // distToDest is world-units, timeRemaining is ms → units/ms, matching the else branch.
                speed = distToDest / Math.Max(timeRemaining, 0.0001f) * ForceMoveSpeedScale;
            }
            else
            {
                speed = MP.PathSpeedOverride * 0.001f * ForceMoveSpeedScale;
            }
            float distPerFrame = speed * time;
            float dist = Math.Min(distPerFrame, distRemaining);
            if (dir != Vector2.Zero)
            {
                Position += Vector2.Normalize(dir) * dist;
            }

            // MoveBlockTimeOut give-up (see _forceMoveBlockedTicks): a force move that produces no
            // displacement for 15 consecutive-ish ticks (zero speed override with no travel time,
            // or an unreachable follow) ends instead of holding the unit in the suppressed
            // force-move state forever. Counted cumulatively per force move, like Riot's
            // path-lifetime counter. The arrival branches below still take precedence this tick.
            if (dist <= 0.001f && distRemaining > distPerFrame && timeRemaining > frameTime)
            {
                if (++_forceMoveBlockedTicks >= FORCE_MOVE_BLOCK_TIMEOUT_TICKS)
                {
                    SetForceMovementState(false, MoveStopReason.ForceMovement);
                    return frameTime;
                }
            }

            if (distRemaining <= distPerFrame)
            {
                SetForceMovementState(false);
                return (distPerFrame - distRemaining) / speed;
            }
            if (timeRemaining <= frameTime)
            {
                SetForceMovementState(false);
                return frameTime - timeRemaining;
            }
            MP.PassedDistance += dist;
            MP.ElapsedTime += time;

            return 0;
        }

        // Server dash/knockback setup (Riot Actor_Common::ServerForceLinePath, Actor.h:158 —
        // server body not recoverable in the Mac decomp; ours is replay-derived,
        // docs/FORCEMOVEMENTTYPE_REPLAY_DERIVATION.md). Entry from the unit shim.
        public void ServerForceLinePath(Vector2 endPos, float speed, float gravity = 0.0f, bool keepFacingLastDirection = true, bool lockActions = true, string movementName = "", AttackableUnit caster = null, bool ignoreTerrain = false, Vector2 parabolicStartPoint = default, ForceMovementType movementType = ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersType movementOrdersType = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER, float idealDistance = 0.0f, float moveBackBy = 0.0f, float innerDistance = 0.0f)
        {
            // Displacement immunity: a unit flagged Imobile (Baron/Dragon) or an epic monster cannot be
            // displaced by an EXTERNAL force (knockup/knockback/pull). A self-initiated dash (caster == this)
            // is the unit moving itself and stays allowed.
            if (caster != null && caster != Owner && (IsDisplacementImmune || IsCrowdControlImmune))
            {
                return;
            }

            if (MovementParameters != null)
            {
                SetForceMovementState(false, MoveStopReason.ForceMovement);
            }

            // IdealDistance / MoveBackBy (Riot BBMove/BBMoveAway endpoint resolution): both reshape how far
            // along the aim direction the dash ends up. IdealDistance (when > 0) REPLACES the geometric
            // distance to endPos with a fixed planned travel length, decoupling direction (endPos = aim point)
            // from magnitude. MoveBackBy then pulls the endpoint back toward the start (positive = stop short,
            // negative = overshoot). Resolved here, before the terrain clamping below, so FIRST_WALL_HIT /
            // GetClosestTerrainExit act on the final intended endpoint.
            if (idealDistance > 0.0f || moveBackBy != 0.0f)
            {
                var aimDir = endPos - Position;
                float dirLen = aimDir.Length();
                if (dirLen > 0.0f)
                {
                    var unitDir = aimDir / dirLen;
                    float travelLen = idealDistance > 0.0f ? idealDistance : dirLen;
                    travelLen -= moveBackBy;
                    if (travelLen < 0.0f)
                    {
                        travelLen = 0.0f;
                    }
                    endPos = Position + unitDir * travelLen;
                }
            }

            // Direction of the intended dash (post-idealDistance/moveBackBy, pre-clamp). Captured here so the
            // innerDistance floor below can re-extend along the SAME ray after terrain resolution shortens it.
            Vector2 intendedEnd = endPos;

            // Destination resolution by ForceMovementType. Semantics are REPLAY-DERIVED from the
            // 4.20 corpus (method + numbers: docs/FORCEMOVEMENTTYPE_REPLAY_DERIVATION.md) — the
            // native resolver (Actor::ServerGetLinePathDestination) is server-side C++ and absent
            // from every decomp we have. Three behaviors exist on the wire, all resolved at SETUP
            // time (the first 0x64 already carries the final path):
            //  - FURTHEST_WITHIN_RANGE: fly the full intended vector; only the ENDPOINT is
            //    validated (uncapped nearest-walkable snap). Walls BETWEEN start and end never
            //    shorten the dash (Tristana/Corki W cross walls and land beside a wall they
            //    aimed into; 0 refusals in 265 samples).
            //  - FIRST_WALL_HIT / GET_NEAREST_*: endpoint snapped to the nearest walkable point
            //    only within a small cap; a deeper-in-terrain endpoint REFUSES the movement — the
            //    dash degenerates to a zero-length path at the current position (Vayne Q /
            //    Riven E tumble-in-place into thick walls; the wire shows a real 0x64 with a
            //    [pos,pos] path and the spell still casts).
            //  - FIRST_COLLISION_HIT: the path itself is clamped by coarse cell-size sampling to
            //    the last walkable sample (Vayne E Condemn stops 0..50u short of the wall and
            //    steps over <50u slivers). The only mode where the RAY matters. Publishes
            //    OnCollisionTerrain (wall-stun trigger) when it clamps.
            bool wallHit = false;
            Vector2 newCoords;
            if (ignoreTerrain)
            {
                newCoords = endPos;
            }
            else
            {
                switch (movementType)
                {
                    case ForceMovementType.FIRST_COLLISION_HIT:
                    {
                        var clampedEnd = _game.Map.NavigationGrid.GetLastWalkableSampledPoint(Position, endPos);
                        if (clampedEnd != endPos)
                        {
                            wallHit = true;
                            endPos = clampedEnd;
                        }
                        newCoords = _game.Map.NavigationGrid.GetClosestTerrainExit(endPos, PathfindingRadius + 1.0f);
                        break;
                    }
                    case ForceMovementType.FIRST_WALL_HIT:
                    case ForceMovementType.GET_NEAREST_IN_RANGE:
                    case ForceMovementType.GET_NEAREST_IN_RANGE_INCLUDE_UNITS:
                    {
                        var snapped = _game.Map.NavigationGrid.GetClosestTerrainExit(endPos, PathfindingRadius + 1.0f);
                        newCoords = Vector2.Distance(snapped, endPos) > NEAREST_SNAP_CAP ? Position : snapped;
                        break;
                    }
                    default: // FURTHEST_WITHIN_RANGE
                        newCoords = _game.Map.NavigationGrid.GetClosestTerrainExit(endPos, PathfindingRadius + 1.0f);
                        break;
                }
            }

            // DistanceInner (Riot BBMoveAway): minimum displacement floor. If the wall/terrain resolution above
            // pulled the endpoint closer than innerDistance, push it back out to innerDistance along the dash
            // direction — but only to a walkable point, never into terrain (a wall genuinely caps the travel).
            // Lets an away-dash guarantee a minimum (e.g. SweepingBlow's [550,600]); innerDistance 0 = no floor.
            if (innerDistance > 0.0f)
            {
                var floorDir = intendedEnd - Position;
                float floorLen = floorDir.Length();
                if (floorLen > 0.0f && Vector2.Distance(Position, newCoords) < innerDistance)
                {
                    var flooredEnd = Position + (floorDir / floorLen) * innerDistance;
                    if (ignoreTerrain || _game.Map.NavigationGrid.IsWalkable(flooredEnd, PathfindingRadius))
                    {
                        newCoords = flooredEnd;
                    }
                }
            }

            // POSTPONE_CURRENT_ORDER + an active walk-to-point: snapshot the move destination (last
            // waypoint) NOW, before SetWaypoints below clears it. Re-issued at dash-end (ObjAIBase
            // .SetForceMovementState) so the unit resumes walking to it — an AttackTo (and a TARGETED
            // move-to-cast, which tracks PostponedCastTarget) resumes on its own by re-chasing, but a
            // destination-only order's point lives solely in Waypoints. Covers a plain MoveTo AND a
            // POSITIONAL move-to-cast (TempCastSpell with NO cast target).
            //
            // TargetUnit == null gate (P5 chase-decouple): a CHASING unit has a TargetUnit and resumes via
            // the chase (its _chaseIntent + TargetUnit survive the dash). The decouple leaves MoveOrder
            // STALE during a chase (it may read MoveTo for a unit that engaged mid-move), so without this
            // gate a chasing unit would wrongly snapshot a move-dest and the dash-end MoveTo re-issue would
            // clear _chaseIntent (dropping the chase). Positional move-to-cast also has TargetUnit == null,
            // so it still snapshots. See P1b / the forced-movement plan.
            Vector2 postponedMoveDest = Vector2.Zero;
            if (movementOrdersType == ForceMovementOrdersType.POSTPONE_CURRENT_ORDER
                && Owner is ObjAIBase moverSelf
                && moverSelf.TargetUnit == null
                && (moverSelf.MoveOrder == OrderType.MoveTo
                    || (moverSelf.MoveOrder == OrderType.TempCastSpell && moverSelf.PostponedCastTarget == null))
                && Waypoints != null && Waypoints.Count > 1 && !IsPathEnded())
            {
                postponedMoveDest = Waypoints[Waypoints.Count - 1];
            }

            // False because we don't want this to be networked as a normal movement.
            SetWaypoints(new List<Vector2> { Position, newCoords }, true);

            // Every argument of this overload IS consumed (endpoint resolution above handles
            // movementType/idealDistance/moveBackBy/innerDistance/ignoreTerrain). The zeros
            // below are deliberate for a POSITION dash: the Follow* fields belong to the
            // follow-target overload (ObjAIBase's follow dash sets them), and MoveBackBy stays 0
            // on the WIRE because the server already folded it into newCoords — sending it in
            // SpeedParams too would make the client apply the pull-back a second time.
            MovementParameters = new ForceMovementParameters
            {
                PostponedMoveDestination = postponedMoveDest,
                SetStatus = StatusFlags.None,
                ElapsedTime = 0,
                PathSpeedOverride = speed,
                ParabolicGravity = gravity,
                ParabolicStartPoint = parabolicStartPoint == default ? Position : parabolicStartPoint,
                KeepFacingDirection = keepFacingLastDirection,
                FollowNetID = 0,
                FollowDistance = 0,
                MoveBackBy = 0,
                FollowTravelTime = 0,
                MovementName = movementName,
                MovementOrdersType = movementOrdersType,
                Caster = caster ?? Owner
            };

            if (lockActions)
            {
                MovementParameters.SetStatus = StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove;
            }

            SetForceMovementState(true, MoveStopReason.ForceMovement);

            // Movement is networked this way instead — WaypointGroupWithSpeed (0x64) is Riot's
            // ONLY dash wire (13849x across 38 replays; WaypointListHeroWithSpeed 0x83 is sent 0x).
            _game.PacketNotifier.NotifyWaypointGroupWithSpeed(Owner);
            _movementUpdated = false;

            if (wallHit)
            {
                ApiEventManager.OnCollisionTerrain.Publish(Owner);
            }
        }
    }
}
