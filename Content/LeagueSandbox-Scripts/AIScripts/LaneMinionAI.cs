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

        public void OnActivate(ObjAIBase owner)
        {
            _laneMinion = owner as LaneMinion;

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

            if (_laneMinion.IsAttacking || _laneMinion.TargetUnit == null) 
            { 
                _timeSinceLastAttack = 0f; 
            }
            else 
            { 
                _timeSinceLastAttack += diff; 
            }

            _thinkTimer += diff;
            if (_thinkTimer >= 250f)
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
            if (IsValidTarget(_laneMinion.TargetUnit))
            {
                if (_timeSinceLastAttack >= 4000f)
                {
                    Ignore(_laneMinion.TargetUnit);
                    _laneMinion.SetTargetUnit(null, true);
                    return NodeState.Failure;
                }
                return NodeState.Success;
            }

            if (_laneMinion.TargetUnit != null)
            {
                _laneMinion.CancelAutoAttack(false, true);
                _laneMinion.SetTargetUnit(null, true);
            }
            return NodeState.Failure;
        }

        private NodeState OrderAttackTarget()
        {
            if (_laneMinion.MoveOrder != OrderType.AttackTo)
            {
                _laneMinion.UpdateMoveOrder(OrderType.AttackTo);
            }
            return NodeState.Success;
        }

        private NodeState ScanForTargets()
        {
            _callsForHelpMayBeCleared = true;
            AttackableUnit bestTarget = null;
            int bestPriority = (int)ClassifyUnit.DEFAULT;
            int bestAttackers = int.MaxValue;
            float bestDistSq = _laneMinion.Stats.AcquisitionRange.Total * _laneMinion.Stats.AcquisitionRange.Total;

            var potentialTargets = _unitsAttackingAllies.Keys.ToList();
            if (potentialTargets.Count == 0)
            {
                potentialTargets = ApiFunctionManager.GetUnitsInRange(_laneMinion, _laneMinion.Position, _laneMinion.Stats.AcquisitionRange.Total, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectTurrets);
            }

            foreach (var u in potentialTargets)
            {
                if (IsValidTarget(u) && !_temporaryIgnored.ContainsKey(u.NetId))
                {
                    int priority = _unitsAttackingAllies.ContainsKey(u) ? _unitsAttackingAllies[u] : (int)_laneMinion.ClassifyTarget(u);
                    float distSq = Vector2.DistanceSquared(_laneMinion.Position, u.Position);

                    int attackers = ApiFunctionManager.CountUnitsAttackingUnit(u);

                  
                    if (attackers >= 4) priority += 10;

                    bool isBetterTarget = false;

                    if (bestTarget == null) isBetterTarget = true;
                    else if (priority < bestPriority) isBetterTarget = true;
                    else if (priority == bestPriority)
                    {
                        if (attackers < bestAttackers) isBetterTarget = true;
                        else if (attackers == bestAttackers && distSq < bestDistSq) isBetterTarget = true;
                    }

                    if (isBetterTarget)
                    {
                        bestTarget = u;
                        bestPriority = priority;
                        bestAttackers = attackers;
                        bestDistSq = distSq;
                    }
                }
            }

            if (bestTarget != null)
            {
                _laneMinion.SetTargetUnit(bestTarget, true);
                _timeSinceLastAttack = 0f;
                return NodeState.Success;
            }
            return NodeState.Failure;
        }

        private NodeState FollowLaneWaypoints()
        {
            if (_laneMinion.PathingWaypoints == null || _laneMinion.PathingWaypoints.Count == 0) return NodeState.Failure;

            bool notYetOutOfRange = true;
            while ((notYetOutOfRange = _currentWaypointIndex < _laneMinion.PathingWaypoints.Count) && WaypointReached())
            {
                _currentWaypointIndex++;
            }

            if (notYetOutOfRange)
            {
                Vector2 currentWaypoint = _laneMinion.PathingWaypoints[_currentWaypointIndex];

                Vector2 currentDestination = _laneMinion.Waypoints.LastOrDefault();
                if (Vector2.DistanceSquared(currentDestination, currentWaypoint) > 2500f)
                {
                    var path = ApiFunctionManager.GetPath(_laneMinion.Position, currentWaypoint, _laneMinion.PathfindingRadius);
                    if (path == null || path.Count == 0) path = new List<Vector2> { _laneMinion.Position, currentWaypoint };
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