using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerCore.Scripting.CSharp.BehaviorTree;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;
using MapScripts.Map8;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AIScripts
{
    public class OdinLaneMinionAI : IAIScript
    {
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData
        {
            HandlesCallsForHelp = true
        };

        private LaneMinion _laneMinion;
        private Node _behaviorTree;

        public InfoPoint CurrentTargetNode { get; set; }
        public bool IsClockwise { get; set; }

        private int _currentWaypointIndex = 0;
        private float _thinkTimer = 0f;
        private float _localTime = 0f;
        private float _timeSinceLastAttack = 0f;

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

            if (_timeSinceLastAttack >= 4000f && currentTarget.Model != "OdinNeutralGuardian")
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
                : (u.Model == "OdinNeutralGuardian" ? (int)ClassifyUnit.TURRET : (int)_laneMinion.ClassifyTarget(u));

            score += priority * 100f;

            int targetingAllies = u.TargetedBy.Count(ally =>
                ally.Team == _laneMinion.Team &&
                ally != _laneMinion &&
                !ally.IsDead);
            score += targetingAllies * 150f;

            float attackRange = _laneMinion.Stats.Range.Total + u.CollisionRadius;
            float distSq = Vector2.DistanceSquared(_laneMinion.Position, u.Position);
            bool inAttackRange = distSq <= (attackRange * attackRange);

            if (inAttackRange) score -= 1000f;
            else score += (float)Math.Sqrt(distSq);

            if (isCurrentTarget && inAttackRange) score -= 300f;

            return score;
        }

        private NodeState ScanForTargets()
        {
            _callsForHelpMayBeCleared = true;
            AttackableUnit bestTarget = null;
            float bestScore = float.MaxValue;

            var potentialTargets = ApiFunctionManager.GetUnitsInRange(
                _laneMinion,
                _laneMinion.Position,
                _laneMinion.Stats.AcquisitionRange.Total,
                true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectTurrets | SpellDataFlags.AffectNeutral
            );

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

            if (bestTarget == null && CurrentTargetNode != null && CurrentTargetNode.Point.Team != _laneMinion.Team)
            {
                float distSq = Vector2.DistanceSquared(_laneMinion.Position, CurrentTargetNode.Point.Position);
                float nodeAcqRange = 800f;

                if (distSq <= nodeAcqRange * nodeAcqRange || _currentWaypointIndex >= _laneMinion.PathingWaypoints.Count - 1)
                {
                    bestTarget = CurrentTargetNode.Point;
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

            if (_currentWaypointIndex >= _laneMinion.PathingWaypoints.Count)
            {
                if (CurrentTargetNode != null && CurrentTargetNode.Point.Team == _laneMinion.Team)
                {
                    AdvanceToNextRingNode();
                }
            }

            if (_currentWaypointIndex < _laneMinion.PathingWaypoints.Count)
            {
                Vector2 rawWaypoint = _laneMinion.PathingWaypoints[_currentWaypointIndex];
                Vector2 prevWaypoint = _currentWaypointIndex > 0
                    ? _laneMinion.PathingWaypoints[_currentWaypointIndex - 1]
                    : _laneMinion.Position;

                Vector2 segmentDir = rawWaypoint - prevWaypoint;
                Vector2 parallelDestination = rawWaypoint;

                if (segmentDir.LengthSquared() > 0.001f)
                {
                    Vector2 forward = Vector2.Normalize(segmentDir);
                    Vector2 lateral = new Vector2(-forward.Y, forward.X);

                    if (_laneMinion.Team == TeamId.TEAM_PURPLE)
                    {
                        lateral = -lateral;
                    }


                    if (!ApiFunctionManager.IsWalkable(parallelDestination.X, parallelDestination.Y, _laneMinion.PathfindingRadius))
                    {
                        parallelDestination = rawWaypoint;
                    }
                }

                Vector2 currentDestination = _laneMinion.Waypoints.LastOrDefault();
                if (Vector2.DistanceSquared(currentDestination, parallelDestination) > 2500f)
                {
                    var path = ApiFunctionManager.GetPath(_laneMinion.Position, parallelDestination, _laneMinion.PathfindingRadius);
                    if (path == null || path.Count == 0) path = new List<Vector2> { _laneMinion.Position, parallelDestination };
                    _laneMinion.SetWaypoints(path);
                }

                if (_laneMinion.MoveOrder != OrderType.AttackMove) _laneMinion.UpdateMoveOrder(OrderType.AttackMove);
                return NodeState.Running;
            }

            if (_laneMinion.MoveOrder != OrderType.Stop) _laneMinion.UpdateMoveOrder(OrderType.Stop);
            return NodeState.Success;
        }

        private void AdvanceToNextRingNode()
        {
            var nodes = LevelScriptObjects.InfoPoints;
            var masterRing = LevelScriptObjects.OuterRingWaypoints;
            if (nodes == null || masterRing == null || masterRing.Count == 0 || CurrentTargetNode == null) return;

            int currentIdx = CurrentTargetNode.Index;
            int nextIdx = IsClockwise ? (currentIdx + 1) % 5 : (currentIdx + 4) % 5;

            var nextNode = nodes[nextIdx];

            int startRingIdx = IsClockwise ? CurrentTargetNode.RightCircleIndex : CurrentTargetNode.LeftCircleIndex;
            int endRingIdx = IsClockwise ? nextNode.LeftCircleIndex : nextNode.RightCircleIndex;
            int totalRing = masterRing.Count;

            int rIdx = startRingIdx;
            while (true)
            {
                _laneMinion.PathingWaypoints.Add(masterRing[rIdx]);
                if (rIdx == endRingIdx) 
                    break;

                rIdx = IsClockwise ? (rIdx + 1) % totalRing : (rIdx - 1 + totalRing) % totalRing;
            }

            _laneMinion.PathingWaypoints.Add(nextNode.Point.Position);
            CurrentTargetNode = nextNode;
        }

        private bool IsValidTarget(AttackableUnit u)
        {
            if (u == null || u.IsDead || u.Team == _laneMinion.Team) 
                return false;

            float acqRange = _laneMinion.Stats.AcquisitionRange.Total + u.CollisionRadius + 250f;
            if (Vector2.DistanceSquared(_laneMinion.Position, u.Position) >= acqRange * acqRange) 
                return false;
            if (!u.IsVisibleByTeam(_laneMinion.Team)) 
                return false;

            bool isTargetable = u.Status.HasFlag(StatusFlags.Targetable) || u.Model == "OdinNeutralGuardian";
            if (!isTargetable) 
                return false;

            return !ApiFunctionManager.UnitIsProtectionActive(u);
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