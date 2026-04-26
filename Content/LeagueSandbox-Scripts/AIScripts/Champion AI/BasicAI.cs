using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Collections.Generic;
using System.Numerics;
using System;
using System.Linq;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.Chatbox;
using LeagueSandbox.GameServer.Players;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.Logging;
using log4net;
using LeagueSandbox.GameServer;
using Timer = System.Timers.Timer;
using LeagueSandbox.GameServer.Inventory;


namespace AIScripts
{
    public class BasicAI : IAIScript
    {
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData();
        public Champion ChampionInstance { get; private set; }
        public BotState currentBotState { get; private set; } // Public getter, private setter

        private float TimeSinceLastDamage { get; set; }
        private const float combatDuration = 10.0f;
        private float combatTimer = 0.0f;
        private const float recallDelay = 5.0f;
        private bool isInCombat = false;
        private bool isUnderTower = false;
        private bool isRoaming = false;
        private const float healthThreshold = 0.3f; // Adjust to simulate more cautious or aggressive play
        private static ILog _logger = LoggerProvider.GetLogger();
        private readonly Game _game;
        private BotState _currentState = BotState.Idle;

        private float _stateCooldown = 5.0f; // Time before the bot can switch states
        private const float decisionCooldown = 5.0f; // 2 seconds cooldown between decisions
        private static Random random = new Random();

        public enum BotState
        {
            Idle,
            Farming,
            AttackingEnemy,
            Retreating,
            Recalling,
            Roaming,
            AttackingTower,
            FollowinMinions, //TODO
            HealingInBase, //TODO
            Jungling, //TODO
            Retaliating, //TODO
            DefendingTower, //TODO
            SlowPushing, //TODO
        }

        public void OnActivate(ObjAIBase owner)
        {
            if (owner is Champion champion)
            {
                ChampionInstance = champion;
                ChampionInstance.IsBot = true;

                ApiEventManager.OnTakeDamage.AddListener(this, ChampionInstance, OnTakeDamage, false);
                ChampionInstance.LevelUp();
                _logger.Debug("Bot initialized with state: " + currentBotState);

                isInCombat = false;
                isUnderTower = false;
                isRoaming = false;
            }
        }

        public void OnUpdate(float diff)
        {
            if (ChampionInstance != null && !ChampionInstance.IsDead)
            {
                // Log the initial state before updating timers
                _logger.Debug(
                    $"Before Update: TimeSinceLastDamage = {TimeSinceLastDamage}, combatTimer = {combatTimer}, _stateCooldown = {_stateCooldown}");

                TimeSinceLastDamage += diff;
                combatTimer += diff;
                _stateCooldown = Math.Max(0.0f, _stateCooldown - diff);

                // Log the state after updating timers
                _logger.Debug(
                    $"After Update: TimeSinceLastDamage = {TimeSinceLastDamage}, combatTimer = {combatTimer}, _stateCooldown = {_stateCooldown}");

                if (_stateCooldown <= 0.0f)
                {
                    _logger.Debug("Cooldown has expired, deciding next state...");

                    DecideNextState();

                    _stateCooldown = decisionCooldown; // Reset cooldown after decision
                    _logger.Debug($"New state decided. _stateCooldown reset to {decisionCooldown}");
                    _logger.Debug($"The new state is: {_currentState}");
                }

                HandleCurrentState();

                // Optionally log the current state to check if it's being handled correctly
                _logger.Debug("Handling current state...");
            }
            else
            {
                if (ChampionInstance == null)
                {
                    _logger.Debug("ChampionInstance is null.");
                }
                else if (ChampionInstance.IsDead)
                {
                    _logger.Debug("ChampionInstance is dead.");
                }
            }
        }

        private void DecideNextState()
        {
            // Low health: retreat or recall
            if (ChampionInstance.Stats.CurrentHealth / ChampionInstance.Stats.HealthPoints.Total < healthThreshold)
            {
                StopAttack();
                _currentState = BotState.Retreating;
                return;
            }

            // If there are enemy champions nearby and health is sufficient: attack
            List<Champion> nearbyEnemies = GetNearbyChampions();
            if (nearbyEnemies.Count > 0 && ShouldBeAggressive())
            {
                _currentState = BotState.AttackingEnemy;
                return;
            }

            // If under a tower: attack the tower
            if (IsUnderTower(ChampionInstance.Position))
            {
                _currentState = BotState.AttackingTower;
                return;
            }

            // No enemies and minions nearby: farm
            List<AttackableUnit> nearbyMinions = GetNearbyMinions();
            if (nearbyMinions.Count > 0)
            {
                _currentState = BotState.Farming;
                return;
            }

            // Roam if no other activities are available
            _currentState = BotState.Roaming;
        }

