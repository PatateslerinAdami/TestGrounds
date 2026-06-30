using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerCore.Scripting.CSharp.BehaviorTree;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace AIScripts
{
    public class MinonAI : IAIScript
    {
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData();
        private Minion _minion;
        private Node _behaviorTree;
        private float _thinkTimer = 0f;

        public void OnActivate(ObjAIBase owner)
        {
            _minion = owner as Minion;

            _behaviorTree = new Selector(
                new Sequence(
                    new ActionNode(CheckHasTarget),
                    new ActionNode(OrderAttackTarget)
                ),
                new ActionNode(ScanForTargets),
                new ActionNode(FollowWaypoints)
            );
        }

        public void OnUpdate(float diff)
        {
            if (_minion.IsDead || _minion.IsAIPaused()) return;

            _thinkTimer += diff;
            if (_thinkTimer >= 250f)
            {
                _thinkTimer = 0f;
                _behaviorTree.Evaluate();
            }
        }

        private NodeState CheckHasTarget()
        {
            if (_minion.TargetUnit != null && !_minion.TargetUnit.IsDead) return NodeState.Success;
            _minion.SetTargetUnit(null, true);
            return NodeState.Failure;
        }

        private NodeState OrderAttackTarget()
        {
            if (_minion.MoveOrder != OrderType.AttackTo) _minion.UpdateMoveOrder(OrderType.AttackTo);
            return NodeState.Success;
        }

        private NodeState ScanForTargets()
        {
            var potentialTargets = ApiFunctionManager.GetUnitsInRange(_minion, _minion.Position, _minion.Stats.AcquisitionRange.Total, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectTurrets);

            AttackableUnit bestTarget = null;
            int bestPriority = (int)ClassifyUnit.DEFAULT;
            int bestAttackers = int.MaxValue;
            float bestDistSq = _minion.Stats.AcquisitionRange.Total * _minion.Stats.AcquisitionRange.Total;

            foreach (var u in potentialTargets)
            {
                if (u.Status.HasFlag(StatusFlags.Targetable) && !ApiFunctionManager.UnitIsProtectionActive(u))
                {
                    int priority = (int)_minion.ClassifyTarget(u);
                    float distSq = Vector2.DistanceSquared(_minion.Position, u.Position);
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
                _minion.SetTargetUnit(bestTarget, true);
                return NodeState.Success;
            }

            return NodeState.Failure;
        }

        private NodeState FollowWaypoints()
        {
            if (_minion.Waypoints.Count > 1 && _minion.MoveOrder != OrderType.MoveTo)
            {
                _minion.UpdateMoveOrder(OrderType.MoveTo);
                return NodeState.Running;
            }
            else if (_minion.Waypoints.Count <= 1 && _minion.MoveOrder != OrderType.Stop)
            {
                _minion.UpdateMoveOrder(OrderType.Stop);
            }

            return NodeState.Success;
        }
    }
}