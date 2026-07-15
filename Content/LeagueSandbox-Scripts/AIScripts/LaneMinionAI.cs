using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Path;
using LeagueSandbox.GameServer.Logging;

namespace AIScripts
{
    // Clean, Riot-faithful lane minion AI — a direct mirror of Scripts/Minion.lua + Aggro.lua +
    // Shared/Scripts/Minions.lua on top of BaseAIScript. Nothing is carried over from the previous
    // (broken) implementation: targeting is the engine FindTargetInAcR, movement is a state machine
    // driven by named timers + event hooks, exactly as the Lua does.
    //
    // Forward-nav (Riot SetStateAndMoveToForwardNav -> NavPointManager) is a faithful port of
    // NavPointManager::GetNextNavLocIter (see LaneNavPointManager): a position-driven, stateless
    // selection of the next lane point, capped at the next alive enemy turret's waypoint
    // (GetMaxAllowedWaypointIndex). CC is buff-driven in our server (Fear/Flee/Taunt force movement),
    // so — unlike the Lua — the AI does not drive CC movement; it pauses while crowd-controlled and
    // reevaluates when CC ends.
    public class LaneMinionAI : BaseAIScript
    {
        public LaneMinionAI()
        {
            AIScriptMetaData.HandlesCallsForHelp = true;
        }

        private const float MAX_ENGAGE_DISTANCE = 2500f;
        private const float DELAY_FIND_ENEMIES = 0.25f;
        // S4 NavPointManager::sNearPointThreshold — distance at which a forward-nav point counts as
        // reached and the minion advances to the next. Patch 4.20 = 500 (sNearPointThresholdSq =
        // 250000); note 4.17 used 150. We target 4.20, so 500. Flat threshold, NOT collision-relative.
        private const float NEAR_POINT_THRESHOLD = 500f;
        // Within this of a FINAL (non-advancing) target — turret / nexus cap — stop re-issuing so the
        // minion settles instead of churning a fresh path per tick (Riot NavPointManager::IsStillOldPosition).
        private const float ARRIVE_STOP = 150f;

        private LaneMinion _minion;
        private Vector2 _lastOrderedWaypoint = new Vector2(float.NaN, float.NaN);
        private int _navIndex;
        // Per-capping-turret committed approach cell (distinct-cell fix, see the capped branch):
        // computed ONCE per turret via the stand-cell reservation handshake, reused until the
        // capping turret changes (next tower after a kill resets it via the NetId mismatch).
        private Vector2 _cappedStandTarget;
        private uint _cappedStandTurretNetId;

        // Whether body-contact with an enemy makes the minion engage it (Aggro.lua OnCollisionEnemy).
        // BaronMinionAI overrides this to false (its OnCollisionEnemy is a no-op).
        protected virtual bool EngagesOnCollision => true;

        // ---------------- Lifecycle (Minions.lua OnInit) ----------------

