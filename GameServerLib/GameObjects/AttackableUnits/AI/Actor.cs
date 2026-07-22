using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
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
        private Vector2 Position => Owner.Position;
        private uint NetId => Owner.NetId;
        private TeamId Team => Owner.Team;
        private StatusFlags Status => Owner.Status;
        private float PathfindingRadius => Owner.PathfindingRadius;
        private float GetMoveSpeed() => Owner.GetMoveSpeed();
        private bool _inHardCollision { get => Owner.InHardCollision; set => Owner.InHardCollision = value; }
        private bool _inBodyContact { get => Owner.InBodyContact; set => Owner.InBodyContact = value; }
        private int _stuckGhostFrames { get => Owner.StuckGhostFrames; set => Owner.StuckGhostFrames = value; }
        private Vector2 _unreplicatedDrift { get => Owner.UnreplicatedDrift; set => Owner.UnreplicatedDrift = value; }

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
        private ForceMovementParameters MovementParameters => Owner.MovementParameters;
        private bool SetWaypoints(NavigationPath p) => Owner.SetWaypoints(p);
        private void SetPosition(Vector2 vec, bool repath) => Owner.SetPosition(vec, repath);

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
    }
}
