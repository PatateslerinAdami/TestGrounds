using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerCore.Scripting.CSharp.BehaviorTree;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AIScripts
{
    public class LaneMinionAI : IAIScript
    {
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData
        {
            HandlesCallsForHelp = true
        };

        private LaneMinion _laneMinion;
        private Node _behaviorTree;

        private float _thinkTimer = 0f;
        private float _localTime = 0f;
        private float _timeSinceLastAttack = 0f;
        private int _currentWaypointIndex = 0;

        private Dictionary<uint, float> _temporaryIgnored = new Dictionary<uint, float>();
        private Dictionary<AttackableUnit, int> _unitsAttackingAllies = new Dictionary<AttackableUnit, int>();
        private bool _callsForHelpMayBeCleared = false;
        private static readonly Random _rnd = new Random();
        private bool _wasCC = false;

        public void OnActivate(ObjAIBase owner)
        {
            _laneMinion = owner as LaneMinion;
            _thinkTimer = (float)_rnd.NextDouble() * 200f;

            _behaviorTree = new Selector(
                new Sequence(
                    new ActionNode(CheckHasTarget),
                    new ActionNode(OrderAttackTarget)
                ),
                new ActionNode(ScanForTargets),
                new ActionNode(FollowLaneWaypoints)
            );
        }

        public void OnUpdate(float diff)
        {
            _localTime += diff;

            if (_laneMinion == null || _laneMinion.IsDead || _laneMinion.IsAIPaused())
                return;

            if (!_laneMinion.CanIssueMoveOrders())
            {
                _wasCC = true;
                return;
            }

            if (_wasCC)
            {
                _wasCC = false;
                _thinkTimer = 200f;
            }

            if (_laneMinion.IsAttacking || _laneMinion.TargetUnit == null)
            {
                _timeSinceLastAttack = 0f;
            }
            else
            {
                _timeSinceLastAttack += diff;
            }

            if (_laneMinion.StuckTime > 0.25f && _laneMinion.TargetUnit != null)
            {
                Ignore(_laneMinion.TargetUnit, 100f);
                _laneMinion.SetTargetUnit(null, true);
                _laneMinion.StuckTime = 0f;

                _thinkTimer = 200f;
            }

            _thinkTimer += diff;
            if (_thinkTimer >= 200f)
            {
                _thinkTimer = 0f;
                FilterTemporaryIgnoredList();
                _behaviorTree.Evaluate();

                if (_callsForHelpMayBeCleared)
                {
                    _callsForHelpMayBeCleared = false;
                    _unitsAttackingAllies.Clear();
                }
            }
        }

        public void OnCallForHelp(AttackableUnit attacker, AttackableUnit victim)
        {
            if (_unitsAttackingAllies != null)
            {
                int priority = Math.Min(_unitsAttackingAllies.GetValueOrDefault(attacker, (int)ClassifyUnit.DEFAULT), (int)_laneMinion.ClassifyTarget(attacker, victim));
                _unitsAttackingAllies[attacker] = priority;
            }
        }

        private NodeState CheckHasTarget()
        {
            var currentTarget = _laneMinion.TargetUnit;

            if (!IsValidTarget(currentTarget))
            {
                if (currentTarget != null)
                {
                    _laneMinion.CancelAutoAttack(false, true);
                    _laneMinion.SetTargetUnit(null, true);
                }
                return NodeState.Failure;
            }

            if (_timeSinceLastAttack >= 4000f)
            {
                Ignore(currentTarget);
                _laneMinion.SetTargetUnit(null, true);
                return NodeState.Failure;
            }

            return NodeState.Success;
        }

        private NodeState OrderAttackTarget()
        {
            if (_laneMinion.MoveOrder != OrderType.AttackTo)
            {
                _laneMinion.UpdateMoveOrder(OrderType.AttackTo);
            }
            return NodeState.Success;
        }

        private float ScoreTarget(AttackableUnit u, bool isCurrentTarget)
        {
            float score = 0f;

            int priority = _unitsAttackingAllies.ContainsKey(u)
                ? _unitsAttackingAllies[u]
                : (int)_laneMinion.ClassifyTarget(u);
            score += priority * 100f;

            int targetingAllies = u.TargetedBy.Count(ally =>
                ally.Team == _laneMinion.Team &&
                ally != _laneMinion &&
                !ally.IsDead);
            score += targetingAllies * 150f;

            float attackRange = _laneMinion.Stats.Range.Total + u.CollisionRadius;
            float distSq = Vector2.DistanceSquared(_laneMinion.Position, u.Position);
            bool inAttackRange = distSq <= (attackRange * attackRange);

            if (inAttackRange)
            {
                score -= 1000f;
            }
            else
            {
                score += (float)Math.Sqrt(distSq);
            }

            if (isCurrentTarget && inAttackRange)
            {
                score -= 300f;
            }

            return score;
        }

        private NodeState ScanForTargets()
        {
            _callsForHelpMayBeCleared = true;
            AttackableUnit bestTarget = null;
            float bestScore = float.MaxValue;

            var potentialTargets = _unitsAttackingAllies.Keys.ToList();
            if (potentialTargets.Count == 0)
            {
                potentialTargets = ApiFunctionManager.GetUnitsInRange(
                    _laneMinion,
                    _laneMinion.Position,
                    _laneMinion.Stats.AcquisitionRange.Total,
                    true,
                    SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectTurrets
                );
            }

            foreach (var u in potentialTargets)
            {
                if (IsValidTarget(u) && !_temporaryIgnored.ContainsKey(u.NetId))
                {
                    bool isCurrent = (u == _laneMinion.TargetUnit);
                    float score = ScoreTarget(u, isCurrent);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestTarget = u;
                    }
                }
            }

            if (bestTarget != null)
            {
                if (_laneMinion.TargetUnit != bestTarget)
                {
                    _laneMinion.SetTargetUnit(bestTarget, true);
                }
                _timeSinceLastAttack = 0f;
                return NodeState.Success;
            }

            return NodeState.Failure;
        }

        private NodeState FollowLaneWaypoints()
        {
            if (_laneMinion.PathingWaypoints == null || _laneMinion.PathingWaypoints.Count == 0) return NodeState.Failure;

            while (_currentWaypointIndex < _laneMinion.PathingWaypoints.Count && WaypointReached())
            {
                _currentWaypointIndex++;
            }

            if (_currentWaypointIndex < _laneMinion.PathingWaypoints.Count)
            {
                Vector2 nextWaypoint = _laneMinion.PathingWaypoints[_currentWaypointIndex];

                Vector2 currentDestination = _laneMinion.Waypoints.LastOrDefault();
                if (Vector2.DistanceSquared(currentDestination, nextWaypoint) > 2500f)
                {
                    var path = ApiFunctionManager.GetPath(_laneMinion.Position, nextWaypoint, _laneMinion.PathfindingRadius);
                    if (path == null || path.Count == 0) path = new List<Vector2> { _laneMinion.Position, nextWaypoint };
                    _laneMinion.SetWaypoints(path);
                }

                if (_laneMinion.MoveOrder != OrderType.AttackMove) _laneMinion.UpdateMoveOrder(OrderType.AttackMove);
                return NodeState.Running;
            }

            if (_laneMinion.MoveOrder != OrderType.Stop) _laneMinion.UpdateMoveOrder(OrderType.Stop);
            return NodeState.Success;
        }

        private bool IsValidTarget(AttackableUnit u)
        {
            return u != null && !u.IsDead && u.Team != _laneMinion.Team && Vector2.DistanceSquared(_laneMinion.Position, u.Position) < (_laneMinion.Stats.AcquisitionRange.Total * _laneMinion.Stats.AcquisitionRange.Total) && u.IsVisibleByTeam(_laneMinion.Team) && u.Status.HasFlag(StatusFlags.Targetable) && !ApiFunctionManager.UnitIsProtectionActive(u);
        }

        private void Ignore(AttackableUnit unit, float time = 5000f)
        {
            _temporaryIgnored[unit.NetId] = _localTime + time;
        }

        private void FilterTemporaryIgnoredList()
        {
            var keysToRemove = _temporaryIgnored.Where(pair => pair.Value <= _localTime).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove) _temporaryIgnored.Remove(key);
        }

        private bool WaypointReached()
        {
            return Vector2.DistanceSquared(_laneMinion.Position, _laneMinion.PathingWaypoints[_currentWaypointIndex]) <= 150f * 150f;
        }
    }
}