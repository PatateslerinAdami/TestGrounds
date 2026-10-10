using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class LaneMinion : Minion
    {
        /// <summary>
        /// Const waypoints that define the minion's route
        /// </summary>
        public List<Vector2> PathingWaypoints { get; }
        /// <summary>
        /// Name of the Barracks that spawned this lane minion.
        /// </summary>
        public string BarracksName { get; }
        public MinionSpawnType MinionSpawnType { get; }

        public override bool SpawnShouldBeHidden => false;

        public float LaneOffset { get; set; } = float.NaN;

        private AttackableUnit _approachTarget;
        private Vector2 _plannedTargetPosition;
        private float _nextApproachUpdate;
        private Vector2 _lastApproachPosition;
        private float _plannedAttackRange;
        private bool _lastApproachIncomplete;
        private int _unchangedApproachAttempts;

        public LaneMinion(
            Game game,
            MinionSpawnType spawnType,
            Vector2 position,
            string barracksName,
            List<Vector2> mainWaypoints,
            string model,
            uint netId = 0,
            TeamId team = TeamId.TEAM_BLUE,
            Stats stats = null,
            string AIScript = ""
        ) : base(game, null, new Vector2(), model, model, netId, team, stats: stats, AIScript: AIScript)
        {
            IsLaneMinion = true;
            MinionSpawnType = spawnType;
            BarracksName = barracksName;
            PathingWaypoints = mainWaypoints;
            _aiPaused = false;

            SetPosition(_game.Map.NavigationGrid.GetClosestTerrainExit(position, PathfindingRadius));

            StopMovement();

            MoveOrder = OrderType.Hold;
            Replication = new ReplicationLaneMinion(this);
        }

        public override void RefreshWaypoints(float idealRange)
        {
            if (MovementParameters != null || !CanMove()) return;

            if (TargetUnit != null
                && _castingSpell == null
                && ChannelSpell == null
                && MoveOrder != OrderType.AttackTo)
            {
                UpdateMoveOrder(OrderType.AttackTo, true);
            }

            if (SpellToCast != null)
            {
                idealRange = SpellToCast.GetCurrentCastRange();
            }

            Vector2 targetPos = Vector2.Zero;

            if (MoveOrder == OrderType.AttackTo
                && TargetUnit != null
                && !TargetUnit.IsDead)
            {
                targetPos = TargetUnit.Position;
            }

            if ((MoveOrder == OrderType.AttackMove
                || MoveOrder == OrderType.AttackTerrainOnce
                || MoveOrder == OrderType.AttackTerrainSustained)
                && !IsPathEnded())
            {
                targetPos = Waypoints.LastOrDefault();

                if (targetPos == Vector2.Zero)
                {
                    return;
                }
            }

            if (targetPos == Vector2.Zero)
            {
                return;
            }

            if (MoveOrder == OrderType.AttackTo
                && TargetUnit != null
                && !TargetUnit.IsDead)
            {
                if (Vector2.DistanceSquared(Position, targetPos)
                    <= idealRange * idealRange)
                {
                    bool isReadyToAttack =
                        CanAttack()
                        && _autoAttackCurrentCooldown <= 0
                        && AutoAttackSpell != null
                        && AutoAttackSpell.State == SpellState.STATE_READY;

                    if (!IsPathEnded()) StopMovement(networked: !isReadyToAttack);

                    return;
                }

                TryUpdateAttackApproach(idealRange, recovery: false);
            }
        }

        // Combat refresh and collision recovery share a single search schedule.
        // Keep walking an existing path while waiting, do not put the actor into
        // a waiting state or alter its speed/collision response.
        internal bool TryUpdateAttackApproach(float idealRange, bool recovery)
        {
            var target = TargetUnit;
            if (target == null || target.IsDead || MovementParameters != null || !CanMove()) return false;
            if (SpellToCast != null) idealRange = SpellToCast.GetCurrentCastRange();
            var targetPos = target.Position;
            bool changed = _approachTarget != target
                || Vector2.DistanceSquared(_plannedTargetPosition, targetPos) >= 2500
                || Math.Abs(_plannedAttackRange - idealRange) > .01f;
            bool progressed = Vector2.DistanceSquared(_lastApproachPosition, Position) >= 625;
            // New targets/ranges and materially moved targets bypass old retries.
            if (!changed && _game.GameTime < _nextApproachUpdate) return false;
            if (!changed && !recovery && !IsPathEnded()
                && NavigationState.BlockedSeconds < .5f) return false;
            if (!_game.MovementManager.TryReservePathSearch(this)) return false;

            _unchangedApproachAttempts = !changed && !progressed && _lastApproachIncomplete
                ? Math.Min(2, _unchangedApproachAttempts + 1) : 0;
            var path = _game.Map.PathingHandler.GetAttackApproach(this, target, idealRange);
            _approachTarget = target;
            _plannedTargetPosition = targetPos;
            _plannedAttackRange = idealRange;
            _lastApproachPosition = Position;
            float radius = Math.Max(1, idealRange - 5);
            _lastApproachIncomplete = path == null || path.Count < 2
                || Vector2.DistanceSquared(path[path.Count - 1], targetPos) > radius * radius + 1;
            if (!_lastApproachIncomplete) _unchangedApproachAttempts = 0;
            // At most 500 ms between unchanged failed/partial attempts. Existing
            // movement still probes collisions every movement step in that time.
            _nextApproachUpdate = _game.GameTime + 250 + 125 * _unchangedApproachAttempts;
            if (path != null && path.Count > 1)
            {
                if (recovery) ApplyRecoveryPath(path);
                else SetWaypoints(path);
            }
            else if (IsPathEnded()) NavigationState.LastReason = "attack approach blocked";
            return true;
        }
    }
}
