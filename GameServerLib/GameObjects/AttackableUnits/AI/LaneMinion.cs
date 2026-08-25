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

        protected float? _claimedAttackAngle = null;
        protected float? _initialApproachAngle = null;
        protected AttackableUnit _lastSlotTarget = null;
        private static readonly Random _rnd = new Random();
        private float _steerTimer = 0f;

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

            SetPosition(position);

            StopMovement();

            MoveOrder = OrderType.Hold;
            Replication = new ReplicationLaneMinion(this);
            _steerTimer = (float)_rnd.NextDouble() * 250f;
        }

        public override void RefreshWaypoints(float idealRange)
        {
            if (MovementParameters != null) return;

            if (TargetUnit != null && _castingSpell == null && ChannelSpell == null && MoveOrder != OrderType.AttackTo)
            {
                UpdateMoveOrder(OrderType.AttackTo, true);
            }

            if (SpellToCast != null)
            {
                idealRange = SpellToCast.GetCurrentCastRange();
            }

            Vector2 targetPos = Vector2.Zero;

            if (MoveOrder == OrderType.AttackTo && TargetUnit != null && !TargetUnit.IsDead)
            {
                targetPos = TargetUnit.Position;
            }

            if (MoveOrder == OrderType.AttackMove || MoveOrder == OrderType.AttackTerrainOnce || MoveOrder == OrderType.AttackTerrainSustained && !IsPathEnded())
            {
                targetPos = Waypoints.LastOrDefault();
                if (targetPos == Vector2.Zero) return;
            }

            if (MoveOrder == OrderType.AttackMove && targetPos != Vector2.Zero && MovementParameters == null && Vector2.DistanceSquared(Position, targetPos) <= idealRange * idealRange && _autoAttackCurrentCooldown <= 0)
            {
                UpdateMoveOrder(OrderType.Stop, true);
            }
            else if (targetPos == Vector2.Zero)
            {
                return;
            }

            if (MoveOrder == OrderType.AttackTo && targetPos != Vector2.Zero)
            {
                if (Vector2.DistanceSquared(Position, targetPos) <= idealRange * idealRange)
                {
                    bool isReadyToAttack = CanAttack() && _autoAttackCurrentCooldown <= 0 && AutoAttackSpell != null && AutoAttackSpell.State == SpellState.STATE_READY;
                    if (isReadyToAttack) StopMovement(networked: false);
                    else UpdateMoveOrder(OrderType.Hold, true);
                }
                else
                {
                    Vector2 attackSlotPos = GetAttackSlotPosition(TargetUnit, idealRange);

                    if (attackSlotPos == Position)
                    {
                        if (Waypoints.Count > 1) StopMovement(networked: true);
                        return;
                    }

                    if (!_game.Map.PathingHandler.IsWalkable(attackSlotPos, PathfindingRadius))
                    {
                        attackSlotPos = _game.Map.NavigationGrid.GetClosestTerrainExit(attackSlotPos, PathfindingRadius);
                    }

                    bool needsRepath = true;
                    if (Waypoints != null && Waypoints.Count > 0)
                    {
                        Vector2 currentDest = Waypoints.Last();
                        if (Vector2.DistanceSquared(currentDest, attackSlotPos) < 2500f) needsRepath = false;
                    }

                    if (needsRepath)
                    {
                        var newWaypoints = FindClearLocalPath(Position, attackSlotPos, PathfindingRadius);
                        if (newWaypoints != null && newWaypoints.Count > 1) SetWaypoints(newWaypoints);
                    }
                }
            }
        }

        protected Vector2 GetAttackSlotPosition(AttackableUnit target, float idealRange)
        {
            float standDistance = Math.Max(60f, idealRange - 25f);
            float spaceNeeded = CollisionRadius * 2.0f;

            var nearbyAllies = _game.Map.CollisionHandler.GetNearestObjects(new System.Activities.Presentation.View.Circle(target.Position, standDistance + 500f))
                .OfType<ObjAIBase>()
                .Where(u => u.Team == Team && u != this && !u.IsDead && u.TargetUnit == target)
                .ToList();

            if (_lastSlotTarget != target)
            {
                _claimedAttackAngle = null;
                _lastSlotTarget = target;
                Vector2 targetToUs = Position - target.Position;
                if (targetToUs.LengthSquared() <= 0.001f) targetToUs = new Vector2(1, 0);
                _initialApproachAngle = (float)Math.Atan2(targetToUs.Y, targetToUs.X);
            }

            float checkDistSq = spaceNeeded * spaceNeeded;

            bool IsPositionFree(Vector2 testPos)
            {
                foreach (var ally in nearbyAllies)
                {
                    Vector2 allyPos = ally.Position;
                    Vector2 allyDest = ally.Waypoints.Count > 0 ? ally.Waypoints.Last() : ally.Position;
                    if (Vector2.DistanceSquared(allyPos, testPos) < checkDistSq || Vector2.DistanceSquared(allyDest, testPos) < checkDistSq)
                        return false;
                }
                return true;
            }

            if (_claimedAttackAngle.HasValue)
            {
                Vector2 claimedDir = new Vector2((float)Math.Cos(_claimedAttackAngle.Value), (float)Math.Sin(_claimedAttackAngle.Value));
                Vector2 claimedPos = target.Position + (claimedDir * standDistance);
                if (IsPositionFree(claimedPos) && _game.Map.PathingHandler.IsWalkable(claimedPos, PathfindingRadius))
                    return claimedPos;
            }

            float angleStepRadians = spaceNeeded / standDistance;
            if (angleStepRadians < 0.2f) angleStepRadians = 0.2f;

            int numSlots = (int)(Math.PI * 2 / angleStepRadians);
            float baseAngle = _initialApproachAngle ?? 0f;

            for (int i = 0; i <= numSlots / 2; i++)
            {
                float[] offsets = i == 0 ? new float[] { 0 } : new float[] { i * angleStepRadians, -i * angleStepRadians };
                foreach (float offset in offsets)
                {
                    float testAngle = baseAngle + offset;
                    Vector2 testDir = new Vector2((float)Math.Cos(testAngle), (float)Math.Sin(testAngle));
                    Vector2 testPos = target.Position + (testDir * standDistance);
                    if (IsPositionFree(testPos) && _game.Map.PathingHandler.IsWalkable(testPos, PathfindingRadius))
                    {
                        _claimedAttackAngle = testAngle;
                        return testPos;
                    }
                }
            }
            return Position;
        }

        private float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq == 0) return Vector2.Distance(p, a);
            float t = Math.Max(0, Math.Min(1, Vector2.Dot(p - a, ab) / lenSq));
            Vector2 proj = a + t * ab;
            return Vector2.Distance(p, proj);
        }

        protected List<Vector2> FindClearLocalPath(Vector2 start, Vector2 target, float radius)
        {
            float distToTarget = Vector2.Distance(start, target);
            if (distToTarget <= 0.001f) return new List<Vector2> { start, target };

            float scanRadius = Math.Min(distToTarget, Stats.AcquisitionRange.Total) + radius + 150f;
            var obstacles = _game.Map.CollisionHandler.GetNearestObjects(new System.Activities.Presentation.View.Circle(start, scanRadius))
                .OfType<ObjAIBase>()
                .Where(u => u != this && !u.IsDead && !u.Status.HasFlag(StatusFlags.Ghosted) && u.CollisionRadius > 0 && u != TargetUnit)
                .ToList();

            bool isSegmentClear(Vector2 p1, Vector2 p2)
            {
                if (_game.Map.NavigationGrid.CastCircle(p1, p2, radius)) return false;
                foreach (var obs in obstacles)
                {
                    float distToLine = DistancePointToSegment(obs.Position, p1, p2);
                    if (distToLine < (radius + obs.CollisionRadius)) return false;
                }
                return true;
            }

            if (isSegmentClear(start, target)) return new List<Vector2> { start, target };

            Vector2 dirToTarget = (target - start) / distToTarget;
            float[] angles = new float[] { 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 90f, -90f };
            float scanDist = Math.Min(distToTarget, Stats.AcquisitionRange.Total);
            if (scanDist < 100f) scanDist = 100f;

            foreach (float angle in angles)
            {
                float rad = angle * (float)Math.PI / 180f;
                float cos = (float)Math.Cos(rad);
                float sin = (float)Math.Sin(rad);
                Vector2 scanDir = new Vector2(dirToTarget.X * cos - dirToTarget.Y * sin, dirToTarget.X * sin + dirToTarget.Y * cos);
                Vector2 scanTarget = start + scanDir * scanDist;

                if (isSegmentClear(start, scanTarget))
                {
                    if (_game.Map.PathingHandler.IsWalkable(scanTarget, radius))
                        return new List<Vector2> { start, scanTarget, target };
                }
            }

            var fallbackPath = _game.Map.PathingHandler.GetPath(this, target);
            if (fallbackPath != null && fallbackPath.Count > 1) return fallbackPath;
            return new List<Vector2> { start, target };
        }

        public override bool Move(float delta)
        {
            if (MoveOrder == OrderType.CastSpell
                || MoveOrder == OrderType.OrderNone
                || MoveOrder == OrderType.Stop
                || MoveOrder == OrderType.Taunt)
            {
                return false;
            }

            if (CurrentWaypointKey < Waypoints.Count)
            {
                float speed = GetMoveSpeed() * 0.001f;
                var maxDist = speed * delta;

                var dir = CurrentWaypoint - Position;
                var dist = dir.Length();

                Vector2 desiredMovement;
                if (maxDist < dist)
                {
                    desiredMovement = (dir / dist) * maxDist;
                }
                else
                {
                    desiredMovement = dir; 
                }

                var neighbors = _game.Map.CollisionHandler.GetNearestObjects(new System.Activities.Presentation.View.Circle(Position, 300f))
                    .OfType<ObjAIBase>();

                bool isStuck;
                Vector2 steeredMovement = MinColl.CalculateSteeredMovement(this, desiredMovement, maxDist, delta / 1000f, neighbors, out isStuck);

                if (isStuck)
                {
                    var blockingEnemy = neighbors.FirstOrDefault(n => n.Team != this.Team && Vector2.DistanceSquared(n.Position, this.Position) < 25000f);
                    if (blockingEnemy != null && CanAttack())
                    {
                        SetTargetUnit(blockingEnemy, true);
                        UpdateMoveOrder(OrderType.AttackTo, true);
                        return false;
                    }
                    return false;
                }

                Vector2 nextPos = Position + steeredMovement;

                if (!_game.Map.PathingHandler.IsWalkable(nextPos, PathfindingRadius))
                {
                    nextPos = Position + desiredMovement;
                    if (!_game.Map.PathingHandler.IsWalkable(nextPos, PathfindingRadius))
                    {
                        nextPos = _game.Map.NavigationGrid.GetClosestTerrainExit(nextPos, PathfindingRadius);
                    }
                }

                if (Vector2.DistanceSquared(desiredMovement, steeredMovement) > 1.0f) //Pam pam spam
                {
                    _movementUpdated = true;
                }

                Velocity = nextPos - Position;
                Position = nextPos;

                if (maxDist >= dist)
                {
                    CurrentWaypointKey++;
                    if (CurrentWaypointKey == Waypoints.Count)
                    {
                        return true; 
                    }
                }
                return true;
            }

            Velocity = Vector2.Zero;
            return false;
        }
    }
}