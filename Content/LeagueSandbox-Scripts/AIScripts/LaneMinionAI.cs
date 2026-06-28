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

        private LaneMinion _minion;
        private Vector2 _lastOrderedWaypoint = new Vector2(float.NaN, float.NaN);
        private int _navIndex;

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
            // intent only. The engine's idealRange swing gate (ChasingAttackRangePercent) is unchanged, so
            // WHERE it fires is identical.

            InitTimer("TimerFindEnemies", DELAY_FIND_ENEMIES, true, TimerFindEnemies);
            InitTimer("TimerMoveForward", 0f, true, TimerMoveForward);
            InitTimer("TimerAntiKite", 4f, false, TimerAntiKite);
            StopTimer("TimerAntiKite");

            // CC movement is driven by the Fear/Flee/Taunt buffs; just reevaluate when it ends.
            Subscribe(AIEvent.OnFearEnd, _ => FindTargetOrMove());
            Subscribe(AIEvent.OnTauntEnd, _ => OnTauntEnded());
            Subscribe(AIEvent.OnCharmEnd, _ => FindTargetOrMove());

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
                _minion.UpdateMoveOrder(OrderType.Stop);
                return;
            }

            Vector2 target = next.Value;

            // Inline-turret fidelity (Riot GetNextNavLocIter halts at the enemy turret nav node):
            // once the cap waypoint is reached, target the capping turret's exact position instead
            // of the nearest lane waypoint, so the minion always closes into its acquisition range.
            if (_navIndex >= maxIndex)
            {
                var cappingTurret = _minion.GetCappingTurret();
                if (cappingTurret != null)
                {
                    target = cappingTurret.Position;
                }
            }

            // Re-issue the path only when the target point changed or the current path ran out.
            if (_lastOrderedWaypoint != target || _minion.IsPathEnded())
            {
                List<Vector2> path = GetPath(_minion.Position, target, _minion.PathfindingRadius)
                                     ?? new List<Vector2> { _minion.Position, target };
                _minion.SetWaypoints(path);
                _lastOrderedWaypoint = target;
            }

            _minion.UpdateMoveOrder(OrderType.MoveTo);
        }
    }
}