        protected override void OnActivateBehavior()
        {
            _minion = Owner as LaneMinion;
            if (_minion == null)
            {
                return;
            }

            // Firing is driven by the shared AutoAttackComponent (in attack range → on, past cancel range
            // → off), not by the engine auto-firing on "target set". This script keeps SELECTION + chase
            // intent only. The engine's idealRange swing gate (full edge-to-edge attack range) is
            // unchanged, so WHERE it fires is identical.

            InitTimer("TimerFindEnemies", DELAY_FIND_ENEMIES, true, TimerFindEnemies);
            InitTimer("TimerMoveForward", 0f, true, TimerMoveForward);
            InitTimer("TimerAntiKite", 4f, false, TimerAntiKite);
            StopTimer("TimerAntiKite");

            // CC movement is driven by the Fear/Flee/Taunt buffs; just reevaluate when it ends.
            Subscribe(AIEvent.OnFearEnd, _ => FindTargetOrMove());
            Subscribe(AIEvent.OnTauntEnd, _ => OnTauntEnded());
            Subscribe(AIEvent.OnCharmEnd, _ => FindTargetOrMove());

            // Aggro.lua OnCanMove / OnCanAttack: a movement/attack-disabling lock just ended
            // (stun, root, snare, channel lock — anything that drops the capability) → reset to
            // idle and re-decide immediately (lua body: NetSetState(AI_IDLE); FindTargetOrMove())
            // instead of resuming the stale pre-CC intent on the next 0.25s sweep. Fear/Taunt/
            // Charm additionally have their dedicated End events above; a double re-acquire on
            // overlap is harmless (FindTargetOrMove re-picks / keeps forward nav idempotently).
            Subscribe(AIEvent.OnCanMove, _ => OnCapabilityRegained());
            Subscribe(AIEvent.OnCanAttack, _ => OnCapabilityRegained());

            // Aggro.lua OnCollisionEnemy / OnTargetLost / OnPathToTargetBlocked — immediate
            // reevaluation on body-contact with an enemy, when the engine drops our target, and
            // when navigation to the target fails (the 0.25s scan is the fallback for all three).
            ApiEventManager.OnCollision.AddListener(this, _minion, OnOwnerCollision, false);
            ApiEventManager.OnTargetLost.AddListener(this, _minion, OnOwnerTargetLost, false);
            ApiEventManager.OnPathToTargetBlocked.AddListener(this, _minion, OnOwnerPathToTargetBlocked, false);

            NetSetState(AIState.AI_IDLE);
        }

        // While crowd-controlled / dashing the buff/forced-movement system owns movement; the AI
        // must not issue competing orders.
        private bool IsCrowdControlled()
        {
            const StatusFlags cc = StatusFlags.Feared | StatusFlags.Charmed | StatusFlags.Taunted;
            return (_minion.Status & cc) != 0 || _minion.IsForceMoved;
        }

        // Lane minions are always Hostile (4.20 has no spawn dormancy) — the lane clash emerges from
        // movement + AcquisitionRange, not a wake gate. This check now only excludes RunInFear (CC
        // fear-flee), during which the minion must not acquire. See MinionRoamState.
        private bool CanAggro()
        {
            return _minion.RoamState == MinionRoamState.Hostile;
        }

        // State-gated auto-attack (option C, Minion.lua/Aggro.lua TimerFindEnemies → TurnOnAutoAttack):
        // a lane minion only swings while ENGAGED — AI_ATTACKMOVE_ATTACKING (acquired / CFH / collision /
        // taunt-end target) or AI_TAUNTED (taunt forces it onto the taunter via the CrowdControlComponent).
        // While pushing (AI_ATTACKMOVESTATE) or idle it has no engaged target, so this gates OUT the
        // "stale target still set while walking" case (it re-engages → ATTACKMOVE_ATTACKING on the next
        // 0.25s sweep before swinging). Every attack path sets the target via SetStateAndCloseToTarget(
        // AI_ATTACKMOVE_ATTACKING, …) — state + target atomically — so this is behaviour-identical in active
        // combat. Inherited by BaronMinionAI (same state machine). Charm carries no target → never fires.
        public override bool AutoAttackStatePermits()
        {
            return CurrentState == AIState.AI_ATTACKMOVE_ATTACKING
                || CurrentState == AIState.AI_TAUNTED;
        }

        // ---------------- Timers (Minion.lua) ----------------

        // 0.25s — acquire / maintain a target.
        private void TimerFindEnemies()
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled())
            {
                return;
            }

            if (CurrentState == AIState.AI_ATTACKMOVESTATE)
            {
                if (!CanAggro())
                {
                    return;
                }

                AttackableUnit target = FindTargetInAcR();
                if (target != null)
                {
                    SetStateAndCloseToTarget(AIState.AI_ATTACKMOVE_ATTACKING, target);
                    ResetAndStartTimer("TimerAntiKite");
                }
                return;
            }

