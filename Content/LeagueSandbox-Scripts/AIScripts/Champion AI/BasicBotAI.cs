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
    public class BasicBotAI : IAIScript
    {
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData
        {
            HandlesCallsForHelp = true
        };

        private Champion _bot;
        private Node _behaviorTree;
        private static readonly Random _rnd = new Random();

        private float _thinkTimer = 0f;
        private Vector2 _enemyBasePosition;
        private Vector2 _allyBasePosition;

        private readonly SpellDataFlags _targetFlags = SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions;

        public void OnActivate(ObjAIBase owner)
        {
            _bot = owner as Champion;
            _thinkTimer = (float)_rnd.NextDouble() * 250f;

            TeamId enemyTeam = _bot.Team == TeamId.TEAM_BLUE ? TeamId.TEAM_PURPLE : TeamId.TEAM_BLUE;
            _enemyBasePosition = ApiFunctionManager.GetFountainPosition(enemyTeam);
            _allyBasePosition = ApiFunctionManager.GetFountainPosition(_bot.Team);

            _behaviorTree = new Selector(
                new ActionNode(CheckRetreat),
                new ActionNode(CheckHasTarget),
                new ActionNode(CombatLogic),
                new ActionNode(PushLane)
            );
        }

        public void OnUpdate(float diff)
        {
            if (_bot == null || _bot.IsDead || _bot.IsAIPaused())
                return;

            if (!_bot.CanIssueMoveOrders())
                return;

            _thinkTimer += diff;
            if (_thinkTimer >= 250f)
            {
                _thinkTimer = 0f;
                _behaviorTree.Evaluate();
            }
        }

        public void OnCallForHelp(AttackableUnit attacker, AttackableUnit victim) { }


        private NodeState CheckRetreat()
        {
            float healthPercent = _bot.Stats.CurrentHealth / _bot.Stats.HealthPoints.Total;

            if (healthPercent < 0.20f)
            {
                _bot.SetTargetUnit(null, true);

                if (Vector2.DistanceSquared(_bot.Position, _allyBasePosition) > 400f * 400f)
                {
                    var path = ApiFunctionManager.GetPath(_bot.Position, _allyBasePosition);
                    _bot.SetWaypoints(path);
                    _bot.UpdateMoveOrder(OrderType.MoveTo);
                }

                return NodeState.Success;
            }

            return NodeState.Failure;
        }

        private NodeState CheckHasTarget()
        {
            if (_bot.TargetUnit != null && !_bot.TargetUnit.IsDead && ApiFunctionManager.IsValidTarget(_bot, _bot.TargetUnit, _targetFlags))
            {
                if (Vector2.DistanceSquared(_bot.Position, _bot.TargetUnit.Position) > 1000f * 1000f)
                {
                    _bot.SetTargetUnit(null, true);
                    return NodeState.Failure;
                }

                if (_bot.MoveOrder != OrderType.AttackTo && _bot.MoveOrder != OrderType.Hold)
                {
                    _bot.UpdateMoveOrder(OrderType.AttackTo);
                }

                return NodeState.Success;
            }

            return NodeState.Failure;
        }

        private NodeState CombatLogic()
        {
            var potentialTargets = ApiFunctionManager.GetUnitsInRange(
                _bot,
                _bot.Position,
                800f,
                true,
                _targetFlags
            );

            AttackableUnit bestTarget = null;
            int highestPriority = -1;
            float closestDistSq = float.MaxValue;

            foreach (var target in potentialTargets)
            {
                if (!ApiFunctionManager.IsValidTarget(_bot, target, _targetFlags)) continue;

                int priority = target is Champion ? 2 : 1;
                float distSq = Vector2.DistanceSquared(_bot.Position, target.Position);

                if (priority > highestPriority || (priority == highestPriority && distSq < closestDistSq))
                {
                    bestTarget = target;
                    highestPriority = priority;
                    closestDistSq = distSq;
                }
            }

            if (bestTarget != null)
            {
                _bot.SetTargetUnit(bestTarget, true);

                if (_bot.MoveOrder != OrderType.AttackTo)
                {
                    _bot.UpdateMoveOrder(OrderType.AttackTo);
                }

                return NodeState.Success;
            }

            if (_bot.TargetUnit != null)
            {
                _bot.SetTargetUnit(null, true);
            }

            return NodeState.Failure;
        }

        private NodeState PushLane()
        {
            if (_bot.Waypoints == null || _bot.Waypoints.Count <= 1)
            {
                var path = ApiFunctionManager.GetPath(_bot.Position, _enemyBasePosition);
                if (path != null && path.Count > 0)
                {
                    _bot.SetWaypoints(path);
                }
            }

            if (_bot.MoveOrder != OrderType.AttackMove)
            {
                _bot.UpdateMoveOrder(OrderType.AttackMove);
            }

            return NodeState.Success;
        }
    }
}