        private void HandleCurrentState()
        {
            switch (_currentState)
            {
                case BotState.Farming:
                    FarmMinions(GetNearbyMinions());
                    break;
                case BotState.AttackingEnemy:
                    Attack(GetClosestEnemyChampion());
                    break;
                case BotState.Retreating:
                    RetreatOrRecall();
                    break;
                case BotState.Recalling:
                    Recall();
                    break;
                case BotState.Roaming:
                    Roam();
                    break;
                case BotState.AttackingTower:
                    AttackTowers();
                    break;
                case BotState.Idle:
                default:
                    // Stay idle if no other state is active
                    break;
            }
        }

        private BotState _currentBotState; // Backing field for the state

        public BotState CurrentBotState
        {
            get => _currentBotState;
            private set
            {
                if (_currentBotState != value) // Only log if the state is actually changing
                {
                    _logger.Debug($"Bot state changed from {_currentBotState} to {value}");
                    _currentBotState = value;
                }
            }
        }

        public void SetBotState(BotState newState)
        {
            if (_currentBotState != newState)
            {
                _currentBotState = newState; // This triggers the setter, logging the state change
            }
        }

        private bool ShouldRecall()
        {
            return TimeSinceLastDamage > recallDelay &&
                   ChampionInstance.Stats.CurrentHealth / ChampionInstance.Stats.HealthPoints.Total < healthThreshold;
        }

        private void Recall()
        {
            Vector2 basePosition = GetBasePosition();
            _logger.Debug($"Recalling to base position {basePosition}");
            MoveToPosition(basePosition);
            // Add recall animation or actual recall logic if applicable
        }

        private void RetreatOrRecall()
        {
            List<Champion> nearbyEnemies = GetNearbyChampions();
            if (nearbyEnemies.Count == 0)
            {
                Recall();
            }
            else
            {
                MoveTowardsBase();
            }
        }

        private void FarmMinionsOrDefend()
        {
            List<AttackableUnit> nearbyMinions = GetNearbyMinions();
            if (nearbyMinions.Count > 0)
            {
                FarmMinions(nearbyMinions);
            }
            else
            {
                FollowAlliedMinions();
            }
        }

        private bool ShouldRoam()
        {
            // If the bot's health is below the threshold, it can't roam.
            if (ChampionInstance.Stats.CurrentHealth < healthThreshold)
            {
                return false;
            }

            // Existing logic for roaming when there are no nearby minions and a 10% chance
            return isRoaming || (GetNearbyMinions().Count == 0 && random.Next(0, 100) < 10);
        }

        private void Roam()
        {
            isRoaming = true;
            Vector2 roamTarget = GetRoamTarget();
            _logger.Debug($"Roaming to target position {roamTarget}");
            MoveToPosition(roamTarget);

            // Cancel roaming if under attack
            if (isInCombat)
            {
                isRoaming = false;
            }
        }

        private Vector2 GetRoamTarget()
        {
            // Select a random target location in another lane or near an objective
            List<Vector2> potentialTargets = new List<Vector2>
            {
                new Vector2(12000, 2000), // Top lane
                new Vector2(3000, 12000), // Bot lane
                new Vector2(7500, 7500) // Mid lane or jungle
            };
            return potentialTargets[new Random().Next(0, potentialTargets.Count)];
        }

        private void OnTakeDamage(DamageData damageData)
        {
            if (ChampionInstance != null)
            {
                combatTimer = 0.0f;
                TimeSinceLastDamage = 0.0f;
            }
        }

        private void Attack(AttackableUnit attacker)
        {
            if (attacker != null && !attacker.IsDead)
            {
                // Check if the bot should be retreating instead of attacking
                if (currentBotState == BotState.Retreating || ChampionInstance.Stats.CurrentHealth < healthThreshold)
                {
                    StopAttack();
                    isInCombat = false;
                    _logger.Debug(
                        "Skipping attack due to retreat or low health."); // this doesn't actually stop the attack, not sure why
                    return;
                }

                Vector2 attackerPosition = attacker.Position;
                _logger.Debug($"Attacking target at position {attackerPosition}");
                ChampionInstance.UpdateMoveOrder(OrderType.AttackMove);
                ChampionInstance.SetWaypoints(new List<Vector2> { attackerPosition });
                ChampionInstance.SetTargetUnit(attacker);
            }
        }

        private void StopAttack()
        {
            {
                // Log that the bot is stopping the attack
                _logger.Debug("Stopping the attack.");

                // Clear the current attack target and stop any queued actions
                ChampionInstance.SetTargetUnit(null);
                ChampionInstance.UpdateMoveOrder(OrderType.Stop); // Issue a stop command to halt movement/attacks
                isInCombat = false;

                // Optionally reset waypoints if the bot was moving towards a target
                ChampionInstance.SetWaypoints(new List<Vector2>());

                // Change the bot's state to idle or retreating based on context
                currentBotState = BotState.Idle; // Or BotState.Retreating, depending on your game's logic
            }
        }