            if (CurrentState == AIState.AI_ATTACKMOVE_ATTACKING)
            {
                AttackableUnit target = _minion.TargetUnit;
                if (target == null || target.IsDead
                    || MAX_ENGAGE_DISTANCE < Vector2.Distance(_minion.Position, target.Position))
                {
                    FindTargetOrMove();
                    return;
                }

                // Faithful Minion.lua / Aggro.lua TimerFindEnemies: while the current target is in
                // ATTACK RANGE, reset the anti-kite timer every 0.25s sweep. The 4s TimerAntiKite is
                // meant to fire ONLY for a minion that has been CHASING a target it can never reach
                // (true kiting); a minion that is actually trading hits in range must keep cancelling
                // it, or it spuriously drops a target it is busy killing. We were missing this reset,
                // so any minion still nudging (reposition / clash shuffle) while in range eventually
                // hit 4s and switched targets for no reason.
                if (TargetInAttackRange(target))
                {
                    ResetAndStartTimer("TimerAntiKite");
                }
            }
        }

        // Riot TargetInAttackRange(): center-to-center distance within auto-attack range plus both
        // collision radii (Stats.Range is the edge-to-edge auto range — same expansion the engine's
        // auto-attack gate uses in ObjAIBase).
        private bool TargetInAttackRange(AttackableUnit target)
        {
            float range = _minion.Stats.Range.Total + target.CollisionRadius + _minion.CollisionRadius;
            return Vector2.DistanceSquared(_minion.Position, target.Position) <= range * range;
        }

        // Every tick — keep pushing the lane while not engaged.
        private void TimerMoveForward()
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled())
            {
                return;
            }

            if (CurrentState == AIState.AI_IDLE)
            {
                FindTargetOrMove();
            }
            else if (CurrentState == AIState.AI_ATTACKMOVESTATE)
            {
                SetStateAndMoveToForwardNav();
            }
        }

        // 4s — if stuck moving toward a target without landing a hit, ignore it briefly and re-acquire.
        private void TimerAntiKite()
        {
            if (_minion == null || _minion.IsDead)
            {
                return;
            }

            if (CurrentState == AIState.AI_ATTACKMOVE_ATTACKING && IsMoving())
            {
                LogTargetEvent("antikite-drop", _minion.TargetUnit);
                AddToIgnore(0.1f);
                FindTargetOrMove();
            }
        }

        // S4 obj_AI_Minion::IsBetterThanGivenTarget tournament: IsBetterTargetThanMinion returns
        // EQUAL, so a minion never sub-ranks other minions by type — equal-priority candidates
        // fall through to the distance tiebreak in FindTargetInAcR. Collapse the lane-minion
        // subtypes (super/cannon/caster/melee) into the single MINION bucket so the *nearest*
        // enemy minion is picked, not the highest-"value" one. Everything else keeps the engine
        // ClassifyTarget ordering (champions/turrets/buildings stay deprioritised below minions,
        // which is what keeps a pushing minion from peeling onto a nearby champion in lane).
        protected override int GetTargetPriority(AttackableUnit target)
        {
            ClassifyUnit c = _minion.ClassifyTarget(target);
            switch (c)
            {
                case ClassifyUnit.SUPER_OR_CANNON_MINION:
                case ClassifyUnit.CASTER_MINION:
                case ClassifyUnit.MELEE_MINION:
                    return (int)ClassifyUnit.MINION;
                default:
                    return (int)c;
            }
        }

        // ---------------- Decision (Minion.lua FindTargetOrMove) ----------------

        private void FindTargetOrMove()
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled())
            {
                return;
            }

            AttackableUnit target = CanAggro() ? FindTargetInAcR() : null;
            if (target != null)
            {
                // Minion.lua LastAutoAttackFinished() guard: never re-issue the attack order mid
                // auto-attack windup — SetTargetUnit resets the AA pipeline on a target switch, so
                // doing it now would cancel the committed swing. Defer to the next 0.25s scan.
                // (IsAttacking == true means an auto-attack windup is in progress.)
                if (_minion.IsAttacking)
                {
                    return;
                }

                LogTargetEvent("acquire", target);
                SetStateAndCloseToTarget(AIState.AI_ATTACKMOVE_ATTACKING, target);
                ResetAndStartTimer("TimerAntiKite");
            }
            else
            {
                SetStateAndMoveToForwardNav();
                StopTimer("TimerAntiKite");
            }
        }

        // Minion.lua OnTauntEnd: keep attacking the taunter if it's still valid (and restart the
        // anti-kite timer), only re-acquiring when there's no taunt target. Our taunt is buff-driven:
        // the CrowdControlComponent set TargetUnit = taunter on taunt begin and does NOT clear it on
        // end (unlike fear/charm), and the AI yields while Taunted, so TargetUnit still holds the
        // taunter here. (A plain FindTargetOrMove usually re-acquires the same adjacent taunter, but
        // this matches the Lua's explicit "stay on the taunter" + anti-kite restart.)
        private void OnTauntEnded()
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled())
            {
                return;
            }

            AttackableUnit tauntTarget = _minion.TargetUnit;
            if (tauntTarget != null && !tauntTarget.IsDead && tauntTarget.Team != _minion.Team)
            {
                SetStateAndCloseToTarget(AIState.AI_ATTACKMOVE_ATTACKING, tauntTarget);
                ResetAndStartTimer("TimerAntiKite");
            }
            else
            {
                FindTargetOrMove();
            }
        }

        // Aggro.lua OnCallForHelp: respond to an ally's attacker while pushing/engaging.
        protected override void OnCallForHelpBehavior(AttackableUnit attacker, AttackableUnit victium)
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled() || attacker == null || !CanAggro())
            {
                return;
            }

            // FRONT-OF-WAVE TARGETING (stage A, 2026-06-21): do NOT peel onto a MINION attacker.
            // A minion attacking us from behind/beside its own front line is exactly the target that,
            // chased, pulls us off the front of the wave toward the backline — driving same-target
            // convergence AND the behind-wave routing (the chase path then routes AROUND the enemy
            // front to reach it). Minion-vs-minion targeting stays the nearest FRONT enemy chosen by
            // FindTargetInAcR (the 0.25s sweep re-acquires the nearest if we currently have none).
            // We STILL respond to non-minion threats (champion aggro) — that's the real purpose of
            // CallForHelp. Verify in-game: faithfulness of the minion-attacker filter is the open
            // question (Riot's wave doesn't mass-converge on backline minions, which supports it).
            if (attacker is Minion)
            {
                return;
            }

            if (CurrentState == AIState.AI_ATTACKMOVESTATE || CurrentState == AIState.AI_ATTACKMOVE_ATTACKING)
            {
                SetStateAndCloseToTarget(AIState.AI_ATTACKMOVE_ATTACKING, attacker);
                ResetAndStartTimer("TimerAntiKite");
            }
        }

        // Aggro.lua OnCollisionEnemy: engage an enemy we bump into. Restricted to the pushing
        // states (not yet locked onto a target) so it never yanks a minion off its current target
        // every time it grazes another body in a clash — the 0.25s FindTargetInAcR keeps the
        // best target once engaged.
        private void OnOwnerCollision(GameObject self, GameObject collider)
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled() || !CanAggro())
            {
                return;
            }

            if (!EngagesOnCollision || CurrentState == AIState.AI_ATTACKMOVE_ATTACKING)
            {
                return;
            }

            if (collider is not AttackableUnit u || u.IsDead || u.Team == _minion.Team
                || !u.Status.HasFlag(StatusFlags.Targetable) || !u.IsVisibleByTeam(_minion.Team))
            {
                return;
            }

            SetStateAndCloseToTarget(AIState.AI_ATTACKMOVE_ATTACKING, u);
            ResetAndStartTimer("TimerAntiKite");
        }

        // Aggro.lua OnCanMove / OnCanAttack handler body: NetSetState(AI_IDLE) + FindTargetOrMove.
        // The idle reset matters: Riot's minion re-picks the NEAREST target after a CC lock ends
        // rather than resuming whatever it was doing before the lock.
        private void OnCapabilityRegained()
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled())
            {
                return;
            }

            NetSetState(AIState.AI_IDLE);
            FindTargetOrMove();
        }

        // Aggro.lua OnTargetLost: the engine dropped our target (died / went invalid) — re-acquire
        // immediately instead of waiting for the next 0.25s tick.
        private void OnOwnerTargetLost(ObjAIBase owner, AttackableUnit lostTarget, TargetLostReason reason)
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled())
            {
                return;
            }

            if (CurrentState == AIState.AI_ATTACKMOVE_ATTACKING)
            {
                LogTargetEvent("lost-drop:" + reason, lostTarget);
                FindTargetOrMove();
            }
        }

        // Aggro.lua / Shared/Minions.lua OnPathToTargetBlocked: the engine couldn't path to our
        // attack target (unreachable / partial path only). Briefly ignore it and re-acquire so the
        // minion peels onto a reachable target instead of grinding against the obstacle until the
        // 4s anti-kite timer fires. Identical body to the Lua handler.
        private void OnOwnerPathToTargetBlocked(AttackableUnit blockedTarget)
        {
            if (_minion == null || _minion.IsDead || IsCrowdControlled())
            {
                return;
            }

            if (CurrentState == AIState.AI_ATTACKMOVE_ATTACKING)
            {
                LogTargetEvent("pathblocked-drop", blockedTarget);
                AddToIgnore(0.1f);
                FindTargetOrMove();
            }
        }

        // ---------------- Forward navigation (Riot SetStateAndMoveToForwardNav / NavPointManager) ----------------

        // Faithful port of NavPointManager::GetNextNavLocIter (see LaneNavPointManager): a position-
        // driven, stateless selection of the next lane point — robust to being shoved off-axis — capped
        // at the next alive enemy turret's waypoint (GetMaxAllowedWaypointIndex). Paths to the chosen
        // point through the blocker-aware A* so it arcs around building footprints.
        private void SetStateAndMoveToForwardNav()
        {
            NetSetState(AIState.AI_ATTACKMOVESTATE);

            IReadOnlyList<Vector2> lane = _minion.PathingWaypoints;
            if (lane == null || lane.Count == 0)
            {
                return;
            }

            int maxIndex = _minion.GetMaxAllowedWaypointIndex();
            Vector2? next = LaneNavPointManager.GetNextNavTarget(
                _minion.SpawnPosition, lane, maxIndex,
                _minion.Position, NEAR_POINT_THRESHOLD, ref _navIndex);

            if (next == null)
            {
                // Held at the first alive enemy turret's regroup point, or arrived at the nexus end.
                if (PathLogger.Enabled)
                {
                    PathLogger.LogNav(ApiMapFunctionManager.GameTime(), _minion.NetId, _navIndex, maxIndex,
                        true, 0f, _minion.Position.X, _minion.Position.Y, "stop");
                }
                _minion.UpdateMoveOrder(OrderType.Stop);
                return;
            }

            Vector2 target = next.Value;

            // Inline-turret fidelity (Riot GetNextNavLocIter halts at the enemy turret nav node):
            // once the cap waypoint is reached, target the capping turret instead of the nearest
            // lane waypoint, so the minion always closes into its acquisition range.
            //
            // DISTINCT-CELL FIX (2026-07-05, tt120 "casters path into each other, oscillate, end
            // STACKED on one attack position"): the old code targeted cappingTurret.Position RAW —
            // the IDENTICAL coordinate for every capped wave member. This is a plain forward-nav
            // MOVE, so none of the attack-stand machinery (GCAP spiral, reservations, CommitStand)
            // ever ran: paths converged geometrically onto one point, the casters fused during the
            // walk (wire: dests 10-24u apart at 111s, identical at 112.5s, d=0.0 stacked from
            // 114s for 10+s) and the in-range stops froze the stack. Route the shared point
            // through the stand-cell reservation handshake ONCE per capping turret (cached — a
            // per-tick CommitStand would relocate against its own reservation churn and re-issue
            // every evaluation): each member commits a distinct get-to-able cell at the tower.
            if (_navIndex >= maxIndex)
            {
                var cappingTurret = _minion.GetCappingTurret();
                if (cappingTurret != null)
                {
                    if (_cappedStandTurretNetId != cappingTurret.NetId)
                    {
                        _cappedStandTarget = CommitAttackStandPosition(_minion, cappingTurret.Position);
                        // ACQUISITION CLAMP (2026-07-05, user bug "minions stop on the lane just
                        // short of turret range"): the reservation relocation is spatially
                        // unconstrained — under crowding the Nth ring cell can land OUTSIDE this
                        // minion's acquisition range of the turret. The minion then arrives there,
                        // atFinalTarget correctly suppresses further re-issues (riot-emission
                        // model: no lookahead top-up prods it anymore either), targeting never
                        // sees the turret, and it stands on the lane forever. A cell that cannot
                        // acquire the turret falls back to the raw center (one minion risking
                        // ring overlap beats a pacifist).
                        float acq = _minion.GetAcquisitionRange();
                        if (Vector2.DistanceSquared(_cappedStandTarget, cappingTurret.Position) > acq * acq)
                        {
                            _cappedStandTarget = cappingTurret.Position;
                        }
                        _cappedStandTurretNetId = cappingTurret.NetId;
                    }
                    target = _cappedStandTarget;
                }
                else
                {
                    _cappedStandTurretNetId = 0;
                }
            }

            // Re-issue the path only when the forward-nav target actually changed, OR the path was
            // consumed while the minion is still meaningfully SHORT of the target (a partial/blocked
            // route — worth one retry).
            //
            // The old gate also re-issued on a bare IsPathEnded(), which re-fired EVERY tick once a
            // minion ARRIVED at its current nav point, or was held at the turret-cap target (target
            // unchanged + path consumed). With the unit jittering ~Position each tick (collision),
            // GetPath returned a slightly different short path every call, so SetWaypoints broadcast a
            // fresh same-goal WaypointGroup per tick — measured at 6-12x Riot's reanchor rate (wpath,
            // 2026-06-28, docs/.. memory project_lane_minion_snap_measurement). The client hard-snaps
            // to wp0 on each receive (ActorClient.cpp:169), so it reads as the minion stuttering/snapping
            // in place. Riot's NavPointManager::IsStillOldPosition (AINavPointManager.cpp:615-689)
            // suppresses re-issue while the unit is still validly sitting at / progressing toward the
            // same nav point. Gating the IsPathEnded branch behind "not yet arrived" reproduces that:
            // arrival now waits for the next nav-point advance (targetChanged) to re-issue exactly once,
            // instead of churning per tick. The "still short" escape keeps a genuinely partial/blocked
            // transit retrying (the 0.25s sweep + OnPathToTargetBlocked also cover that case).
            bool atFinalTarget =
                Vector2.DistanceSquared(_minion.Position, target) <= ARRIVE_STOP * ARRIVE_STOP;
            // RIOT EMISSION MODEL (2026-07-05, wobble invention audit — VERIFIED in-game: the
            // marching wobble/"Rumpathen" disappeared with this change): ONE full path per lane
            // leg, no mid-leg top-up. Riot issues the whole leg to the nav node in one path
            // (wire: n=2, 776-1341u) and re-issues only on the nav-node advance — the client
            // walks the whole leg undisturbed. The previous invented rolling-segment scheme
            // (700u truncation + 300u lookahead top-up, see git history) replaced the client's
            // path mid-run several times per leg; every replace hard-snaps the client to wp0
            // (ActorClient.cpp:169) and resets its local collision separation — that churn WAS
            // the visible wobble. Re-issue ONLY when the forward-nav target changes (node
            // advance / turret cap) or when the issued path was consumed while still short of
            // the target (partial/blocked route — worth one retry; without it a blocked minion
            // would stand until the next node).
            bool pathConsumedShort = _minion.IsPathEnded() && !atFinalTarget;
            if (_lastOrderedWaypoint != target || pathConsumedShort)
            {
                // Diagnostic reason for PATH_LOG: which clause opened the gate.
                string reason = _lastOrderedWaypoint != target ? "fwd:target" : "fwd:pathend";
                // TERRAIN-ONLY path (Riot-faithful, replay-verified 2026-06-30): the lane walk issues a
                // straight path to the next nav point, routing around TERRAIN only — NOT around allied
                // bodies. Riot's wire proves this: in a stacked wave 72% of minion paths are n=1 (a single
                // "walk to X" waypoint) and only 13% are n>=3; the real client de-clumps the pile locally
                // with its OWN collision sim. Our previous full actor-blocking (ignoreTargetRadius:0)
                // re-routed a differently-bent A* path around the SHIFTING neighbours on every re-issue
                // (measured 51% n>=3 vs Riot 17%, n=4 25% vs 4.6%); the client hard-snaps to each new wp0
                // (ActorClient.cpp:169) → the "wusseln" / slide-together / weird-path artifacts the wire
                // capture flagged. A terrain-only line to a FIXED nav point is shape-stable across
                // re-issues (terrain doesn't move) → wp0 just advances with the minion, no snap. Then
                // TRUNCATE to a short rolling segment so a long lane leg still refreshes via IsPathEnded
                // as the minion advances. (Server-side de-clumping isn't lost faithfully: the steer
                // returns early for n=2 and the push needs Count>=4 — exactly Riot's gating — so straight
                // lane walks rely on client separation just like Riot's do.)
                // Full leg, no truncation — Riot sends the whole leg to the nav node in one
                // issue (see the emission-model comment on the gate above).
                List<Vector2> path = GetPath(_minion.Position, target)
                                     ?? new List<Vector2> { _minion.Position, target };
                _minion.SetWaypoints(path, pathReason: reason);
                _lastOrderedWaypoint = target;

                if (PathLogger.Enabled)
                {
                    PathLogger.LogNav(ApiMapFunctionManager.GameTime(), _minion.NetId, _navIndex, maxIndex,
                        _navIndex >= maxIndex, MathF.Sqrt(Vector2.DistanceSquared(_minion.Position, target)),
                        _minion.Position.X, _minion.Position.Y, "issue:" + reason);
                }
            }
            else if (PathLogger.Enabled)
            {
                PathLogger.LogNav(ApiMapFunctionManager.GameTime(), _minion.NetId, _navIndex, maxIndex,
                    _navIndex >= maxIndex, MathF.Sqrt(Vector2.DistanceSquared(_minion.Position, target)),
                    _minion.Position.X, _minion.Position.Y, "hold");
            }

            _minion.UpdateMoveOrder(OrderType.MoveTo);
        }

        // Diagnostic (opt-in PATH_LOG): record a targeting acquire/drop with the target's type, netId,
        // reachability (CheckIsGetToAble) and distance — to diagnose the attack↔forward oscillation
        // near a turret (does it keep re-acquiring an UNREACHABLE target, and which trigger drops it).
        private void LogTargetEvent(string action, AttackableUnit t)
        {
            if (!PathLogger.Enabled || _minion == null)
            {
                return;
            }
            uint tnet = t?.NetId ?? 0;
            string ttype = t?.GetType().Name ?? "none";
            bool reach = t != null && CheckIsGetToAble(_minion, t.Position);
            float dist = t != null ? MathF.Sqrt(Vector2.DistanceSquared(_minion.Position, t.Position)) : 0f;
            // Enrich the action with THIS minion's spawn type + its edge-to-edge attack range, so the
            // capture can show whether a MELEE minion is standing/engaging far beyond its own range
            // (= "stops before it has reached / before vision") vs a caster legitimately at ~550u.
            float selfRange = t != null
                ? _minion.Stats.Range.Total + t.CollisionRadius + _minion.CollisionRadius
                : _minion.Stats.Range.Total;
            string enriched = $"{action}|{_minion.MinionSpawnType}|r{selfRange:F0}";
            PathLogger.LogTarget(ApiMapFunctionManager.GameTime(), _minion.NetId, tnet, ttype, reach, dist,
                _minion.Position.X, _minion.Position.Y, enriched);
        }

    }
}
