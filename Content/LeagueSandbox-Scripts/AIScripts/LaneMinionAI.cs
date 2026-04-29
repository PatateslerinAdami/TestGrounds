using GameServerCore.Enums;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Scripting.CSharp;
using System.Linq;
using System.Collections.Generic;
using System;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace AIScripts
{
    public class LaneMinionAI : IAIScript
    {
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData
        {
            HandlesCallsForHelp = true
        };
        LaneMinion LaneMinion;
        int currentWaypointIndex = 0;
        float minionActionTimer = 250f;
        bool targetIsStillValid = false;
        Dictionary<uint, float> temporaryIgnored = new Dictionary<uint, float>();
        public Dictionary<AttackableUnit, int> unitsAttackingAllies { get; } = new Dictionary<AttackableUnit, int>();
        float timeSinceLastAttack = 0f;
        int targetUnitPriority = (int) ClassifyUnit.DEFAULT;
        float localTime = 0f;
        bool callsForHelpMayBeCleared = false;
        bool followsWaypoints = true;
        bool _useFirstAcquisitionRange = false;
        bool _firstTargetSearchActive = false;

        const float LATERAL_SPREAD_DISTANCE = 150.0f;

        const float WAVE_DETECT_RANGE = 1500.0f;

        bool _engagementTriggered = false;

        static Dictionary<(TeamId, string, int, MinionSpawnType), int> _engagementSlotCounters = new Dictionary<(TeamId, string, int, MinionSpawnType), int>();

        private bool hadTarget = false;
        private bool TargetJustDied()
        {
            targetIsStillValid = IsValidTarget(LaneMinion.TargetUnit);
            if(targetIsStillValid)
            {
                hadTarget = true;
            }
            else if(hadTarget)
            {
                hadTarget = false;
                ExitFirstWaveBehavior();
                return true;
            }
            return false;
        }

        private void ExitFirstWaveBehavior()
        {
            if (LaneMinion == null || !LaneMinion.IsFirstWave) return;
            LaneMinion.IsFirstWave = false;
            LaneMinion.IsAsleep = false;
            _useFirstAcquisitionRange = false;
            _firstTargetSearchActive = false;
        }

        private void UpdateHasPassedFirstTurret()
        {
            if (LaneMinion.HasPassedFirstTurret) return;
            if (!LaneMinion.OuterTurretPosition.HasValue) return;

            var spawnToTurret = LaneMinion.OuterTurretPosition.Value - LaneMinion.SpawnPosition;
            var spawnToMinion = LaneMinion.Position - LaneMinion.SpawnPosition;
            var axisLengthSq = spawnToTurret.LengthSquared();
            if (axisLengthSq < 1f) return;

            var dot = Vector2.Dot(spawnToMinion, spawnToTurret);
            if (dot > axisLengthSq)
            {
                LaneMinion.HasPassedFirstTurret = true;
            }
        }

        public void OnActivate(ObjAIBase owner)
        {
            LaneMinion = owner as LaneMinion;
        }
        
        public void OnUpdate(float delta)
        {
            localTime += delta;

            if(LaneMinion != null && !LaneMinion.IsDead)
            {
                UpdateHasPassedFirstTurret();

                if(LaneMinion.IsFirstWave && LaneMinion.TargetUnit is Champion)
                {
                    ExitFirstWaveBehavior();
                }

                if(LaneMinion.IsAsleep && HasEnemyInRange(LaneMinion.CharData.WakeUpRange))
                {
                    LaneMinion.IsAsleep = false;
                    _useFirstAcquisitionRange = true;
                    _firstTargetSearchActive = true;
                }

                if(!_engagementTriggered && HasEnemyLaneMinionInWaveDetectRange())
                {
                    _engagementTriggered = true;
                    AssignSpreadSlot();
                    LogInfo($"[SPREAD] {LaneMinion.Team} {LaneMinion.BarracksName} netId={LaneMinion.NetId} " +
                            $"wave={LaneMinion.WaveNumber} → assigned SlotIndex={LaneMinion.SpreadSlotIndex} " +
                            $"at pos={LaneMinion.Position} asleep={LaneMinion.IsAsleep}");
                }

                if(LaneMinion.IsAttacking || LaneMinion.TargetUnit == null)
                {
                    timeSinceLastAttack = 0f;
                }
                else
                {
                    timeSinceLastAttack += delta;
                }

                minionActionTimer += delta;
                if(
                    //Quote: There’s also a number of things
                    //       that can occur between the 0.25 second interval
                    //       of the normal sweep through the AI Priority List:
                    //Quote: Taunt/Fear/Flee/Movement Disable/Attack Disable.
                    //       All of these cause a minion to freshly reevaluate its behavior immediately.
                    LaneMinion.MovementParameters == null
                    //Quote: Collisions.
                    //       Minions that end up overlapping other minions will reevaluate their behavior immediately.
                    //&& !LaneMinion.RecalculateAttackPosition()
                    && (
                        //Quote: Their current attack target dies.
                        //       Minions who witness the death of their foe will check for a new valid target in their acquisition range.
                        TargetJustDied()
                        //Quote: Call for Help.
                        || FoundNewTarget(true)
                        || minionActionTimer >= 250.0f
                    )
                ) {
                    OrderType nextBehaviour = ReevaluateBehavior(delta);
                    LaneMinion.UpdateMoveOrder(nextBehaviour);
                    minionActionTimer = 0;

                    _useFirstAcquisitionRange = false;
                    _firstTargetSearchActive = false;
                }

                if(callsForHelpMayBeCleared)
                {
                    callsForHelpMayBeCleared = false;
                    unitsAttackingAllies.Clear();
                }
            }
        }

        public void OnCallForHelp(AttackableUnit attacker, AttackableUnit victium)
        {
            if(LaneMinion != null && LaneMinion.IsFirstWave && attacker is Champion)
            {
                ExitFirstWaveBehavior();
            }

            if(LaneMinion != null && LaneMinion.IsFirstWave)
            {
                return;
            }

            if(unitsAttackingAllies != null)
            {
                int priority = Math.Min(
                    unitsAttackingAllies.GetValueOrDefault(attacker, (int)ClassifyUnit.DEFAULT),
                    (int)LaneMinion.ClassifyTarget(attacker, victium)
                );
                unitsAttackingAllies[attacker] = priority;
            }
        }

        bool UnitInRange(AttackableUnit u, float range)
        {
            var effectiveRange = range + u.CollisionRadius;
            return Vector2.DistanceSquared(LaneMinion.Position, u.Position) < (effectiveRange * effectiveRange);
        }

        bool IsValidTarget(AttackableUnit u)
        {
            if (LaneMinion.IsAsleep) return false;

            if (_firstTargetSearchActive
                && u is LaneMinion enemyLane
                && enemyLane.MinionSpawnType == MinionSpawnType.MINION_TYPE_CASTER)
            {
                return false;
            }

            return (
                u != null
                && !u.IsDead
                && u.Team != LaneMinion.Team
                && UnitInRange(u, GetEffectiveAcquisitionRange())
                && u.IsVisibleByTeam(LaneMinion.Team)
                && u.Status.HasFlag(StatusFlags.Targetable)
                && !UnitIsProtectionActive(u)
            );
        }

        float GetEffectiveAcquisitionRange()
        {
            if (_useFirstAcquisitionRange)
            {
                return LaneMinion.CharData.FirstAcquisitionRange;
            }
            return LaneMinion.Stats.AcquisitionRange.Total;
        }

        bool HasEnemyInRange(float range)
        {
            foreach (var obj in EnumerateUnitsInRange(LaneMinion.Position, range, true))
            {
                if (obj is AttackableUnit u
                    && !u.IsDead
                    && u.Team != LaneMinion.Team
                    && u.IsVisibleByTeam(LaneMinion.Team)
                    && u.Status.HasFlag(StatusFlags.Targetable)
                    && UnitInRange(u, range))
                {
                    return true;
                }
            }
            return false;
        }

        bool HasEnemyLaneMinionInWaveDetectRange()
        {
            foreach (var obj in EnumerateUnitsInRange(LaneMinion.Position, WAVE_DETECT_RANGE, true))
            {
                if (obj is LaneMinion enemyLane
                    && !enemyLane.IsDead
                    && enemyLane.Team != LaneMinion.Team
                    && enemyLane.Status.HasFlag(StatusFlags.Targetable)
                    && UnitInRange(enemyLane, WAVE_DETECT_RANGE))
                {
                    return true;
                }
            }
            return false;
        }

        void AssignSpreadSlot()
        {
            var key = (LaneMinion.Team, LaneMinion.BarracksName, LaneMinion.WaveNumber, LaneMinion.MinionSpawnType);
            int counter = _engagementSlotCounters.TryGetValue(key, out var current) ? current : 0;
            LaneMinion.SpreadSlotIndex = counter;
            _engagementSlotCounters[key] = counter + 1;
        }

        Vector2 ComputeLateralOffset(int slot, Vector2 leftPerp)
        {
            if (slot <= 0) return Vector2.Zero;
            int rank = (slot + 1) / 2;
            bool isLeft = (slot % 2) == 1;
            float magnitude = rank * LATERAL_SPREAD_DISTANCE;
            return (isLeft ? leftPerp : -leftPerp) * magnitude;
        }

        bool _loggedFirstSpreadOffset = false;
        Vector2 GetSpreadedWaypoint(int waypointIndex)
        {
            Vector2 baseWaypoint = LaneMinion.PathingWaypoints[waypointIndex];
            if (LaneMinion.SpreadSlotIndex < 1) return baseWaypoint;

            Vector2 prevWaypoint = waypointIndex > 0
                ? LaneMinion.PathingWaypoints[waypointIndex - 1]
                : LaneMinion.SpawnPosition;
            Vector2 axis = baseWaypoint - prevWaypoint;
            if (axis.LengthSquared() <= 0.001f) return baseWaypoint;

            Vector2 dirAxis = Vector2.Normalize(axis);
            Vector2 leftPerp = new Vector2(-dirAxis.Y, dirAxis.X);
            Vector2 offset = ComputeLateralOffset(LaneMinion.SpreadSlotIndex, leftPerp);
            if (!_loggedFirstSpreadOffset)
            {
                _loggedFirstSpreadOffset = true;
                LogInfo($"[SPREAD] netId={LaneMinion.NetId} slot={LaneMinion.SpreadSlotIndex} " +
                        $"wpIdx={waypointIndex} base={baseWaypoint} offset={offset} " +
                        $"target={baseWaypoint + offset}");
            }
            return baseWaypoint + offset;
        }

        void Ignore(AttackableUnit unit, float time = 500)
        {
            temporaryIgnored[unit.NetId] = localTime + time;
        }

        void FilterTemporaryIgnoredList()
        {
            List<uint> keysToRemove = new List<uint>();
            foreach (var pair in temporaryIgnored)
            {
                if(pair.Value <= localTime)
                    keysToRemove.Add(pair.Key);
            }
            foreach (var key in keysToRemove)
            {
                temporaryIgnored.Remove(key);
            }
        }

        bool FoundNewTarget(bool handleOnlyCallsForHelp = false)
        {
            callsForHelpMayBeCleared = true;

            AttackableUnit currentTarget = null;
            AttackableUnit nextTarget = currentTarget;
            int nextTargetPriority = (int)ClassifyUnit.DEFAULT;
            float acquisitionRange = GetEffectiveAcquisitionRange();
            float nextTargetDistanceSquared = acquisitionRange * acquisitionRange;
            int nextTargetAttackers = 0;
            if(targetIsStillValid)
            {
                currentTarget = LaneMinion.TargetUnit;
                nextTarget = currentTarget;
                nextTargetPriority = targetUnitPriority;
                nextTargetDistanceSquared = Vector2.DistanceSquared(LaneMinion.Position, nextTarget.Position);
                nextTargetAttackers = LaneMinion.IsFirstWave ? CountUnitsAttackingUnit(nextTarget) : 0;
            }
            
            FilterTemporaryIgnoredList();

            IEnumerable<AttackableUnit> nearestObjects;
            if(handleOnlyCallsForHelp)
            {
                if(unitsAttackingAllies.Count == 0)
                {
                    return false;
                }
                nearestObjects = unitsAttackingAllies.Keys;
            }
            else
            {
                nearestObjects = EnumerateUnitsInRange(LaneMinion.Position, acquisitionRange, true);
            }
            foreach (var it in nearestObjects)
            {
                if (it is AttackableUnit u && IsValidTarget(u) && !temporaryIgnored.ContainsKey(u.NetId))
                {
                    int priority = unitsAttackingAllies.ContainsKey(u) ?
                        unitsAttackingAllies[u]
                        : (int)LaneMinion.ClassifyTarget(u)
                    ;
                    float distanceSquared = Vector2.DistanceSquared(LaneMinion.Position, u.Position);
                    int attackers = LaneMinion.IsFirstWave ? CountUnitsAttackingUnit(u) : 0;
                    if (
                        nextTarget == null
                        || attackers < nextTargetAttackers
                        || (
                            attackers == nextTargetAttackers
                            && (
                                priority < nextTargetPriority
                                || (
                                    priority == nextTargetPriority
                                    && distanceSquared < nextTargetDistanceSquared
                                )
                            )
                        )
                    ) {
                        nextTarget = u;
                        nextTargetPriority = priority;
                        nextTargetDistanceSquared = distanceSquared;
                        nextTargetAttackers = attackers;
                    }
                }
            }
            
            if(nextTarget != null && nextTarget != currentTarget)
            {
                // This is the only place where the target is set
                LaneMinion.SetTargetUnit(nextTarget, true);
                targetUnitPriority = nextTargetPriority;
                timeSinceLastAttack = 0f;
                followsWaypoints = false;

                return true;
            }
            return false;
        }

        bool WaypointReached()
        {
            Vector2 currentWaypoint = GetSpreadedWaypoint(currentWaypointIndex);

            float radius = LaneMinion.CollisionRadius;
            Vector2 center = LaneMinion.Position;

            var nearestMinions = EnumerateUnitsInRange(LaneMinion.Position, LaneMinion.Stats.AcquisitionRange.Total, true)
                                .OfType<LaneMinion>()
                                .OrderBy(minion => Vector2.DistanceSquared(LaneMinion.Position, minion.Position) - minion.CollisionRadius);

            // This is equivalent to making any colliding minions equal to a single minion to save on pathfinding resources.
            foreach (LaneMinion minion in nearestMinions)
            {
                if(minion != LaneMinion){
                    // If the closest minion is in collision range, add its collision radius to the waypoint success range.
                    if (GameServerCore.Extensions.IsVectorWithinRange(minion.Position, center, radius + minion.CollisionRadius))
                    {
                        Vector2 dir = Vector2.Normalize(minion.Position - center);
                        //Vector2 pa = center + dir * (radius + minion.CollisionRadius * 2f);
                        //Vector2 pb = center - dir * radius;
                        //center = (pa + pb) * 0.5f;
                        //radius = (minion.CollisionRadius * 2f + radius * 2f) * 0.5f;

                        // Or simply
                        center += dir * minion.CollisionRadius;
                        radius += minion.CollisionRadius;
                    }
                    // If the closest minion (above) is not in collision range, then we stop the loop.
                    else break;
                }
            }

            float margin = 25f; // Otherwise, interferes with AttackableUnit which stops a little earlier 
            if (GameServerCore.Extensions.IsVectorWithinRange(currentWaypoint, center, radius + margin))
            {
                return true;
            }
            return false;
        }

        OrderType ReevaluateBehavior(float delta)
        {

            //Quote: Follow any current specialized behavior rules, such as from CC (Taunts, Flees, Fears)
            
            //Quote: Continue attacking (or moving towards) their current target if that target is still valid.
            if(targetIsStillValid)
            {
                //Quote: If they have failed to attack their target for 4 seconds, they temporarily ignore them instead.
                if(timeSinceLastAttack >= 4000f)
                {
                    Ignore(LaneMinion.TargetUnit);
                    targetIsStillValid = false;
                }
                else
                {
                    return OrderType.AttackTo;
                }
                    
            }

            //Quote: Find a new valid target in the minion’s acquisition range to attack.
            //Quote: If multiple valid targets, prioritize based on “how hard is it for me to path there?”
            if (FoundNewTarget())
            {
                return OrderType.AttackTo;
            }
            else if(LaneMinion.TargetUnit != null)
            {
                LaneMinion.CancelAutoAttack(false, true);
                LaneMinion.SetTargetUnit(null, true);
            }
            
            //Quote: Check if near a target waypoint, if so change the target waypoint to the next in the line.
            bool notYetOutOfRange = true;
            while(
                (notYetOutOfRange = currentWaypointIndex < LaneMinion.PathingWaypoints.Count)
                && WaypointReached()
            ) {
                currentWaypointIndex++;
            }

            //Quote: Walk towards the target waypoint.
            if(notYetOutOfRange)
            {
                Vector2 currentWaypoint = GetSpreadedWaypoint(currentWaypointIndex);
                Vector2 currentDestination = LaneMinion.Waypoints[LaneMinion.Waypoints.Count - 1];

                if(currentDestination != currentWaypoint)
                {
                    List<Vector2> path = null;

                    // If the minion returns to lane
                    // Instead of continuing to move along the waypoints
                    if(!followsWaypoints)
                    {
                        followsWaypoints = true;
                        path = GetPath(LaneMinion.Position, currentWaypoint, LaneMinion.PathfindingRadius);
                    }

                    if(path == null)
                    {
                        if(LaneMinion.SpreadSlotIndex >= 1)
                        {
                            Vector2 prev = currentWaypointIndex > 0
                                ? LaneMinion.PathingWaypoints[currentWaypointIndex - 1]
                                : LaneMinion.SpawnPosition;
                            Vector2 axis = LaneMinion.PathingWaypoints[currentWaypointIndex] - prev;
                            if(axis.LengthSquared() > 0.001f)
                            {
                                Vector2 dirAxis = Vector2.Normalize(axis);
                                Vector2 leftPerp = new Vector2(-dirAxis.Y, dirAxis.X);
                                Vector2 offset = ComputeLateralOffset(LaneMinion.SpreadSlotIndex, leftPerp);

                                Vector2 toAxis = LaneMinion.Position - prev;
                                float along = Vector2.Dot(toAxis, dirAxis);
                                Vector2 onAxis = prev + dirAxis * along;
                                float forwardLength = MathF.Max(300f, offset.Length() * 3f);
                                Vector2 intermediate = onAxis + dirAxis * forwardLength + offset;

                                path = new List<Vector2> { LaneMinion.Position, intermediate, currentWaypoint };
                            }
                        }

                        if(path == null)
                        {
                            path = new List<Vector2>()
                            {
                                LaneMinion.Position,
                                currentWaypoint
                            };
                        }
                    }
                    LaneMinion.SetWaypoints(path);
                }
                
                return OrderType.MoveTo;
            }

            return OrderType.Stop;
        }
    }
}