        private void MoveToPosition(Vector2 targetPosition)
        {
            Vector2 botPosition = ChampionInstance.Position;
            _logger.Debug($"Moving from {botPosition} to {targetPosition}");
            List<Vector2> waypoints = new List<Vector2> { botPosition, targetPosition };
            ChampionInstance.MoveOrder = OrderType.MoveTo;
            ChampionInstance.SetWaypoints(waypoints);
        }

        private void MoveTowardsBase()
        {
            Vector2 basePosition = GetBasePosition();
            {
                // move towards the base position
                MoveToPosition(basePosition);
            }
        }

        private bool ShouldBeAggressive()
        {
            return ChampionInstance.Stats.CurrentHealth > healthThreshold
                   && ChampionInstance.Stats.CurrentMana > 0.3f
                   && GetNearbyChampions().Count > 0;
        }

        private List<Champion> GetNearbyChampions()
        {
            Vector2 botPosition = ChampionInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, 2000.0f, true);
            return units.OfType<Champion>()
                .Where(champion => champion.Team != ChampionInstance.Team && !champion.IsDead).ToList();
        }

        private Champion GetClosestEnemyChampion()
        {
            Vector2 botPosition = ChampionInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, 2000.0f, true);
            var nearbyChampions = units.OfType<Champion>()
                .Where(champion => champion.Team != ChampionInstance.Team && !champion.IsDead).ToList();

            // Ensure there are champions in the list
            if (nearbyChampions.Any())
            {
                // Find the closest champion
                return nearbyChampions
                    .OrderBy(champion => Vector2.Distance(botPosition, champion.Position))
                    .FirstOrDefault();
            }

            return null; // Return null if no valid champions are found
        }

        private List<AttackableUnit> GetNearbyMinions()
        {
            Vector2 botPosition = ChampionInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, 2000.0f, true);
            return units.Where(unit => unit is LaneMinion).ToList();
        }

        private void FarmMinions(List<AttackableUnit> minions)
        {
            if (minions.Count > 0)
            {
                foreach (AttackableUnit minion in minions)
                {
                    if (!minion.IsDead)
                    {
                        _logger.Debug($"Farming minion at position {minion.Position}");
                        Attack(minion);
                        return; // Focus on one minion at a time
                    }
                }
            }
        }

        private void FollowAlliedMinions()
        {
            Vector2 botPosition = ChampionInstance.Position;
            float detectionRange = 2000.0f;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, detectionRange, true);
            List<AttackableUnit> alliedMinions =
                units.Where(unit => unit is Minion && unit.Team == ChampionInstance.Team).ToList();

            if (alliedMinions.Count > 0)
            {
                Vector2 averageMinionPosition = Vector2.Zero;
                foreach (var minion in alliedMinions)
                {
                    averageMinionPosition += minion.Position;
                }

                averageMinionPosition /= alliedMinions.Count;
                MoveToPosition(averageMinionPosition);
            }
        }

        private void AttackTowers()
        {
            Vector2 botPosition = ChampionInstance.Position;
            if (IsUnderTower(botPosition))
            {
                AttackableUnit nearestTower = GetNearestTower(botPosition);
                List<AttackableUnit> minionsInRange = GetUnitsInRange(nearestTower.Position, 450.0f, true)
                    .Where(unit => unit is LaneMinion && unit.Team == ChampionInstance.Team).ToList();

                if (minionsInRange.Count > 0)
                {
                    Attack(nearestTower);
                    _logger.Debug("Attacking tower at {tower.Position}");
                }
            }
        }

        private bool IsUnderTower(Vector2 position)
        {
            float towerRange = 1500.0f;
            isUnderTower = true;
            return GetUnitsInRange(position, towerRange, true).Any(unit => unit is LaneTurret);
        }

        private AttackableUnit GetNearestTower(Vector2 position)
        {
            List<AttackableUnit> LaneTurret = GetTowers();
            return LaneTurret.OrderBy(tower => Vector2.Distance(position, tower.Position)).FirstOrDefault();
        }

        private List<AttackableUnit> GetTowers()
        {
            // Get a list of all towers in the game
            return GetUnitsInRange(Vector2.Zero, float.MaxValue, true)
                .Where(unit => unit is LaneTurret && unit.Team != ChampionInstance.Team).ToList();
        }

        private Vector2 GetBasePosition()
        {
            // Get the team's base position, depending on the team
            if (ChampionInstance.Team == TeamId.TEAM_BLUE)
            {
                return new Vector2(500, 500); // Blue team base
            }
            else
            {
                return new Vector2(14200, 14200); // Red team base
            }
        }

        private List<AttackableUnit> GetUnitsInRange(Vector2 position, float range, bool includeDeadUnits)
        {
            // Retrieves all units within the specified range
            return EnumerateValidUnitsInRange(ChampionInstance, position, range, includeDeadUnits,
                SpellDataFlags.AffectEnemies).ToList();
        }
    }
}