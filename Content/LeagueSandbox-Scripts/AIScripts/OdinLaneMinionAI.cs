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
            HandlesTargetAcquisition = true,
            HandlesCallsForHelp = true
        };

        private LaneMinion _laneMinion;
        private Node _behaviorTree;

        public InfoPoint CurrentTargetNode { get; set; }
        public bool IsClockwise { get; set; }

        private int _currentWaypointIndex = 0;
        private Vector2 _laneStart;
        private List<Vector2> _laneWaypoints;
        private float _thinkTimer = 0f;
        private float _localTime = 0f;
        private float _timeSinceLastAttack = 0f;

        private Dictionary<uint, float> _temporaryIgnored = new Dictionary<uint, float>();
        private LaneMinionTargetSelection _targetSelection;
        private static readonly Random _rnd = new Random();
        private bool _wasCC = false;


        public void OnActivate(ObjAIBase owner)
        {
            _laneMinion = owner as LaneMinion;
            _targetSelection = new LaneMinionTargetSelection(_laneMinion, IsValidTarget,
                u => _temporaryIgnored.ContainsKey(u.NetId),
                u => u.Model == "OdinNeutralGuardian" ? (int)ClassifyUnit.TURRET : (int)_laneMinion.ClassifyTarget(u));

            _laneWaypoints = null;
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
            _targetSelection?.Advance(diff);

            if (_laneMinion == null || _laneMinion.IsDead || _laneMinion.IsAIPaused())
                return;

            if (_laneWaypoints == null)
            {
                if (_laneMinion.PathingWaypoints == null)
                    return;

                _laneWaypoints = new List<Vector2>(_laneMinion.PathingWaypoints);
                _laneStart = _laneMinion.Position;
                _currentWaypointIndex = 0;
            }

            while (_laneWaypoints != null && _currentWaypointIndex < _laneWaypoints.Count && WaypointReached())
                _currentWaypointIndex++;

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

            if (_laneMinion.IsAttacking ||
                _laneMinion.TargetUnit == null ||
                _laneMinion.NavigationState.BlockedSeconds < .5f ||
                Vector2.DistanceSquared(
                    _laneMinion.Position,
                    _laneMinion.TargetUnit.Position
                ) <= MathF.Pow(
                    _laneMinion.Stats.Range.Total + _laneMinion.TargetUnit.CollisionRadius,
                    2
                ))
            {
                _timeSinceLastAttack = 0f;
            }
            else
            {
                _timeSinceLastAttack += diff;
            }

            _thinkTimer += diff;

            if (_thinkTimer >= 200f || _targetSelection.Pending)
            {
                _thinkTimer = 0f;

                FilterTemporaryIgnoredList();
                _behaviorTree.Evaluate();

            }
        }

        public void OnCallForHelp(AttackableUnit attacker, AttackableUnit victim)
        {
            _targetSelection?.ReceiveHelp(attacker, victim);
        }

        public void OnMovementOverlap()
        {
            _targetSelection?.RequestOverlap();
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
                    _laneMinion.StopMovement();
                }

                return NodeState.Failure;
            }

            if (_timeSinceLastAttack >= 4000f &&
                currentTarget.Model != "OdinNeutralGuardian")
            {
                Ignore(currentTarget);

                _laneMinion.SetTargetUnit(null, true);
                _laneMinion.StopMovement();

                return NodeState.Failure;
            }

            return _targetSelection.Pending ? NodeState.Failure : NodeState.Success;
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
            var bestTarget = _targetSelection.Select(ApiFunctionManager.EnumerateValidUnitsInRange(
                _laneMinion, _laneMinion.Position, _laneMinion.Stats.AcquisitionRange.Total, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes
                | SpellDataFlags.AffectMinions | SpellDataFlags.AffectTurrets | SpellDataFlags.AffectNeutral));

            if (bestTarget == null &&
                CurrentTargetNode != null &&
                IsValidTarget(CurrentTargetNode.Point))
            {
                float distSq = Vector2.DistanceSquared(
                    _laneMinion.Position,
                    CurrentTargetNode.Point.Position
                );

                float nodeAcqRange = 800f;

                if (distSq <= nodeAcqRange * nodeAcqRange)
                {
                    bestTarget = CurrentTargetNode.Point;
                }
            }

            if (bestTarget != null)
            {
                if (_laneMinion.TargetUnit != bestTarget)
                {
                    _timeSinceLastAttack = 0f;

                    if (_laneMinion.IsAttacking && !_laneMinion.HasAutoAttacked
                        && !_laneMinion.AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                        _laneMinion.CancelAutoAttack(false, true);
                    _laneMinion.SetTargetUnit(
                        bestTarget,
                        true
                    );
                }


                return OrderAttackTarget();
            }

            return NodeState.Failure;
        }

        private NodeState FollowLaneWaypoints()
        {
            if (_laneWaypoints == null ||
                _laneWaypoints.Count == 0)
            {
                return NodeState.Failure;
            }

            while (_currentWaypointIndex < _laneWaypoints.Count &&
                   WaypointReached())
            {
                _currentWaypointIndex++;
            }

            if (_currentWaypointIndex >= _laneWaypoints.Count)
            {
                if (CurrentTargetNode != null &&
                    CurrentTargetNode.Point.Team == _laneMinion.Team)
                {
                    _laneMinion.TakeDamage(
                        _laneMinion,
                        99999f,
                        DamageType.DAMAGE_TYPE_TRUE,
                        DamageSource.DAMAGE_SOURCE_INTERNALRAW,
                        false
                    );

                    return NodeState.Success;
                }
            }

            if (_currentWaypointIndex < _laneWaypoints.Count)
            {
                Vector2 parallelDestination =
                    _laneWaypoints[_currentWaypointIndex];

                Vector2 currentDestination =
                    _laneMinion.Waypoints.LastOrDefault();

                if (_laneMinion.IsPathEnded() ||
                    Vector2.DistanceSquared(
                        currentDestination,
                        parallelDestination
                    ) > 2500f)
                {
                    var path = ApiFunctionManager.GetPath(
                        _laneMinion.Position,
                        parallelDestination,
                        _laneMinion.PathfindingRadius
                    );

                    
                    if (path == null || path.Count <= 1)
                    {
                        return NodeState.Running;
                    }

                    _laneMinion.SetWaypoints(path);
                }

                if (_laneMinion.MoveOrder != OrderType.AttackMove)
                {
                    _laneMinion.UpdateMoveOrder(
                        OrderType.AttackMove
                    );
                }

                return NodeState.Running;
            }

            if (_laneMinion.MoveOrder != OrderType.Stop)
            {
                _laneMinion.UpdateMoveOrder(
                    OrderType.Stop
                );
            }

            return NodeState.Success;
        }

        private bool IsValidTarget(AttackableUnit u)
        {
            if (u == null ||
                u.IsDead ||
                u.IsToRemove() ||
                u.Team == _laneMinion.Team)
            {
                return false;
            }

            float acqRange =
                _laneMinion.Stats.AcquisitionRange.Total +
                u.CollisionRadius +
                250f;

            if (u.Model == "OdinNeutralGuardian")
            {
                acqRange = Math.Max(
                    acqRange,
                    800f
                );
            }

            acqRange = _targetSelection.AcquisitionRange(u, acqRange);
            if (Vector2.DistanceSquared(
                    _laneMinion.Position,
                    u.Position
                ) >= acqRange * acqRange)
            {
                return false;
            }

            if (!u.IsVisibleByTeam(_laneMinion.Team))
            {
                return false;
            }

            bool isTargetable =
                u.GetIsTargetableToTeam(_laneMinion.Team) ||
                u.Model == "OdinNeutralGuardian";

            if (!isTargetable)
            {
                return false;
            }

            return !ApiFunctionManager.UnitIsProtectionActive(u);
        }

        private void Ignore(
            AttackableUnit unit,
            float time = 5000f)
        {
            _temporaryIgnored[unit.NetId] =
                _localTime + time;
        }

        private void FilterTemporaryIgnoredList()
        {
            var keysToRemove = _temporaryIgnored
                .Where(pair => pair.Value <= _localTime)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _temporaryIgnored.Remove(key);
            }
        }

        private bool WaypointReached()
        {
            var waypoint =
                _laneWaypoints[_currentWaypointIndex];

            var position =
                _laneMinion.Position;

            if (Vector2.DistanceSquared(
                    position,
                    waypoint
                ) <= 150f * 150f)
            {
                return true;
            }

            var previous =
                _currentWaypointIndex > 0
                    ? _laneWaypoints[_currentWaypointIndex - 1]
                    : _laneStart;

            var segment =
                waypoint - previous;

            float lengthSquared =
                segment.LengthSquared();

            if (lengthSquared < .001f)
            {
                return false;
            }

            var offset =
                position - waypoint;

            float beyond =
                Vector2.Dot(offset, segment);

            float cross =
                offset.X * segment.Y -
                offset.Y * segment.X;

            // Retain the lane corridor check, without forgetting a completed pass.
            return beyond >= 0 &&
                   cross * cross <=
                       150f * 150f * lengthSquared;
        }
    }
}
