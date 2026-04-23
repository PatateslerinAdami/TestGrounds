using GameMaths;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.NetInfo;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Chatbox;
using LeagueSandbox.GameServer.Content.Navigation;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Players;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net;
using Newtonsoft.Json.Bson;
using PacketDefinitions420;
using Spells;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static GameServerLib.GameObjects.AttackableUnits.DamageData;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LENet.Protocol;
using Timer = System.Timers.Timer;
using AIScripts.Common;
using static AIScripts.Common.BotCommunicationAPI;
using static AIScripts.Common.BotLaneAPI;

namespace AIScripts
{
    public class EzrealBot : IAIScript
    {
        // Champion reference
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData();

        private Champion EzrealInstance;
        private static ILog _logger = LoggerProvider.GetLogger();
        // Mid lane waypoints (matching minion path from Map1)
        private int _currentWaypointIndex = 0;
        private const float WaypointReachedThreshold = 300f; // Distance to consider waypoint reached
        private BotState _currentState;
        private float _gameTime;
        private float _lastDamageTakenTime;
        private bool isInCombat;
        private bool isUnderTower;
        private bool MovingToLane;
        private Champion _followTarget;
        private const float CombatCooldownTime = 5.0f; // Time to consider out of combat (seconds)

        // Personality seed for hive mind diversity
        private int _personalitySeed;
        private Random _personalityRandom;
        private Vector2 _currentOffset = Vector2.Zero;
        private float _offsetUpdateTime = 0f;
        private const float OffsetUpdateInterval = 2.0f; // Update offset every 2 seconds
        private const float MaxPositionOffset = 150f; // Maximum offset in units

        // Spell slots
        private readonly byte QSlot = 0;
        private readonly byte WSlot = 1;
        private readonly byte ESlot = 2;
        private readonly byte RSlot = 3;
        private readonly byte IgniteSlot = 0; // Summoner spell slot
        private Dictionary<byte, float> _lastCastTime = new Dictionary<byte, float>();
        
        // Mid lane waypoints (will be generated dynamically based on team)
        private List<Vector2> _midLaneWaypoints = new List<Vector2>();

        // Attack weaving / Orbwalking fields
        private float _autoAttackCooldownEndTime = 0f;
        private bool _isAutoAttacking = false;
        private Vector2 _orbwalkDirection = Vector2.Zero;
        private float _lastOrbwalkTime = 0f;
        private const float MinOrbwalkInterval = 0.3f; // Minimum time between direction changes
        private const float OrbwalkMoveSpeedMultiplier = 0.7f; // Move at 70% speed during orbwalking

        // Constants for decision making
        private const float SafeDistance = 650f; // Safe distance to keep from enemies
        private const float QRange = 1150f;
        private const float WRange = 1000f;
        private const float ERange = 475f;
        private const float RRange = 25000f; // Global but practical limit
        private const float AggressiveHealthThreshold = 0.4f; // 40% health to play aggressive
        private const float DefensiveHealthThreshold = 0.3f; // 30% health to play defensive
        private const float MinManaForCombo = 0.4f; // 40% mana needed for full combo
        private readonly float EnemyDetectionRange = 1200.0f;
        private readonly float AllyDetectionRange = 1200.0f;
        private readonly float TowerDetectionRange = 1200.0f;
        private const float AutoAttackRange = 550f; // Ezreal's base auto attack range
        private DateTime _lastPokeTime = DateTime.MinValue;
        private readonly TimeSpan _pokeCooldown = TimeSpan.FromSeconds(3); // Adjust as needed
        private Champion _lastChaseTarget = null;
        bool stillChasing = false;

        // Push and dive constants
        private const float DiveHealthThreshold = 0.2f; // Enemy health below 20% for diving
        private const float MinDiveScore = 2.0f; // Minimum score to consider diving
        private const float TowerDiveSafeDistance = 775f; // Distance to stay from tower while diving
        private Champion _diveTarget = null;

        // Item shopping configuration
        private List<int> _itemBuildOrder = new List<int>(); // Item IDs to buy in order
        private int _currentBuildIndex = 0;
        private const float FountainShopRange = 1000f; // Distance from fountain to shop
        private bool _hasItemsToBuy = true;

        // Lane selection configuration
        private BotLane _assignedLane = BotLane.Mid;
        private bool _hasSelectedLane = false;
        private bool _isRecallingForLane = false;

        // Bot communication settings and tracking
        private BotSettings _botSettings;
        private float _lastPingTime = 0f;
        private float _lastToxicPingTime = 0f;
        private HashSet<uint> _trackedDeadAllies = new HashSet<uint>();
        private ToxicPingSpamState _toxicPingSpamState = new ToxicPingSpamState();

        // Enum for bot state
        public enum BotState
        {
            MovingToLane,
            Farming,
            Poking,
            Aggressive,
            Defensive,
            Retreating,
            Chasing,
            Pushing,
            Diving,
            DeadState,
            Shopping
        }

        public void OnActivate(ObjAIBase owner)
        {
            if (owner is Champion champion)
            {
                EzrealInstance = champion;
                EzrealInstance.IsBot = true;

                // Initialize personality seed for hive mind diversity
                _personalitySeed = EzrealInstance.NetId.GetHashCode() + Environment.TickCount;
                _personalityRandom = new Random(_personalitySeed);
                _logger.Debug($"Ezreal Bot initialized with personality seed: {_personalitySeed}");

                _currentState = BotState.MovingToLane;
                ApiEventManager.OnTakeDamage.AddListener(this, EzrealInstance, OnTakeDamage, false);
                ApiEventManager.OnDeath.AddListener(this, EzrealInstance, OnDeath, false);
                ApiEventManager.OnKill.AddListener(this, EzrealInstance, OnKillChampion, false);

                // Initialize trash talk system with randomized cooldown
                InitializeTrashTalk();

                // Initialize bot communication settings (set to SeriousOnly for now, change to ToxicOnly or Default as desired)
                _botSettings = BotSettings.SeriousOnly;
                _logger.Debug($"Ezreal Bot communication settings - TrashTalk: {_botSettings.EnableTrashTalk}, ToxicPings: {_botSettings.EnableToxicPings}, SeriousPings: {_botSettings.EnableSeriousPings}");

                // Initialize with first skill point
                EzrealInstance.LevelUpSpell(QSlot); // Start with Q for lane poke

                Console.WriteLine("Ezreal Bot initialized with state: " + _currentState);

                // Initialize dynamic waypoints based on team
                InitializeWaypoints();

                // Select lane immediately when bot is activated
                SelectLane();

                _itemBuildOrder = new List<int>
                {
                    3153,
                    3078,
                    3031,
                    3006,
                    3046,
                    3142
                };
            }
        }

        /// <summary>
        /// Selects the optimal lane for this bot based on team composition
        /// </summary>
        private void SelectLane()
        {
            if (EzrealInstance == null)
                return;

            // Check if another ally is already recalling for lane selection
            if (IsAllyRecalling(EzrealInstance))
            {
                _logger.Debug($"Ally is already recalling for lane selection, waiting...");
                return;
            }

            // Check if we should recall due to lane overflow
            if (ShouldRecallDueToOverflow(EzrealInstance))
            {
                // Mark ourselves as recalling to prevent other bots from also recalling
                MarkBotAsRecalling(EzrealInstance);
                _isRecallingForLane = true;
                _logger.Debug($"Lane overflow detected, recalling to choose new lane...");

                // Trigger recall
                EzrealInstance.Recall();
                return;
            }

            // Select the optimal lane
            _assignedLane = GetTargetLane(EzrealInstance);
            _hasSelectedLane = true;

            // Assign this bot to the lane in the API
            AssignBotToLane(EzrealInstance, _assignedLane);

            _logger.Debug($"Selected lane: {_assignedLane}");

            // Update waypoints based on selected lane
            InitializeLaneWaypoints();
        }

        /// <summary>
        /// Initializes waypoints based on the assigned lane
        /// </summary>
        private void InitializeLaneWaypoints()
        {
            if (EzrealInstance == null)
                return;

            // Base waypoints for each lane (blue side perspective)
            List<Vector2> laneWaypoints;

            switch (_assignedLane)
            {
                case BotLane.Top:
                    laneWaypoints = new List<Vector2>
                    {
                        new Vector2(917.0f, 1725.0f),    // Blue fountain area
                        new Vector2(1170.0f, 4041.0f),    // Near blue outer turret
                        new Vector2(861.0f, 6459.0f),      // Mid lane center area
                        new Vector2(880.0f, 10180.0f),     // Mid lane center area
                        new Vector2(1268.0f, 11675.0f),    // Near purple outer turret
                        new Vector2(2806.0f, 13075.0f),    // Purple fountain area (enemy nexus)
                        new Vector2(3907.0f, 13243.0f),
                        new Vector2(7550.0f, 13407.0f),
                        new Vector2(10244.0f, 13238.0f),
                        new Vector2(10947.0f, 13135.0f),
                        new Vector2(12511.0f, 12776.0f)
                    };
                    break;

                case BotLane.Bottom:
                    laneWaypoints = new List<Vector2>
                    {
                        new Vector2(1487.0f, 1302.0f),   // Blue fountain area
                        new Vector2(3789.0f, 1346.0f),   // Near blue outer turret
                        new Vector2(6430.0f, 1005.0f),   // Mid lane center area
                        new Vector2(10995.0f, 1234.0f),  // Mid lane center area
                        new Vector2(12841.0f, 3051.0f),  // Near purple outer turret
                        new Vector2(13148.0f, 4202.0f),  // Purple fountain area (enemy nexus)
                        new Vector2(13249.0f, 7884.0f),
                        new Vector2(12886.0f, 10356.0f),
                        new Vector2(12511.0f, 12776.0f)
                    };
                    break;

                case BotLane.Mid:
                default:
                    laneWaypoints = new List<Vector2>
                    {
                        new Vector2(1418.0f, 1686.0f),   // Blue fountain area
                        new Vector2(2997.0f, 2781.0f),   // Near blue outer turret
                        new Vector2(4472.0f, 4727.0f),   // Mid lane center area
                        new Vector2(8375.0f, 8366.0f),   // Mid lane center area
                        new Vector2(10948.0f, 10821.0f), // Near purple outer turret
                        new Vector2(12511.0f, 12776.0f)  // Purple fountain area (enemy nexus)
                    };
                    break;
            }

            if (EzrealInstance.Team == TeamId.TEAM_BLUE)
            {
                // Blue team: use waypoints in forward order (from blue base to purple base)
                _midLaneWaypoints = laneWaypoints;
            }
            else
            {
                // Purple team: reverse the waypoints (from purple base to blue base)
                _midLaneWaypoints = new List<Vector2>(laneWaypoints);
                _midLaneWaypoints.Reverse();
            }
            _logger.Debug($"Initialized {_midLaneWaypoints.Count} waypoints for lane {_assignedLane} on team {EzrealInstance.Team}");
        }

        public void OnDeath(DeathData deathData)
        {
            // deathData.Unit is the unit that died (the victim)
            // Check if this bot is the one who died
            if (deathData.Unit == EzrealInstance)
            {
                _deathCount++;
                _logger.Debug($"EzrealBot died! Total deaths: {_deathCount}");
                // Queue delayed reaction instead of immediate trash talk
                QueueReactionMessage(_dyingTaunts);
                

                // Send death trash talk using the new API
                SendDeathTrashTalk(EzrealInstance, _botSettings);

                // Request help ping at death position (serious ping to alert allies)
                if (_botSettings.EnableSeriousPings && _gameTime - _lastPingTime >= _botSettings.PingCooldown)
                {
                    SendRequestHelpPing(EzrealInstance, _botSettings, EzrealInstance.Position);
                    _lastPingTime = _gameTime;
                }

                // Set flag to indicate we need to shop when respawning
                _hasItemsToBuy = true;

                // Clear recalling status on death
                ClearBotRecallingStatus(EzrealInstance);
                _isRecallingForLane = false;
            }
        }

        public void OnKillChampion(DeathData deathData)
        {
            // Only process if this bot is the killer
            if (deathData.Killer == EzrealInstance && deathData.Unit is Champion && deathData.Unit is not LaneMinion)
            {
                _killCount++;
                _logger.Debug($"EzrealBot got a kill! Total kills: {_killCount}");
                // Queue delayed reaction instead of immediate trash talk
                QueueReactionMessage(_killingTaunts);

                // Send kill trash talk using the new API
                SendKillTrashTalk(EzrealInstance, _botSettings);

                // Send an "On My Way" ping to the enemy position (serious ping for coordinating with allies)
                if (_botSettings.EnableSeriousPings && _gameTime - _lastPingTime >= _botSettings.PingCooldown)
                {
                    SendOnMyWayPing(EzrealInstance, _botSettings, deathData.Unit.Position);
                    _lastPingTime = _gameTime;
                }
            }
        }




        public void OnUpdate(float diff)
        {
            // Guard every field that could be null
            if (EzrealInstance == null)
            {
                _logger.Debug("[EzrealBot] OnUpdate called but EzrealInstance is null, skipping.");
                return;
            }

            _gameTime += diff / 1000f; // Convert to seconds

            // Check if combat state has expired
            if (isInCombat && _gameTime - _lastDamageTakenTime > CombatCooldownTime)
            {
                isInCombat = false;
            }

            // Handle lane selection logic
            if (!_hasSelectedLane && ShouldSelectLanes())
            {
                SelectLane();
            }

            // Handle recall for lane selection
            if (_isRecallingForLane)
            {
                // Check if we've respawned at fountain (teleport completed)
                // This is a simple check - if we're near our base and not moving, recall is complete
                if (IsNearFountain())
                {
                    // Clear recalling status
                    ClearBotRecallingStatus(EzrealInstance);
                    _isRecallingForLane = false;

                // Select new lane
                _assignedLane = GetTargetLane(EzrealInstance);
                _hasSelectedLane = true;

                // Reassign this bot to the new lane
                AssignBotToLane(EzrealInstance, _assignedLane);

                _logger.Debug($"After recall, selected new lane: {_assignedLane}");

                // Update waypoints
                InitializeLaneWaypoints();

                    // Reset state to moving to lane
                    _currentState = BotState.MovingToLane;
                }
            }

            // Update personality offset periodically
            UpdatePersonalityOffset();

            // Process any pending delayed reaction messages (kill/death taunts)
            ProcessDelayedMessages();

            TryTrashTalk();

            // Process any active toxic ping spam sequence (sends pings at intervals)
            ProcessToxicPingSpam(_botSettings, _gameTime, ref _lastToxicPingTime, _toxicPingSpamState);

            // Check for dead allies and start new toxic ping spam sequences if appropriate
            CheckForDeadAlliesAndToxicPing(EzrealInstance, _botSettings, _gameTime, ref _lastToxicPingTime, _trackedDeadAllies, _toxicPingSpamState);

            // Track auto-attack state for orbwalking
            UpdateAutoAttackState();

            // Level up skills when possible
            LevelUpSpells();

            // Update state information
            _followTarget = GetClosestAllyChampion();
            isUnderTower = IsUnderEnemyTower();

            // Update bot state based on game conditions
            UpdateState();
            _logger.Debug($"Current state: {_currentState}");


            // Act based on the current state
            ActOnState();
        }

        /// <summary>
        /// Updates the randomized positioning offset for hive mind diversity
        /// </summary>
        private void UpdatePersonalityOffset()
        {
            if (_gameTime - _offsetUpdateTime >= OffsetUpdateInterval)
            {
                _offsetUpdateTime = _gameTime;
                // Generate random offset within circle
                float angle = (float)(_personalityRandom.NextDouble() * 2 * Math.PI);
                float distance = (float)(_personalityRandom.NextDouble() * MaxPositionOffset);
                _currentOffset = new Vector2(
                    (float)(distance * Math.Cos(angle)),
                    (float)(distance * Math.Sin(angle))
                );
            }
        }

        /// <summary>
        /// Tracks auto-attack state for orbwalking decision making
        /// </summary>
        private void UpdateAutoAttackState()
        {
            if (EzrealInstance.AutoAttackSpell != null)
            {
                var spellState = EzrealInstance.AutoAttackSpell.State;
                _isAutoAttacking = (spellState == SpellState.STATE_CASTING);
            }
        }

        private void OnTakeDamage(GameServerLib.GameObjects.AttackableUnits.DamageData damageData)
        {
            _lastDamageTakenTime = _gameTime;
            isInCombat = true;

            // If damage from enemy champion, react!
            if (damageData.Attacker is Champion enemyChampion && enemyChampion.Team != EzrealInstance.Team)
            {
                _logger.Debug("Taking damage from enemy champion!");

                // If health low, be defensive
                if (GetHealthPercentage() < DefensiveHealthThreshold)
                {
                    _currentState = BotState.Defensive;
                }
                // Otherwise fight back if we have good health
                else if (GetHealthPercentage() > AggressiveHealthThreshold)
                {
                    // Check if we can safely attack
                    if (IsSpellAvailable(QSlot, SpellSlotType.SpellSlots))
                    {
                        _currentState = BotState.Aggressive;

                        // Send attack ping if allies are nearby and cooldown has passed
                        if (_botSettings.EnableSeriousPings && _gameTime - _lastPingTime >= _botSettings.PingCooldown)
                        {
                            var nearestAlly = GetNearestAlly(EzrealInstance, _botSettings.AllyNearbyDistance);
                            if (nearestAlly != null)
                            {
                                SendAttackPing(EzrealInstance, _botSettings, enemyChampion.Position);
                                _lastPingTime = _gameTime;
                            }
                        }

                        // Force immediate Q cast on attacker
                        CastQ(enemyChampion);
                    }
                }
            }
        }


        private void UpdateState()
        {
            // Check if dead
            if (EzrealInstance.IsDead)
            {
                _currentState = BotState.DeadState;
                return;
            }
            
            // Check if we should shop (just respawned and at fountain with items to buy)
            // Only enter shopping state if we can actually afford something
            if (!EzrealInstance.IsDead && _hasItemsToBuy && IsNearFountain())
            {
                if (CanAffordNextItem())
                {
                    _currentState = BotState.Shopping;
                    return;
                }
                else
                {
                    // Can't afford anything, reset shopping flag and go farm
                    _logger.Debug("Cannot afford next item, resuming farming");
                    _hasItemsToBuy = false;
                }
            }
            
            // Check if we should recall due to lane overflow (only before minions spawn)
            if (!EzrealInstance.IsDead && ShouldSelectLanes() && ShouldRecallDueToOverflow(EzrealInstance) && !_isRecallingForLane)
            {
                // Check if another ally is already recalling
                if (!IsAllyRecalling(EzrealInstance))
                {
                    MarkBotAsRecalling(EzrealInstance);
                    _isRecallingForLane = true;
                    _logger.Debug($"Lane overflow detected, recalling to choose new lane...");
                    EzrealInstance.Recall();
                }
            }

            if (!EzrealInstance.IsDead && !isInCombat && !ShouldPushTower() && _currentState != BotState.Shopping && !_isRecallingForLane)
            {
                _currentState = BotState.MovingToLane;
            }

            // Get nearby enemies
            List<Champion> nearbyEnemies = GetNearbyEnemyChampions(1200f);

            // State logic
            switch (_currentState)
            {
                case BotState.MovingToLane:
                    _currentState = BotState.Farming;
                    break;

                case BotState.Farming:
                    if (nearbyEnemies.Count > 0 && CanPoke())
                    {
                        _currentState = BotState.Poking;
                    }
                    // Add the check for chase state here
                    else if (ShouldChaseEnemy())
                    {
                        _logger.Debug("Found vulnerable enemy - switching to chase mode");
                        _currentState = BotState.Chasing;
                    }
                    // Check for tower pushing opportunity - aggressive check for nearby towers
                    else if (ShouldPushTower())
                    {
                        _logger.Debug("Tower push opportunity detected - switching to push mode");
                        _currentState = BotState.Pushing;
                    }
                    // Additional check: if there's an enemy tower nearby and we're healthy, switch to pushing
                    else if (GetHealthPercentage() > 0.6f)
                    {
                        LaneTurret nearbyTower = GetNearbyEnemyTower(1000f);
                        if (nearbyTower != null)
                        {
                            float distanceToTower = Vector2.Distance(EzrealInstance.Position, nearbyTower.Position);
                            if (distanceToTower <= AutoAttackRange + 300f)
                            {
                                _logger.Debug($"Enemy tower nearby at {distanceToTower:F0} units - switching to push mode");
                                _currentState = BotState.Pushing;
                            }
                        }
                    }
                    break;

                case BotState.Poking:
                    if (GetHealthPercentage() < DefensiveHealthThreshold)
                    {
                        _currentState = BotState.Defensive;
                    }
                    else if (CanGoAggressive(nearbyEnemies))
                    {
                        _currentState = BotState.Aggressive;
                    }
                    // Add chase transition from poking state
                    else
                    {
                        // Check if any enemy is vulnerable
                        bool foundVulnerable = false;
                        Champion vulnerableTarget = null;
                        foreach (var enemy in nearbyEnemies)
                        {
                            if (IsChampionVulnerable(enemy))
                            {
                                _currentState = BotState.Chasing;
                                foundVulnerable = true;
                                vulnerableTarget = enemy;
                                break;
                            }
                        }

                        // Send OnMyWay ping if chasing with allies nearby
                        if (foundVulnerable && _botSettings.EnableSeriousPings && _gameTime - _lastPingTime >= _botSettings.PingCooldown)
                        {
                            var nearestAlly = GetNearestAlly(EzrealInstance, _botSettings.AllyNearbyDistance);
                            if (nearestAlly != null && vulnerableTarget != null)
                            {
                                SendOnMyWayPing(EzrealInstance, _botSettings, vulnerableTarget.Position);
                                _lastPingTime = _gameTime;
                            }
                        }

                        // If no vulnerable enemies and no enemies in range, go back to farming
                        if (!foundVulnerable && nearbyEnemies.Count == 0)
                        {
                            _currentState = BotState.Farming;
                        }
                    }
                    break;


                case BotState.Aggressive:
                    if (GetHealthPercentage() < DefensiveHealthThreshold)
                    {
                        _currentState = BotState.Defensive;
                    }
                    else if (!CanGoAggressive(nearbyEnemies))
                    {
                        _currentState = BotState.Poking;
                    }
                    break;

                case BotState.Defensive:
                    if (GetHealthPercentage() > AggressiveHealthThreshold)
                    {
                        //  _currentState = BotState.Farming;
                    }
                    else if (isUnderTower && !IsAllyTower())
                    {
                        // _currentState = BotState.Retreating;
                    }

                    // Request help ping when defensive and allies are nearby
                    if (_botSettings.EnableSeriousPings && _gameTime - _lastPingTime >= _botSettings.PingCooldown)
                    {
                        var nearestAlly = GetNearestAlly(EzrealInstance, _botSettings.AllyNearbyDistance);
                        if (nearestAlly != null)
                        {
                            SendRequestHelpPing(EzrealInstance, _botSettings, EzrealInstance.Position);
                            _lastPingTime = _gameTime;
                        }
                    }
                    break;

                case BotState.Chasing:
                    // Check if we should stop chasing
                    if (GetHealthPercentage() < DefensiveHealthThreshold)
                    {
                        _currentState = BotState.Defensive;
                    }
                    else
                    {
                        // Check if target is still vulnerable
                        bool stillChasing = false;
                        foreach (var enemy in nearbyEnemies)
                        {
                            if (IsChampionVulnerable(enemy))
                            {
                                stillChasing = true;
                                break;
                            }
                        }

                        if (!stillChasing)
                        {
                            _currentState = BotState.Farming;
                        }
                    }
                    break;

                case BotState.Retreating:
                    if (GetHealthPercentage() > DefensiveHealthThreshold)
                    {
                        _currentState = BotState.Retreating;
                    }
                    break;

                case BotState.Pushing:
                    // Check if we should stop pushing
                    if (GetHealthPercentage() < DefensiveHealthThreshold)
                    {
                        _currentState = BotState.Defensive;
                    }
                    else if (nearbyEnemies.Count > 0)
                    {
                        // Enemies appeared - check if we should dive or retreat
                        _currentState = BotState.Poking;
                    }
                    else if (!ShouldPushTower())
                    {
                        // No longer good pushing conditions
                        _currentState = BotState.Farming;
                    }
                    // Dive evaluation happens in PushTower() method
                    break;

                case BotState.Diving:
                    // Dive state is managed by ExecuteDive() which transitions out when appropriate
                    break;

                case BotState.Shopping:
                    // Shopping is handled in ActOnState, transition back to farming once done
                    _currentState = BotState.Farming;
                    break;
            }
        }

        private void ActOnState()
        {
            bool isUnderTower = IsUnderEnemyTower();
            List<Champion> nearbyEnemies = GetNearbyEnemyChampions(1200f);
            switch (_currentState)
            {
                case BotState.MovingToLane:
                    // Check for enemies even when moving to lane
                    Champion enemyOnWay = GetBestQTarget();
                    if (enemyOnWay != null && IsSpellAvailable(QSlot, SpellSlotType.SpellSlots))
                    {
                        CastQ(enemyOnWay);
                    }
                    MoveToLane();
                    break;

                case BotState.Farming:
                    Farm();

                    // Check for poke opportunities while farming
                    Champion target = GetBestQTarget();
                    if (target != null && IsSpellAvailable(QSlot, SpellSlotType.SpellSlots))
                    {
                        CastQ(target);
                    }
                    break;

                case BotState.Poking:
                    Poke();
                    break;

                case BotState.Aggressive:
                    ExecuteCombo();
                    break;

                case BotState.Defensive:
                    Retreat();
                    UseDefensiveE();
                    break;

                case BotState.Chasing:
                    ChaseEnemy();
                    break;

                case BotState.Retreating:
                    Retreat();
                    break;

                case BotState.Pushing:
                    PushTower();
                    break;

                case BotState.Diving:
                    ExecuteDive();
                    break;

                case BotState.Shopping:
                    TryBuyItems();
                    // After shopping, return to farming
                    _currentState = BotState.Farming;
                    break;
            }
        }

        // Movement and positioning
        private void MoveToLane()
        {
            // Check for enemy towers first - stop if near one
            LaneTurret nearbyEnemyTower = GetNearbyEnemyTower(1000f);
            if (nearbyEnemyTower != null)
            {
                float distanceToTower = Vector2.Distance(EzrealInstance.Position, nearbyEnemyTower.Position);
                _logger.Debug($"Enemy tower detected at distance {distanceToTower:F0} while moving to lane");
                
                // If we're close enough to attack tower, switch to pushing
                if (distanceToTower <= AutoAttackRange + 200f)
                {
                    _currentState = BotState.Pushing;
                    return;
                }
                
                // Otherwise, position at safe distance from tower
                Vector2 safePosition = GetSafePositionFromTower(nearbyEnemyTower);
                MoveToPosition(safePosition);
                MovingToLane = true;
                return;
            }

            // Check if there are ally minions nearby to follow
            LaneMinion allyMinionToFollow = GetNearbyAllyMinion();
            if (allyMinionToFollow != null)
            {
                // Follow the ally minion
                _logger.Debug($"Following ally minion at {allyMinionToFollow.Position}");
                MoveToPosition(allyMinionToFollow.Position);
                MovingToLane = true;
                return;
            }

            // Use waypoint system to progress through mid lane
            Vector2 currentWaypoint = GetCurrentWaypoint();
            
            // Check if we've reached the current waypoint
            float distanceToWaypoint = Vector2.Distance(EzrealInstance.Position, currentWaypoint);
            if (distanceToWaypoint < WaypointReachedThreshold)
            {
                // Advance to next waypoint
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _midLaneWaypoints.Count)
                {
                    _currentWaypointIndex = _midLaneWaypoints.Count - 1; // Stay at last waypoint
                }
                _logger.Debug($"Reached waypoint {_currentWaypointIndex}, advancing to next");
            }
            
            // Move to current waypoint
            currentWaypoint = GetCurrentWaypoint();
            MoveToPosition(currentWaypoint);
            _logger.Debug($"Moving to waypoint {_currentWaypointIndex} at {currentWaypoint}, distance: {distanceToWaypoint:F0}");
            MovingToLane = true;
        }

        private Vector2 GetCurrentWaypoint()
        {
            if (_currentWaypointIndex >= 0 && _currentWaypointIndex < _midLaneWaypoints.Count)
            {
                return _midLaneWaypoints[_currentWaypointIndex];
            }
            // Fallback to center
            return new Vector2(7500f, 7500f);
        }

        /// <summary>
        /// Initializes waypoints dynamically based on team (blue advances forward, purple advances backward through the same points)
        /// </summary>
        private void InitializeWaypoints()
        {
            // Base waypoints from blue side perspective (moving toward enemy base)
            var baseWaypoints = new List<Vector2>
            {
                new Vector2(1418.0f, 1686.0f),   // Blue fountain area
                new Vector2(2997.0f, 2781.0f),   // Near blue outer turret
                new Vector2(4472.0f, 4727.0f),   // Mid lane center area
                new Vector2(8375.0f, 8366.0f),   // Mid lane center area
                new Vector2(10948.0f, 10821.0f), // Near purple outer turret
                new Vector2(12511.0f, 12776.0f)  // Purple fountain area (enemy nexus)
            };

            if (EzrealInstance.Team == TeamId.TEAM_BLUE)
            {
                // Blue team: use waypoints in forward order (from blue base to purple base)
                _midLaneWaypoints = baseWaypoints;
            }
            else
            {
                // Purple team: reverse the waypoints (from purple base to blue base)
                _midLaneWaypoints = new List<Vector2>(baseWaypoints);
                _midLaneWaypoints.Reverse();
            }
            _logger.Debug($"Initialized {_midLaneWaypoints.Count} waypoints for team {EzrealInstance.Team}");
        }

        private LaneMinion GetNearbyAllyMinion()
        {
            // Look for ally minions within range
            List<AttackableUnit> units = GetUnitsInRange(EzrealInstance.Position, 800f, true);
            LaneMinion closestMinion = null;
            float closestDistance = float.MaxValue;
            
            foreach (var unit in units)
            {
                if (unit is LaneMinion minion && minion.Team == EzrealInstance.Team)
                {
                    float dist = Vector2.Distance(EzrealInstance.Position, minion.Position);
                    // Prefer minions that are ahead of us (closer to enemy base)
                    Vector2 enemyBaseDir = GetEnemyBaseDirection();
                    Vector2 toMinion = Vector2.Normalize(minion.Position - EzrealInstance.Position);
                    float dot = Vector2.Dot(enemyBaseDir, toMinion);
                    
                    // If minion is roughly ahead of us or very close
                    if ((dot > 0.3f || dist < 300f) && dist < closestDistance)
                    {
                        closestMinion = minion;
                        closestDistance = dist;
                    }
                }
            }
            
            return closestMinion;
        }

        private LaneTurret GetNearbyEnemyTower(float range)
        {
            // Look for enemy towers within range
            List<AttackableUnit> units = GetUnitsInRange(EzrealInstance.Position, range, true);
            
            foreach (var unit in units)
            {
                if (unit is LaneTurret turret && turret.Team != EzrealInstance.Team)
                {
                    return turret;
                }
            }
            
            return null;
        }

        private Vector2 GetSafePositionFromTower(LaneTurret tower)
        {
            // Position at auto attack range from tower
            Vector2 directionToTower = Vector2.Normalize(tower.Position - EzrealInstance.Position);
            float distance = AutoAttackRange + 50f;
            return tower.Position - (directionToTower * distance);
        }

        private Vector2 GetEnemyBaseDirection()
        {
            // Return normalized direction toward enemy base
            if (EzrealInstance.Team == TeamId.TEAM_BLUE)
            {
                return Vector2.Normalize(new Vector2(12511f, 12776f) - EzrealInstance.Position);
            }
            else
            {
                return Vector2.Normalize(new Vector2(1418f, 1686f) - EzrealInstance.Position);
            }
        }

        private void Farm()
        {
            // Reset last chase target when back to farming
            _lastChaseTarget = null;

            Vector2 botPosition = EzrealInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, 800f, true);

            if (ShouldPushTower())
            {
                _logger.Debug("Should push tower - pushing");
                _currentState = BotState.Pushing;
                PushTower();
                return;
            }

            // Check if we're taking minion damage and should back off
            if (IsTakingMinionDamage())
            {
                _logger.Debug("Taking minion damage - backing off");
                _currentState = BotState.Defensive;
                Retreat();
                return;
            }

            Champion enemyTarget = GetBestQTarget();
            if (enemyTarget != null)
            {
                float enemyHealthPct = enemyTarget.Stats.CurrentHealth / enemyTarget.Stats.HealthPoints.Total;
                _logger.Debug($"Found enemy {enemyTarget.Name} with {enemyHealthPct:P0} health during farming");

                // Only consider actions if our health is reasonable AND we won't take heavy minion damage
                if (GetHealthPercentage() > 0.3f && !WillTakeMinionAggro(enemyTarget))
                {
                    // If enemy is vulnerable (low health or isolated), switch to chase mode
                    if (IsSafeToEngageChampion(enemyTarget))
                    {
                        _logger.Debug($"Found vulnerable enemy during farming - switching to chase mode");
                        _currentState = BotState.Chasing;
                        ChaseEnemy();
                        return;
                    }
                    // Otherwise switch to poke mode only if safe from minions
                    else if (IsSafeFromMinions())
                    {
                        _logger.Debug("Found enemy during farming - switching to poke");
                        _currentState = BotState.Poking;
                        Poke();
                        return;
                    }
                }
            }

            // Rest of your existing farming logic...
            var lowHealthMinions = units.OfType<LaneMinion>()
                .Where(minion => minion.Team != EzrealInstance.Team &&
                       minion.Stats.CurrentHealth < EzrealInstance.Stats.AttackDamage.Total * 1.5f)
                .OrderBy(minion => minion.Stats.CurrentHealth)
                .ToList();

            if (lowHealthMinions.Any())
            {
                Minion targetMinion = lowHealthMinions.First();
                // Use orbwalking for minion attacks
                if (IsInAutoAttackRange(targetMinion))
                {
                    PerformOrbwalk(GetIdealFarmingPosition(), targetMinion);
                }
                else
                {
                    MoveToPosition(GetIdealFarmingPosition());
                }
            }
            else
            {
                var qKillableMinions = GetUnitsInRange(botPosition, QRange, true).OfType<LaneMinion>()
                    .Where(minion => minion.Team != EzrealInstance.Team &&
                           minion.Stats.CurrentHealth < CalculateQDamage(minion) &&
                           !IsInAutoAttackRange(minion))
                    .OrderBy(minion => minion.Stats.CurrentHealth)
                    .ToList();

                if (qKillableMinions.Any() && IsSpellAvailable(QSlot, SpellSlotType.SpellSlots))
                {
                    CastQ(qKillableMinions.First());
                }
                else
                {
                    // No minions to farm - continue pushing forward using waypoints
                    AdvanceWaypointIfNeeded();
                    MoveToPosition(GetIdealFarmingPosition());
                }
            }
        }

        private void AdvanceWaypointIfNeeded()
        {
            // First check for enemy towers - don't advance past them
            LaneTurret enemyTower = GetNearbyEnemyTower(1000f);
            if (enemyTower != null && !ShouldPushTower())
            {
                float distanceToTower = Vector2.Distance(EzrealInstance.Position, enemyTower.Position);
                _logger.Debug($"Not advancing waypoint - enemy tower nearby at distance {distanceToTower:F0}");
                return;
            }

            // Check if we've reached current waypoint and should advance
            Vector2 currentWaypoint = GetCurrentWaypoint();
            float distanceToWaypoint = Vector2.Distance(EzrealInstance.Position, currentWaypoint);
            
            if (distanceToWaypoint < WaypointReachedThreshold)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _midLaneWaypoints.Count)
                {
                    _currentWaypointIndex = _midLaneWaypoints.Count - 1;
                }
                _logger.Debug($"Advanced to waypoint {_currentWaypointIndex} while farming");
            }
        }

        private bool IsTakingMinionDamage()
        {
            // Check if we're in combat and the damage might be from minions
            if (!isInCombat) return false;

            // Get nearby enemy minions
            var nearbyEnemyMinions = GetUnitsInRange(EzrealInstance.Position, 500f, true)
                .OfType<LaneMinion>()
                .Where(minion => minion.Team != EzrealInstance.Team)
                .ToList();

            // If we have multiple minions nearby and we're in combat, likely taking minion damage
            if (nearbyEnemyMinions.Count >= 3 && isInCombat)
            {
                return true;
            }

            // Check if any minions are actively targeting us
            var minionsTargetingUs = nearbyEnemyMinions
                .Where(minion => minion.TargetUnit == EzrealInstance)
                .ToList();

            return minionsTargetingUs.Count > 1; // More than 1 minion targeting us is dangerous
        }

        private bool WillTakeMinionAggro(Champion target)
        {
            // Check if attacking this champion will draw minion aggro
            var nearbyEnemyMinions = GetUnitsInRange(target.Position, 500f, true)
                .OfType<LaneMinion>()
                .Where(minion => minion.Team != EzrealInstance.Team)
                .ToList();

            // If there are many minions near the target, attacking will draw aggro
            if (nearbyEnemyMinions.Count >= 4) return true;

            // If we're already low health, avoid any minion aggro
            if (GetHealthPercentage() < 0.5f && nearbyEnemyMinions.Count >= 2) return true;

            return false;
        }

        private bool IsSafeFromMinions()
        {
            var nearbyEnemyMinions = GetUnitsInRange(EzrealInstance.Position, 400f, true)
                .OfType<Minion>()
                .Where(minion => minion.Team != EzrealInstance.Team)
                .ToList();

            // Safe if few minions nearby
            return nearbyEnemyMinions.Count <= 2;
        }

        private bool IsSpellAvailable(byte slot, SpellSlotType slotType)
        {
            byte key = slot;
            if (slotType == SpellSlotType.SummonerSpellSlots)
            {
                key = (byte)(slot + 100); // Example conversion
            }

            try
            {
                Spell spell = null;
                if (slotType == SpellSlotType.SpellSlots)
                {
                    spell = EzrealInstance.Spells[(short)slot];
                }
                else if (slotType == SpellSlotType.SummonerSpellSlots)
                {
                    int convertedSlot = ConvertAPISlot(slotType, slot);
                    spell = EzrealInstance.Spells[(short)convertedSlot];
                }

                if (spell == null || spell.CastInfo.SpellLevel == 0)
                {
                    return false;
                }

                if (_lastCastTime.TryGetValue(key, out float lastCastTime))
                {
                    // Match GetCooldown's indexing (SpellLevel-1)
                    float cooldown = spell.SpellData.Cooldown[spell.CastInfo.SpellLevel - 1];
                    if (_gameTime < lastCastTime + cooldown)
                    {
                        return false;
                    }
                }

                // Check mana
                float manaCost = spell.SpellData.ManaCost[spell.CastInfo.SpellLevel - 1];
                if (EzrealInstance.Stats.CurrentMana < manaCost)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error checking spell availability: {ex.Message}");
                return false;
            }
        }


        private int GetSpellLevel(byte slot, SpellSlotType slotType)
        {
            try
            {
                Spell spell = null;
                if (slotType == SpellSlotType.SpellSlots)
                {
                    spell = EzrealInstance.Spells[(short)slot];
                }
                else if (slotType == SpellSlotType.SummonerSpellSlots)
                {
                    int convertedSlot = ConvertAPISlot(slotType, slot);
                    spell = EzrealInstance.Spells[(short)convertedSlot];
                }

                if (spell == null)
                    return 0;

                return spell.CastInfo.SpellLevel;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        private void Poke()
        {
            // First, check if we're in a dangerous position
            if (IsUnderEnemyTower())
            {
                _logger.Debug("In dangerous position under enemy tower - retreating");
                ClearTargetAndOrders();
                Retreat();
                return;
            }

            // Use Q to poke enemy champions if cooldown has passed
            if (DateTime.Now - _lastPokeTime >= _pokeCooldown)
            {
                Champion target = GetBestQTarget();
                if (target != null && IsSpellAvailable(QSlot, SpellSlotType.SpellSlots))
                {
                    _currentState = BotState.Poking;
                    CastQ(target);
                    _lastPokeTime = DateTime.Now; // Set cooldown after successful poke
                    _logger.Debug($"Successfully poked {target.Name} with Q");

                    // After poking, briefly back off to maintain safe distance
                    Retreat();
                    return;
                }
            }

            // If we can't poke with abilities, try to position better
            MoveToPosition(GetIdealPokingPosition());

            // Only auto-attack with orbwalking if we're already in a good position and it's safe
            Champion nearbyTarget = GetClosestEnemyChampion();
            if (nearbyTarget != null && IsInAutoAttackRange(nearbyTarget) && IsSafeToEngageChampion(nearbyTarget))
            {
                _logger.Debug($"Auto attacking {nearbyTarget.Name} during poke with orbwalking");
                _currentState = BotState.Poking;
                PerformOrbwalk(GetIdealPokingPosition(), nearbyTarget);
            }
        }

        private void ExecuteCombo()
        {
            Champion target = GetBestComboTarget();

            if (target == null)
            {
                _currentState = BotState.Aggressive;
                return;
            }

            // Full combo logic
            // W -> E -> Q -> Auto -> R if killable

            // Cast W first if in range
            if (IsSpellAvailable(WSlot, SpellSlotType.SpellSlots) && IsInSpellRange(target, WRange))
            {
                CastW(target);
            }


            // Use E aggressively if safe
            if (IsSpellAvailable(ESlot, SpellSlotType.SpellSlots) && IsSafeToUseAggressiveE(target))
            {
                CastAggressiveE(target);
            }

            // Cast Q
            if (IsSpellAvailable(QSlot, SpellSlotType.SpellSlots) && IsInSpellRange(target, QRange))
            {
                CastQ(target);
            }

            // Auto attack with orbwalking if in range
            if (IsInAutoAttackRange(target))
            {
                PerformOrbwalk(target.Position, target);
            }

            // Cast R if can kill
            if (IsSpellAvailable(RSlot, SpellSlotType.SpellSlots) && CanKillWithR(target))
            {
                CastR(target);
            }
        }

        private void Retreat()
        {
            // Move toward closest ally or tower
            ClearTargetAndOrders();
            Vector2 safePosition = GetSafePosition();
            MoveToPosition(safePosition);
            stillChasing = false;

            if (IsSpellAvailable(ESlot, SpellSlotType.SpellSlots))
            {
                UseDefensiveE();
            }
        }

        // Item shopping methods
        private void TryBuyItems()
        {
            if (!_hasItemsToBuy || _currentBuildIndex >= _itemBuildOrder.Count)
            {
                _hasItemsToBuy = false;
                return;
            }

            int targetItemId = _itemBuildOrder[_currentBuildIndex];
            var itemTemplate = ApiFunctionManager.GetItemData(targetItemId);
            
            if (itemTemplate == null)
            {
                _logger.Debug($"Item {targetItemId} not found in ItemManager");
                _currentBuildIndex++;
                return;
            }

            // Check if we can afford the item
            var price = itemTemplate.TotalPrice;
            var ownedItems = EzrealInstance.Inventory.GetAvailableItems(itemTemplate.Recipe.GetItems());
            if (ownedItems.Count != 0)
            {
                price -= ownedItems.Sum(item => item.ItemData.TotalPrice);
            }

            // Buy if we have enough gold
            if (EzrealInstance.Stats.Gold >= price)
            {
                bool success = EzrealInstance.Shop.HandleItemBuyRequest(targetItemId);
                if (success)
                {
                    _logger.Debug($"Successfully bought item {itemTemplate.Name} (ID: {targetItemId})");
                    _currentBuildIndex++;
                    
                    // Continue buying if there are more items and we have gold
                    if (_currentBuildIndex < _itemBuildOrder.Count)
                    {
                        TryBuyItems();
                    }
                    else
                    {
                        _hasItemsToBuy = false;
                        _logger.Debug("Completed item build order");
                    }
                }
                else
                {
                    _logger.Debug($"Failed to buy item {itemTemplate.Name} - shop request failed");
                }
            }
            else
            {
                _logger.Debug($"Not enough gold for {itemTemplate.Name}. Need {price}, have {EzrealInstance.Stats.Gold}");
                // Can't afford this item, stop shopping and go farm
                _hasItemsToBuy = false;
            }
        }

        private bool CanAffordNextItem()
        {
            if (_currentBuildIndex >= _itemBuildOrder.Count)
                return false;

            int targetItemId = _itemBuildOrder[_currentBuildIndex];
            var itemTemplate = ApiFunctionManager.GetItemData(targetItemId);
            
            if (itemTemplate == null)
                return false;

            // Calculate price with owned components
            var price = itemTemplate.TotalPrice;
            var ownedItems = EzrealInstance.Inventory.GetAvailableItems(itemTemplate.Recipe.GetItems());
            if (ownedItems.Count != 0)
            {
                price -= ownedItems.Sum(item => item.ItemData.TotalPrice);
            }

            return EzrealInstance.Stats.Gold >= price;
        }

        // Spell casting methods
        private void CastQ(ObjAIBase target)
        {
            if (target == null) return;
            _logger.Debug("Casting Q at target");

            Vector2 predictedPos = PredictPosition(target, QRange);

            // Create a target list with the enemy
            List<CastTarget> targets = new List<CastTarget> { new CastTarget(target, HitResult.HIT_Normal) };

            // Cast spell
            SpellCast(
                EzrealInstance,
                QSlot,
                SpellSlotType.SpellSlots,
                predictedPos,
                Vector2.Zero,
                false,
                Vector2.Zero,
                targets
            );

            MarkSpellAsUsed(QSlot, SpellSlotType.SpellSlots);
        }

        private void CastW(ObjAIBase target)
        {
            if (target == null) return;
            _logger.Debug("Casting W at target");

            Vector2 predictedPos = PredictPosition(target, WRange);

            // Create a target list with the enemy
            List<CastTarget> targets = new List<CastTarget> { new CastTarget(target, HitResult.HIT_Normal) };

            // Cast spell
            SpellCast(
                EzrealInstance,
                WSlot,
                SpellSlotType.SpellSlots,
                predictedPos,
                Vector2.Zero,
                false,
                Vector2.Zero,
                targets
            );

            MarkSpellAsUsed(WSlot, SpellSlotType.SpellSlots);
        }

        private void CastAggressiveE(ObjAIBase target)
        {
            if (target == null) return;
            _logger.Debug("Casting aggressive E");
            Vector2 targetPos = target.Position;
            Vector2 myPos = EzrealInstance.Position;
            // Don't E directly on top of them - stay at a safe distance
            Vector2 direction = Vector2.Normalize(targetPos - myPos);
            float distance = Math.Min(ERange, Vector2.Distance(myPos, targetPos) - 200);
            Vector2 ePos = myPos + direction * distance;

            // Create a proper target for the spell system to use
            List<CastTarget> targets = new List<CastTarget> { new CastTarget(target, HitResult.HIT_Normal) };

            // Ensure ESlot is within the valid range - should be 2 for Ezreal's E (0=Q, 1=W, 2=E, 3=R)
            // If ESlot is defined as a SpellSlot enum, you don't need this check and conversion
            int rawSlot = ESlot;
            if (rawSlot < 0 || rawSlot > 3)
            {
                _logger.Error($"Invalid ESlot value: {rawSlot}. Spell slot must be between 0-3.");
                return;
            }

            // Cast spell with carefully set parameters to avoid index errors
            SpellCast(
                EzrealInstance,
                rawSlot, // Make sure this is a proper spell slot index (0-3 for QWER)
                SpellSlotType.SpellSlots,
                ePos, // Position to cast to
                targetPos, // End position (direction)
                false, // Don't fire without casting
                Vector2.Zero, // No override cast position
                targets, // Target list with the enemy
                false, // Not force casting
                -1, // Default force level
                false, // Don't update auto attack timer for abilities
                false // Not an auto attack spell
            );
            MarkSpellAsUsed(ESlot, SpellSlotType.SpellSlots);
        }

        private void UseDefensiveE()
        {
            if (!IsSpellAvailable(ESlot, SpellSlotType.SpellSlots))
                return;

            Champion closestEnemy = GetClosestEnemyChampion();
            if (closestEnemy != null && Vector2.Distance(EzrealInstance.Position, closestEnemy.Position) < SafeDistance)
            {
                _logger.Debug("Casting defensive E");

                Vector2 direction = Vector2.Normalize(EzrealInstance.Position - closestEnemy.Position);
                Vector2 ePos = EzrealInstance.Position + direction * ERange;

                // Engine requires at least one target in the list � pass self as a dummy
                List<CastTarget> targets = new List<CastTarget>
        {
            new CastTarget(EzrealInstance, HitResult.HIT_Normal)
        };

                SpellCast(
                    EzrealInstance,
                    ESlot,
                    SpellSlotType.SpellSlots,
                    ePos,
                    Vector2.Zero,
                    false,
                    Vector2.Zero,
                    targets
                );

                MarkSpellAsUsed(ESlot, SpellSlotType.SpellSlots);
            }
        }

        private void CastR(ObjAIBase target)
        {
            if (target == null) return;
            _logger.Debug("Casting R at target");

            Vector2 predictedPos = PredictPosition(target, RRange);

            // Create a target list with the enemy
            List<CastTarget> targets = new List<CastTarget> { new CastTarget(target, HitResult.HIT_Normal) };

            // Cast spell
            SpellCast(
                EzrealInstance,
                RSlot,
                SpellSlotType.SpellSlots,
                predictedPos,
                Vector2.Zero,
                false,
                Vector2.Zero,
                targets
            );

            MarkSpellAsUsed(RSlot, SpellSlotType.SpellSlots);
        }

        private void MarkSpellAsUsed(byte slot, SpellSlotType slotType)
        {
            byte key = slot;
            if (slotType == SpellSlotType.SummonerSpellSlots)
            {
                // Adjust key based on summoner spell conversion if needed
                key = (byte)(slot + 100); // Example conversion
            }
            _lastCastTime[key] = _gameTime;
        }

        // Helper methods
        private float GetHealthPercentage()
        {
            return EzrealInstance.Stats.CurrentHealth / EzrealInstance.Stats.HealthPoints.Total;
        }

        private float GetManaPercentage()
        {
            return EzrealInstance.Stats.CurrentMana / EzrealInstance.Stats.ManaPoints.Total;
        }

        private bool CanPoke()
        {
            return GetManaPercentage() > 0.3f && IsSpellAvailable(QSlot, SpellSlotType.SpellSlots);
        }

        private bool CanGoAggressive(List<Champion> nearbyEnemies)
        {
            // Criteria for aggressive play:
            // 1. Health above threshold
            // 2. Have enough mana for combo
            // 3. Enemy is in a vulnerable position

            if (GetHealthPercentage() < AggressiveHealthThreshold || GetManaPercentage() < MinManaForCombo)
                return false;

            foreach (var enemy in nearbyEnemies)
            {
                if (IsVulnerable(enemy))
                    return true;
            }

            return false;
        }

        private bool IsVulnerable(Champion enemy)
        {
            // Check if enemy has low health or is isolated
            return enemy.Stats.CurrentHealth / enemy.Stats.HealthPoints.Total < 0.5f ||
                   GetNearbyAlliesOfTarget(enemy, 800f) == 0;
        }

        // Helper method to determine if a champion is vulnerable
        private bool IsChampionVulnerable(Champion champion)
        {
            if (champion == null)
                return false;

            float healthPct = champion.Stats.CurrentHealth / champion.Stats.HealthPoints.Total;
            int nearbyAllies = GetNearbyAlliesOfTarget(champion, 800f);

            // Champion is vulnerable if low health or isolated with moderate health
            return (healthPct < 0.3f) || (healthPct < 0.5f && nearbyAllies == 0);
        }

        // Add this new method to find vulnerable enemies
        private Champion FindVulnerableEnemy()
        {
            // Look for enemies in extended range (longer than Q range to allow chasing)
            List<Champion> enemies = GetNearbyEnemyChampions(1500f);

            if (enemies == null || enemies.Count == 0)
                return null;

            _logger.Debug($"Found {enemies.Count} potential chase targets");

            Champion bestTarget = null;
            float bestScore = 0f;

            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;

                // Base stats
                float enemyHealthPct = enemy.Stats.CurrentHealth / enemy.Stats.HealthPoints.Total;
                int nearbyAlliesOfTarget = GetNearbyAlliesOfTarget(enemy, 800f);
                int nearbyEnemiesOfTarget = GetNearbyEnemiesOfTarget(enemy, 800f); // So basically allies of Ezreal in relation to the target.
                float distance = Vector2.Distance(EzrealInstance.Position, enemy.Position);
                int levelDiff = EzrealInstance.Stats.Level - enemy.Stats.Level;

                // Calculate a "vulnerability score" for this enemy
                float score = 0f;

                // Low health is good - add critical tier
                if (enemyHealthPct <= 0.15f)          // NEW: Critical threshold
                    score += 4.5f;                    // Highest priority - execution territory
                else if (enemyHealthPct < 0.3f)
                    score += 3.0f;                    // Still very good
                else if (enemyHealthPct < 0.5f)
                    score += 1.5f;                    // Moderate

                // Gang up bonus: many allies vs isolated enemy = great target
                if (nearbyEnemiesOfTarget >= 2 && nearbyAlliesOfTarget == 0)
                    score += 2.0f; // Free kill, jump on it!
                else if (nearbyEnemiesOfTarget >= 1 && nearbyAlliesOfTarget == 0)
                    score += 1.0f; // 2v1 advantage

                // Avoid grouped enemies
                if (nearbyAlliesOfTarget >= 2 && nearbyEnemiesOfTarget == 0)
                    score -= 1.0f; // You'll get collapsed on

                // Closer targets are better (but not too much weight on this)
                score += (1500f - distance) / 1500f;

                // >>> GRANULAR LEVEL ADVANTAGE <<<
                // Each level difference adds/subtracts 0.6, capped between -2 and +3
                float levelScore = levelDiff * 0.6f;
                score += Math.Min(Math.Max(levelScore, -2f), 3f);

                // Debug output
                _logger.Debug($"Enemy {enemy.Name}: health={enemyHealthPct:F2}, EnemiesOfChoosenTarget={nearbyEnemiesOfTarget}, " +
                             $"distance={distance:F0}, levelDiff={levelDiff}, score={score:F2}");

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            // Only chase if score is high enough
            if (bestScore > 1.5f)
            {
                _logger.Debug($"Selected {bestTarget.Name} as chase target with score {bestScore:F2}");
                return bestTarget;
            }

            return null;
        }

        // Add a new method for chasing behavior
        private void ChaseEnemy()
        {
            // Find vulnerable target
            Champion target = FindVulnerableEnemy();
            // Track the last target we chased
            _lastChaseTarget = target;
            // Move toward target but keep some distance based on available spells
            Vector2 idealChasePosition;

            if (target == null)
            {
                // No valid target to chase anymore, go back to farming
                _logger.Debug("No valid chase target found, returning to farming");
                ClearTargetAndOrders();
                _currentState = BotState.Farming;
                return;
            }

            if (!IsSafeToEngageChampion(target))
            {
                _logger.Debug($"Not safe to chase {target.Name}, reverting to poke mode");
                ClearTargetAndOrders();
                _currentState = BotState.Poking;
                Poke();
                return;
            }

            _logger.Debug($"Chasing {target.Name} at {target.Position}, health: {target.Stats.CurrentHealth / target.Stats.HealthPoints.Total:P0}");

            // Distance to target
            float distance = Vector2.Distance(EzrealInstance.Position, target.Position);
            _logger.Debug($"Distance to target: {distance:F0}");

            if (IsInAutoAttackRange(target))
            {
                _logger.Debug($"Auto attacking {target.Name} during chase with orbwalking");
                PerformOrbwalk(GetPositionAtDistanceFromTarget(target, AutoAttackRange - 50), target);
            }

            if (!IsInAutoAttackRange(target))
            {
                if (IsSpellAvailable(QSlot, SpellSlotType.SpellSlots))
                {
                    // If Q is up, stay at Q range
                    idealChasePosition = GetPositionAtDistanceFromTarget(target, QRange - 50);
                    _logger.Debug($"Positioning at Q range ({QRange - 50})");
                }
                else
                {
                    // Otherwise move closer for auto attacks
                    idealChasePosition = GetPositionAtDistanceFromTarget(target, AutoAttackRange - 50);
                    _logger.Debug("Moving to auto attack range");
                }

                _logger.Debug($"Moving to position {idealChasePosition}");
                MoveToPosition(idealChasePosition);
            }

            // If in Q range, cast Q
            if (distance <= QRange && IsSpellAvailable(QSlot, SpellSlotType.SpellSlots))
            {
                _logger.Debug($"Casting Q on {target.Name}");
                CastQ(target);
            }

            // If in W range, cast W
            if (distance <= WRange && IsSpellAvailable(WSlot, SpellSlotType.SpellSlots))
            {
                _logger.Debug($"Casting W on {target.Name}");
                CastW(target);
            }

            // If target is very low and in ultimate range, cast R
            float targetHealthPct = target.Stats.CurrentHealth / target.Stats.HealthPoints.Total;
            if (targetHealthPct < 0.2f && distance <= RRange && IsSpellAvailable(RSlot, SpellSlotType.SpellSlots))
            {
                _logger.Debug($"Casting R on low health {target.Name}");
                CastR(target);
            }

            // Check if we should use E aggressively to close gap
            if (distance > 400 && distance < 1200 && IsSpellAvailable(ESlot, SpellSlotType.SpellSlots))
            {
                // Only use E aggressively if safe
                if (IsSafeToUseAggressiveE(target))
                {
                    _logger.Debug($"Using E aggressively to chase {target.Name}");
                    CastAggressiveE(target);
                }
            }


            if (IsSpellAvailable(QSlot, SpellSlotType.SpellSlots))
            {
                // If Q is up, stay at Q range
                idealChasePosition = GetPositionAtDistanceFromTarget(target, QRange - 50);
                _logger.Debug($"Positioning at Q range ({QRange - 50})");
            }
            else if (IsInAutoAttackRange(target))
            {
                // If in auto range, maintain auto range
                idealChasePosition = GetPositionAtDistanceFromTarget(target, 550);
                _logger.Debug("Positioning at auto attack range");
            }


            // If target moved too far or is no longer vulnerable, stop chasing
            if (distance > 2000 || target.Stats.CurrentHealth / target.Stats.HealthPoints.Total > 0.5f)
            {
                _logger.Debug("Target too far or no longer vulnerable, stopping chase");
                _currentState = BotState.Poking;
            }
        }

        private bool IsSafeToEngageChampion(Champion target)
        {
            if (target == null)
                return false;

            // Don't engage if our health is too low
            if (GetHealthPercentage() < 0.4f)
                return false;

            // Check if we're under enemy tower
            if (IsUnderEnemyTower())
            {
                _logger.Debug("Not safe to engage - under enemy tower");
                return false;
            }

            // Calculate minion advantage/disadvantage
            int allyMinions = CountNearbyMinions(EzrealInstance.Position, 600f, EzrealInstance.Team);
            int enemyMinions = CountNearbyMinions(target.Position, 600f, target.Team);

            _logger.Debug($"Minion count - Allies: {allyMinions}, Enemies: {enemyMinions}");

            // Don't engage if heavily outnumbered by minions (3+ difference)
            if (enemyMinions > allyMinions + 3)
            {
                _logger.Debug("Not safe to engage - minion disadvantage");
                return false;
            }

            // Check if target is near their tower
            if (IsPositionUnderEnemyTower(target.Position, 775f))
            {
                _logger.Debug("Not safe to engage - target near their tower");
                return false;
            }

            // Check if there are multiple enemy champions nearby
            List<Champion> nearbyEnemies = GetNearbyEnemyChampions(800f);
            if (nearbyEnemies.Count > 1)
            {
                _logger.Debug($"Not safe to engage - {nearbyEnemies.Count} enemies nearby");
                return false;
            }

            return true;
        }

        // Add this method to check if we should chase an enemy
        private bool ShouldChaseEnemy()
        {
            // Only chase if health is high enough
            if (GetHealthPercentage() < 0.6f)
                return false;

            // Find a vulnerable enemy to chase
            Champion target = FindVulnerableEnemy();
            return target != null;
        }

        // Helper method for chase positioning
        private Vector2 GetPositionAtDistanceFromTarget(Champion target, float desiredDistance)
        {
            Vector2 directionFromTarget = Vector2.Normalize(EzrealInstance.Position - target.Position);
            return target.Position + (directionFromTarget * desiredDistance);
        }


        private bool IsSafeToUseAggressiveE(Champion target)
        {
            // Check if it's safe to E aggressively:
            // 1. Won't E under enemy tower
            // 2. Won't be outnumbered after E
            // 3. Have enough health

            Vector2 targetPos = target.Position;
            Vector2 myPos = EzrealInstance.Position;

            // Calculate potential E position
            Vector2 direction = Vector2.Normalize(targetPos - myPos);
            Vector2 ePos = myPos + direction * Math.Min(ERange, Vector2.Distance(myPos, targetPos) - 200);

            // Check if E would put us under tower
            if (IsPositionUnderEnemyTower(ePos))
                return false;

            // Check health
            if (GetHealthPercentage() < AggressiveHealthThreshold)
                return false;

            return true;
        }

        private float CalculateQDamage(ObjAIBase target)
        {
            // Basic damage calculation for Q
            int level = GetSpellLevel(QSlot, SpellSlotType.SpellSlots);
            float baseDamage = 20 + 25 * level; // Example values
            float apRatio = 0.4f; // Example AP ratio
            float adRatio = 1.3f; // Example AD ratio (120% base AD + 10% bonus AD)

            float damage = baseDamage +
                          (EzrealInstance.Stats.AbilityPower.Total * apRatio) +
                          (EzrealInstance.Stats.AttackDamage.Total * adRatio);

            // Apply physical damage reduction
            float armor = target.Stats.Armor.Total;
            float damageMultiplier = 100 / (100 + armor);

            return damage * damageMultiplier;
        }

        private bool CanKillWithR(Champion target)
        {
            // Calculate if R will kill the target
            float rDamage = CalculateRDamage(target);
            return target.Stats.CurrentHealth <= rDamage;
        }

        private float CalculateRDamage(ObjAIBase target)
        {
            // Basic damage calculation for R
            int level = GetSpellLevel(RSlot, SpellSlotType.SpellSlots);
            float baseDamage = 350 + 150 * level; // Example values delete 2 zeros here and the R script damage
            float apRatio = 0.9f; // Example AP ratio
            float adRatio = 1.0f; // Example AD ratio

            float damage = baseDamage +
                          (EzrealInstance.Stats.AbilityPower.Total * apRatio) +
                          (EzrealInstance.Stats.AttackDamage.Total * adRatio);

            // Apply magic resistance reduction
            float magicResist = target.Stats.MagicResist.Total;
            float damageMultiplier = 100 / (100 + magicResist);

            return damage * damageMultiplier;
        }

        private void LevelUpSpells()
        {
            int level = EzrealInstance.Stats.Level;

            // Count current skill points allocated
            int allocatedPoints = 0;
            for (byte i = 0; i <= 3; i++)
            {
                allocatedPoints += GetSpellLevel(i, SpellSlotType.SpellSlots);
            }

            // If we already leveled up for this level, return
            if (level <= allocatedPoints)
                return;

            // Level priority: R > Q > E > W
            if (level == 6 || level == 11 || level == 16)
            {
                LevelUpSpell(RSlot, SpellSlotType.SpellSlots);
            }
            else if (level % 2 == 1) // Odd levels prioritize Q
            {
                LevelUpSpell(QSlot, SpellSlotType.SpellSlots);
            }
            else // Even levels alternate between E and W, favoring E
            {
                if (GetSpellLevel(ESlot, SpellSlotType.SpellSlots) < GetSpellLevel(WSlot, SpellSlotType.SpellSlots))
                    LevelUpSpell(ESlot, SpellSlotType.SpellSlots);
                else
                    LevelUpSpell(WSlot, SpellSlotType.SpellSlots);
            }
        }

        private void LevelUpSpell(byte slot, SpellSlotType slotType)
        {
            try
            {
                if (slotType == SpellSlotType.SpellSlots)
                {
                    EzrealInstance.LevelUpSpell(slot);
                }
                else if (slotType == SpellSlotType.SummonerSpellSlots)
                {
                    // Handle summoner spell level up if needed
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error leveling up spell: {ex.Message}");
            }
        }

        // Target acquisition methods
        private Champion GetBestQTarget()
        {
            List<Champion> enemies = GetNearbyEnemyChampions(QRange);
            // Find the most optimal target for Q
            Champion bestTarget = null;
            float lowestHealth = float.MaxValue;
            foreach (var enemy in enemies)
            {
                // Skip if not in line of sight or behind minions
                if (!HasLineOfSight(enemy) || IsTargetBehindMinions(enemy))
                    continue;

                // Get the enemy's current health using the Stats.CurrentHealth property
                float enemyCurrentHealth = enemy.Stats.CurrentHealth;

                // Prioritize low health targets
                if (enemyCurrentHealth < lowestHealth)
                {
                    lowestHealth = enemyCurrentHealth;
                    bestTarget = enemy;
                }
            }
            return bestTarget;
        }

        private Champion GetBestComboTarget()
        {
            List<Champion> enemies = GetNearbyEnemyChampions(QRange);

            // Find the most optimal target for full combo
            Champion bestTarget = null;
            float bestScore = 0;

            foreach (var enemy in enemies)
            {
                float score = EvaluateTargetForCombo(enemy);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            return bestTarget;
        }

        private float EvaluateTargetForCombo(Champion target)
        {
            // Higher score = better target
            float score = 0;

            // Low health targets are preferred
            score += (1 - (target.Stats.CurrentHealth / target.Stats.HealthPoints.Total)) * 50;

            // Closer targets are easier to hit
            float distance = Vector2.Distance(EzrealInstance.Position, target.Position);
            score += (1 - (distance / QRange)) * 30;

            // Targets with no allies nearby are better
            int nearbyAllies = GetNearbyAlliesOfTarget(target, 800f);
            score += (5 - Math.Min(nearbyAllies, 5)) * 10;

            // Targets without escape abilities are better (would need specific logic)

            return score;
        }

        // Utility methods you'll need to implement based on your game engine
        private List<Champion> GetNearbyEnemyChampions(float range)
        {
            Vector2 botPosition = EzrealInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, EnemyDetectionRange, true);
            var nearbyEnemyChampions = units.OfType<Champion>()
                .Where(champion => champion.Team != EzrealInstance.Team && !champion.IsDead)
                .OrderBy(champion => Vector2.Distance(botPosition, champion.Position))
                .ToList();

            return nearbyEnemyChampions;
        }

        private int GetNearbyAlliesOfTarget(Champion target, float range)
        {
            Vector2 enemyPosition = target.Position;

            List<AttackableUnit> units = GetUnitsInRange(enemyPosition, range, true);

            var enemyTeammatesNearTarget = units.OfType<Champion>()
                .Where(champion => champion.Team != EzrealInstance.Team && !champion.IsDead && champion != target)
                .ToList();

            return enemyTeammatesNearTarget.Count;
        }

        private int GetNearbyEnemiesOfTarget(Champion target, float range)
        {
            // Get allies near the TARGET (not near the bot), so we know how many friends are near the enemy
            Vector2 enemyPosition = target.Position;

            List<AttackableUnit> units = GetUnitsInRange(enemyPosition, range, true); // Use enemyPosition, not botPosition

            var alliesNearTarget = units.OfType<Champion>()
                .Where(champion => champion.Team == EzrealInstance.Team && !champion.IsDead && champion != EzrealInstance)
                .ToList();

            return alliesNearTarget.Count; // Return the actual count
        }

        private List<Champion> GetNearbyAllyChampions(float range)
        {
            // Return list of ally champions within range
            // Implementation depends on your game engine
            return new List<Champion>();
        }

        private Champion GetClosestAllyChampion()
        {
            Vector2 botPosition = EzrealInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, AllyDetectionRange, true);
            var nearbyAlliedChampions = units.OfType<Champion>()
                .Where(champion => champion.Team == EzrealInstance.Team && !champion.IsDead && champion != EzrealInstance)
                .ToList();

            if (nearbyAlliedChampions.Any())
            {
                return nearbyAlliedChampions
                    .OrderBy(champion => Vector2.Distance(botPosition, champion.Position))
                    .FirstOrDefault();
            }
            return null;
        }

        private Champion GetClosestEnemyChampion()
        {
            Vector2 botPosition = EzrealInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, EnemyDetectionRange, true);
            var nearbyEnemyChampions = units.OfType<Champion>()
                .Where(champion => champion.Team != EzrealInstance.Team && !champion.IsDead)
                .ToList();

            if (nearbyEnemyChampions.Any())
            {
                return nearbyEnemyChampions
                    .OrderBy(champion => Vector2.Distance(botPosition, champion.Position))
                    .FirstOrDefault();
            }
            return null;
        }

        private bool IsUnderEnemyTower()
        {
            Vector2 botPosition = EzrealInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, TowerDetectionRange, true);

            return units.OfType<BaseTurret>()
                .Any(turret => turret.Team != EzrealInstance.Team);
        }

        private bool IsPositionUnderEnemyTower(Vector2 position, float towerRange = 775.0f)
        {
            return GetUnitsInRange(position, towerRange, true)
                .Any(unit => unit is LaneTurret && unit.Team != EzrealInstance.Team);
        }


        private bool IsAllyTower()
        {
            // Check if current tower is allied
            // Implementation depends on your game engine
            return false;
        }

        private Vector2 GetLanePosition()
        {
            // Return current waypoint or first waypoint
            if (_currentWaypointIndex < _midLaneWaypoints.Count)
            {
                return _midLaneWaypoints[_currentWaypointIndex];
            }
            return _midLaneWaypoints[0];
        }


        private Vector2 GetIdealFarmingPosition()
        {
            // First check if there's an enemy tower nearby
            LaneTurret enemyTower = GetNearbyEnemyTower(1200f);
            if (enemyTower != null)
            {
                float distanceToTower = Vector2.Distance(EzrealInstance.Position, enemyTower.Position);
                
                // If we're close enough to attack, position at auto attack range
                if (distanceToTower <= AutoAttackRange + 200f)
                {
                    return GetSafePositionFromTower(enemyTower);
                }
            }

            // Keep Ezreal at his auto attack range from minions (550 range)
            const float IDEAL_DISTANCE = 550f; // Ezreal's auto range

            // Find enemy minions
            List<LaneMinion> enemyMinions = GetUnitsInRange(EzrealInstance.Position, 1200f, true)
                .OfType<LaneMinion>()
                .Where(m => m.Team != EzrealInstance.Team)
                .ToList();

            if (enemyMinions.Any())
            {
                // Find the frontmost enemy minion (closest to our side)
                Vector2 ourBase = EzrealInstance.Team == TeamId.TEAM_BLUE ?
                    new Vector2(1000f, 1000f) : new Vector2(14000f, 14000f);

                LaneMinion frontMinion = enemyMinions
                    .OrderBy(m => Vector2.Distance(m.Position, ourBase))
                    .FirstOrDefault();

                if (frontMinion != null)
                {
                    // Position behind our minions but at max range from enemy minions
                    Vector2 directionFromMinion = Vector2.Normalize(EzrealInstance.Position - frontMinion.Position);
                    return frontMinion.Position + (directionFromMinion * IDEAL_DISTANCE);
                }
            }

            return GetLanePosition();
        }

        private Vector2 GetIdealPokingPosition()
        {
            // Similar to farming but slightly more aggressive
            return GetIdealFarmingPosition(); // Reuse farming logic as a fallback
        }

        private Vector2 GetSafePosition()
        {
            // Retreat toward your base from mid lane
            Vector2 basePosition = EzrealInstance.Team == TeamId.TEAM_BLUE ?
                new Vector2(1000f, 1000f) : // Blue team base approx
                new Vector2(14000f, 14000f); // Purple team base approx

            // Move directly toward base
            Vector2 directionToBase = Vector2.Normalize(basePosition - EzrealInstance.Position);
            return EzrealInstance.Position + (directionToBase * 600f); // Move 600 units toward base
        }

        private bool IsNearFountain()
        {
            Vector2 fountainPosition = ApiFunctionManager.GetFountainPosition(EzrealInstance.Team);
            float distanceToFountain = Vector2.Distance(EzrealInstance.Position, fountainPosition);
            return distanceToFountain <= FountainShopRange;
        }


        private bool IsInAutoAttackRange(GameObject target)
        {
            if (target == null) return false;
            return Vector2.Distance(EzrealInstance.Position, target.Position) <= AutoAttackRange;
        }

        // Helper method to properly clean up target and orders
        private void ClearTargetAndOrders()
        {
            _logger.Debug("Clearing target and orders");
            EzrealInstance.TargetUnit = null;
            EzrealInstance.UpdateMoveOrder(OrderType.Hold);
        }

        private bool IsInSpellRange(GameObject target, float range)
        {
            if (target == null) return false;

            // Simple distance check
            float distance = Vector2.Distance(EzrealInstance.Position, target.Position);
            return distance <= range;
        }


        private int CountNearbyMinions(Vector2 position, float range, TeamId team)
        {
            List<AttackableUnit> units = GetUnitsInRange(position, range, true);
            return units.OfType<LaneMinion>().Count(minion => minion.Team == team);
        }

        private void MoveToPosition(Vector2 targetPosition, bool applyOffset = true)
        {
            Vector2 botPosition = EzrealInstance.Position;

            // Apply personality offset for hive mind diversity (unless disabled)
            Vector2 offsetTarget = targetPosition;
            if (applyOffset)
            {
                offsetTarget = ApplyPersonalityOffset(targetPosition);
            }

            // If the target itself isn't walkable, find the nearest walkable point
            Vector2 safeTarget = FindNearestWalkable(offsetTarget);

            List<Vector2> waypoints = new List<Vector2> { botPosition, safeTarget };
            EzrealInstance.MoveOrder = OrderType.MoveTo;
            EzrealInstance.SetWaypoints(waypoints);
        }

        private Vector2 FindNearestWalkable(Vector2 position)
        {
            // If already walkable, use it directly
            if (IsWalkable(position.X, position.Y, 35f))
                return position;

            // Spiral outward in increasing radius to find nearest walkable point
            float[] searchRadii = { 50f, 100f, 150f, 200f, 300f };
            int angularSteps = 16; // Check every 22.5 degrees

            foreach (float radius in searchRadii)
            {
                for (int i = 0; i < angularSteps; i++)
                {
                    float angle = (2f * MathF.PI / angularSteps) * i;
                    Vector2 candidate = new Vector2(
                        position.X + radius * MathF.Cos(angle),
                        position.Y + radius * MathF.Sin(angle)
                    );

                    if (IsWalkable(candidate.X, candidate.Y, 35f))
                        return candidate;
                }
            }

            // Fallback: return bot's own position to stop movement rather than walk into a wall
            _logger.Debug($"Could not find walkable position near {position}, staying put");
            return EzrealInstance.Position;
        }

        private Vector2 PredictPosition(GameObject target, float range)
        {
            if (target == null) return Vector2.Zero;

            Vector2 targetPos = target.Position;

            // If target isn't moving, just return current position
            if (target is ObjAIBase aiTarget && aiTarget.MoveOrder == OrderType.Hold)
                return targetPos;

            try
            {
                // Calculate target velocity
                float speed = 0;
                Vector2 direction = Vector2.Zero;

                if (target is ObjAIBase aiBase && aiBase.Waypoints.Count >= 2)
                {
                    // Get target's current waypoint destination
                    Vector2 destination = aiBase.Waypoints.Last();
                    direction = Vector2.Normalize(destination - targetPos);
                    speed = aiBase.Stats.MoveSpeed.Total;
                }

                // If we couldn't get a valid direction, just use current position
                if (direction == Vector2.Zero)
                    return targetPos;

                // Calculate projectile travel time (Q speed is around 2000 units/sec)
                float projectileSpeed = 2000f;
                float distance = Vector2.Distance(EzrealInstance.Position, targetPos);
                float travelTime = distance / projectileSpeed;

                // Predict where target will be after travel time
                Vector2 predictedPos = targetPos + (direction * speed * travelTime);

                // Check if prediction is still in range
                if (Vector2.Distance(EzrealInstance.Position, predictedPos) > range)
                {
                    // If out of range, aim at max range in that direction
                    Vector2 dirToTarget = Vector2.Normalize(predictedPos - EzrealInstance.Position);
                    predictedPos = EzrealInstance.Position + (dirToTarget * range);
                }

                return predictedPos;
            }
            catch (Exception)
            {
                // Fallback to current position if prediction fails
                return targetPos;
            }
        }

        private bool HasLineOfSight(GameObject target)
        {
            // Check if there's a clear line of sight to target
            // Implementation depends on your game engine
            return true;
        }

        private bool IsTargetBehindMinions(GameObject target)
        {
            if (target == null) return true; // Safely return true if no target

            Vector2 botPos = EzrealInstance.Position;
            Vector2 targetPos = target.Position;
            Vector2 direction = Vector2.Normalize(targetPos - botPos);
            float distance = Vector2.Distance(botPos, targetPos);

            // Find minions that might be in the way
            List<LaneMinion> minionsInPath = GetUnitsInRange(botPos, distance, true)
                .OfType<LaneMinion>()
                .Where(m => m.Team != EzrealInstance.Team && m != target)
                .ToList();

            foreach (var minion in minionsInPath)
            {
                // Calculate distance from minion to the line between bot and target
                Vector2 minionPos = minion.Position;

                // Calculate point-line distance to check if minion is in path
                // This is the distance from the minion to the line from bot to target
                float a = targetPos.Y - botPos.Y;
                float b = botPos.X - targetPos.X;
                float c = targetPos.X * botPos.Y - botPos.X * targetPos.Y;

                float pointLineDistance = Math.Abs(a * minionPos.X + b * minionPos.Y + c) /
                                         (float)Math.Sqrt(a * a + b * b);

                // If minion is close to line path and between bot and target
                if (pointLineDistance < 100) // Assuming Q width is approximately 100 units
                {
                    // Check if minion is between bot and target
                    float botToMinion = Vector2.Distance(botPos, minionPos);
                    if (botToMinion < distance)
                    {
                        return true; // Minion is blocking the path
                    }
                }
            }

            // No minions blocking
            return false;
        }

        #region Tower Pushing and Diving Logic

        /// <summary>
        /// Finds the best enemy tower to push based on proximity and safety
        /// </summary>
        private LaneTurret FindBestEnemyTower()
        {
            Vector2 botPosition = EzrealInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, TowerDetectionRange * 2, true);

            var enemyTowers = units.OfType<LaneTurret>()
                .Where(turret => turret.Team != EzrealInstance.Team && !turret.IsDead)
                .OrderBy(turret => Vector2.Distance(botPosition, turret.Position))
                .ToList();

            if (enemyTowers.Any())
            {
                return enemyTowers.First();
            }

            return null;
        }

        /// <summary>
        /// Checks if allied minions are under the tower's range to tank damage
        /// </summary>
        private bool AreMinionsUnderTower(LaneTurret tower)
        {
            if (tower == null) return false;

            float towerRange = tower.Stats.Range.Total;
            List<AttackableUnit> units = GetUnitsInRange(tower.Position, towerRange + 100f, true);

            int alliedMinions = units.OfType<LaneMinion>()
                .Where(minion => minion.Team == EzrealInstance.Team && !minion.IsDead)
                .Count();

            _logger.Debug($"Found {alliedMinions} allied minions under tower at {tower.Position}");
            return alliedMinions >= 2; // Need at least 2 minions to tank
        }

        /// <summary>
        /// Determines if conditions are favorable for pushing a tower
        /// </summary>
        private bool ShouldPushTower()
        {
            // Check if no enemies are nearby
            List<Champion> nearbyEnemies = GetNearbyEnemyChampions(1500f);
            if (nearbyEnemies.Count > 0)
            {
                return false;
            }

            // Find an enemy tower
            LaneTurret tower = FindBestEnemyTower();
            if (tower == null)
            {
                return false;
            }

            // Check if minions are under tower
            if (!AreMinionsUnderTower(tower))
            {
                return false;
            }

            // Check if bot is healthy enough
            if (GetHealthPercentage() < 0.4f)
            {
                return false;
            }

            // Check if we're not too far from tower
            float distanceToTower = Vector2.Distance(EzrealInstance.Position, tower.Position);
            if (distanceToTower > AutoAttackRange + 200f)
            {
                return false;
            }

            _logger.Debug($"Should push tower at {tower.Position} - {nearbyEnemies.Count} enemies nearby");
            return true;
        }

        /// <summary>
        /// Executes tower pushing behavior
        /// </summary>
        private void PushTower()
        {
            LaneTurret tower = FindBestEnemyTower();
            if (tower == null)
            {
                _currentState = BotState.Farming;
                return;
            }

            float distanceToTower = Vector2.Distance(EzrealInstance.Position, tower.Position);

            // Move to attack range if needed
            if (distanceToTower > AutoAttackRange)
            {
                Vector2 directionToTower = Vector2.Normalize(tower.Position - EzrealInstance.Position);
                Vector2 attackPosition = tower.Position - (directionToTower * (AutoAttackRange - 50f));
                MoveToPosition(attackPosition);
            }
            else
            {
                // In range - attack the tower using auto-attack
                EzrealInstance.TargetUnit = tower;
                EzrealInstance.MoveOrder = OrderType.AttackTo;
                
                
                _logger.Debug($"Attacking tower at {tower.Position} with auto-attack");
            }

            // Check for dive opportunity while pushing
            EvaluateDiveOpportunity();
        }

        /// <summary>
        /// Evaluates if a dive opportunity exists while pushing
        /// </summary>
        private void EvaluateDiveOpportunity()
        {
            LaneTurret tower = FindBestEnemyTower();
            if (tower == null) return;

            // Look for enemies under their tower
            List<Champion> enemiesUnderTower = GetNearbyEnemyChampions(tower.Stats.Range.Total + 200f)
                .Where(enemy => Vector2.Distance(enemy.Position, tower.Position) <= tower.Stats.Range.Total)
                .ToList();

            foreach (var enemy in enemiesUnderTower)
            {
                float diveScore = CalculateDiveScore(enemy, tower);
                if (diveScore >= MinDiveScore)
                {
                    _logger.Debug($"Dive opportunity detected on {enemy.Name} with score {diveScore:F2}");
                    _diveTarget = enemy;
                    _currentState = BotState.Diving;
                    return;
                }
            }
        }

        /// <summary>
        /// Calculates a dive score for a target - smooth scoring instead of binary
        /// </summary>
        private float CalculateDiveScore(Champion target, LaneTurret tower)
        {
            float score = 0f;

            // Enemy health factor (0.0 to 1.0, where lower is better)
            float enemyHealthPct = target.Stats.CurrentHealth / target.Stats.HealthPoints.Total;
            if (enemyHealthPct <= 0.15f)
                score += 4.0f; // Very low health - excellent dive target
            else if (enemyHealthPct <= 0.25f)
                score += 3.0f; // Low health - good dive target
            else if (enemyHealthPct <= 0.35f)
                score += 1.5f; // Moderate health - risky but possible
            else
                score -= 2.0f; // High health - don't dive

            // Number advantage factor
            int nearbyAllies = GetNearbyEnemiesOfTarget(target, 1000f); // Allies of Ezreal near target
            int nearbyEnemiesOfTarget = GetNearbyAlliesOfTarget(target, 1000f); // Enemy allies near target

            if (nearbyAllies >= 2 && nearbyEnemiesOfTarget == 0)
                score += 3.0f; // 2v1 or better - great dive
            else if (nearbyAllies >= 1 && nearbyEnemiesOfTarget == 0)
                score += 1.5f; // 1v1 - decent dive
            else if (nearbyAllies == 0 && nearbyEnemiesOfTarget >= 1)
                score -= 3.0f; // Outnumbered - dangerous

            // Level difference factor
            int levelDiff = EzrealInstance.Stats.Level - target.Stats.Level;
            score += Math.Min(Math.Max(levelDiff * 0.6f, -2f), 3f); // +/- 0.6 per level, capped

            // Tower factor - closer to tower edge is safer
            float distToTowerEdge = Vector2.Distance(target.Position, tower.Position) - tower.Stats.Range.Total;
            if (distToTowerEdge > 0)
                score += 0.5f; // Target is outside tower range
            else
                score -= Math.Abs(distToTowerEdge) / 200f; // Penalty for being deeper in tower range

            // Bot health factor
            float botHealthPct = GetHealthPercentage();
            if (botHealthPct > 0.7f)
                score += 1.0f; // Healthy - safer dive
            else if (botHealthPct > 0.5f)
                score += 0.0f; // Moderate - neutral
            else
                score -= 1.5f; // Low health - risky dive

            // Available cooldowns factor
            if (IsSpellAvailable(ESlot, SpellSlotType.SpellSlots))
                score += 1.0f; // Have escape ready
            else
                score -= 0.5f; // No escape available

            if (IsSpellAvailable(QSlot, SpellSlotType.SpellSlots) && IsSpellAvailable(WSlot, SpellSlotType.SpellSlots))
                score += 0.5f; // Have damage spells ready

            _logger.Debug($"Dive score for {target.Name}: health={enemyHealthPct:F2}, allies={nearbyAllies}, " +
                         $"enemies={nearbyEnemiesOfTarget}, levelDiff={levelDiff}, score={score:F2}");

            return score;
        }

        /// <summary>
        /// Executes diving behavior on a target under tower
        /// </summary>
        private void ExecuteDive()
        {
            if (_diveTarget == null || _diveTarget.IsDead)
            {
                _currentState = BotState.Pushing;
                _diveTarget = null;
                return;
            }

            LaneTurret tower = FindBestEnemyTower();
            float distToTower = tower != null ? Vector2.Distance(EzrealInstance.Position, tower.Position) : float.MaxValue;

            // Check if dive is still favorable
            if (distToTower > tower.Stats.Range.Total + 300f)
            {
                _logger.Debug("Too far from tower to continue dive");
                _currentState = BotState.Pushing;
                _diveTarget = null;
                return;
            }

            float currentDiveScore = CalculateDiveScore(_diveTarget, tower);
            if (currentDiveScore < MinDiveScore * 0.7f)
            {
                _logger.Debug($"Dive score dropped to {currentDiveScore:F2}, aborting dive");
                _currentState = BotState.Retreating;
                _diveTarget = null;
                return;
            }

            // Execute dive combo
            float distance = Vector2.Distance(EzrealInstance.Position, _diveTarget.Position);

            // Cast W for damage boost
            if (IsSpellAvailable(WSlot, SpellSlotType.SpellSlots) && distance <= WRange)
            {
                CastW(_diveTarget);
            }

            // Cast Q for damage
            if (IsSpellAvailable(QSlot, SpellSlotType.SpellSlots) && distance <= QRange)
            {
                CastQ(_diveTarget);
            }

            // Auto attack
            if (IsInAutoAttackRange(_diveTarget))
            {
                EzrealInstance.MoveOrder = OrderType.AttackTo;
                EzrealInstance.TargetUnit = _diveTarget;
            }
            else
            {
                // Move closer
                MoveToPosition(_diveTarget.Position);
            }

            // Use E aggressively if safe and needed
            if (distance > AutoAttackRange + 100 && IsSpellAvailable(ESlot, SpellSlotType.SpellSlots))
            {
                if (IsSafeToUseAggressiveE(_diveTarget))
                {
                    CastAggressiveE(_diveTarget);
                }
            }

            // Use R for execution
            float targetHealthPct = _diveTarget.Stats.CurrentHealth / _diveTarget.Stats.HealthPoints.Total;
            if (targetHealthPct < 0.15f && IsSpellAvailable(RSlot, SpellSlotType.SpellSlots))
            {
                CastR(_diveTarget);
            }

            // Prepare to retreat after kill or if target escapes
            if (_diveTarget.IsDead || targetHealthPct > 0.5f)
            {
                _currentState = BotState.Retreating;
                _diveTarget = null;
            }
        }

        #endregion

        #region Attack Weaving and Orbwalking

        /// <summary>
        /// Gets the current auto-attack cooldown based on attack speed
        /// </summary>
        private float GetAutoAttackCooldown()
        {
            float attackSpeed = EzrealInstance.Stats.GetTotalAttackSpeed();
            return 1.0f / attackSpeed;
        }

        /// <summary>
        /// Checks if basic attack is currently casting (can't move)
        /// </summary>
        private bool IsBasicAttackCasting()
        {
            if (EzrealInstance.AutoAttackSpell == null) return false;
            return EzrealInstance.AutoAttackSpell.State == SpellState.STATE_CASTING;
        }

        /// <summary>
        /// Checks if basic attack is on cooldown (can move)
        /// </summary>
        private bool IsBasicAttackOnCooldown()
        {
            if (EzrealInstance.AutoAttackSpell == null) return false;
            return EzrealInstance.AutoAttackSpell.State == SpellState.STATE_COOLDOWN;
        }

        /// <summary>
        /// Performs orbwalking movement during auto-attack cooldown
        /// </summary>
        private void PerformOrbwalk(Vector2 targetPosition, AttackableUnit attackTarget = null)
        {
            if (ShouldPushTower())
            {
                return;
            }

            // If casting, stand still
            if (IsBasicAttackCasting())
            {
                EzrealInstance.UpdateMoveOrder(OrderType.Stop);
                return;
            }

            // If on cooldown, move unpredictably
            if (IsBasicAttackOnCooldown())
            {
                // Update orbwalk direction periodically
                if (_gameTime - _lastOrbwalkTime >= MinOrbwalkInterval)
                {
                    _lastOrbwalkTime = _gameTime;

                    // Generate random movement direction (±90 degrees from target)
                    Vector2 toTarget = targetPosition - EzrealInstance.Position;
                    if (toTarget == Vector2.Zero) toTarget = Vector2.UnitX;

                    float baseAngle = MathF.Atan2(toTarget.Y, toTarget.X);
                    float randomAngle = baseAngle + ((float)(_personalityRandom.NextDouble() * Math.PI) - MathF.PI / 2);

                    _orbwalkDirection = new Vector2(
                        MathF.Cos(randomAngle),
                        MathF.Sin(randomAngle)
                    );
                }

                // Move in the chosen direction at reduced speed
                Vector2 moveTarget = EzrealInstance.Position + (_orbwalkDirection * 100f);
                Vector2 safeMoveTarget = FindNearestWalkable(moveTarget);

                EzrealInstance.MoveOrder = OrderType.MoveTo;
                List<Vector2> waypoints = new List<Vector2> { EzrealInstance.Position, safeMoveTarget };
                EzrealInstance.SetWaypoints(waypoints);
            }
            else if (attackTarget != null && IsInAutoAttackRange(attackTarget))
            {
                // Attack is ready and target in range - attack
                EzrealInstance.MoveOrder = OrderType.AttackTo;
                EzrealInstance.TargetUnit = attackTarget;
            }
        }

        /// <summary>
        /// Applies personality offset to a position for hive mind diversity
        /// </summary>
        private Vector2 ApplyPersonalityOffset(Vector2 position)
        {
            // Add current offset to position
            Vector2 offsetPosition = position + _currentOffset;

            // Ensure the offset position is walkable
            return FindNearestWalkable(offsetPosition);
        }

        #endregion

        // Trash talk toggle — set to false to disable entirely
        private const bool TrashTalkEnabled = false;

        // Use personality random (unique per bot) instead of static random (shared)
        // This prevents all bots from spamming at the same time

        // How often the bot can trash talk (seconds) - randomized per bot
        private float _trashTalkCooldown = 30f;
        private float _lastTrashTalkTime = -999f;

        // Thresholds that trigger specific trash talk categories
        private const float KillStreakThreshold = 2f;  // kills before bragging
        private const float DeathStreakThreshold = 2f;  // deaths before raging
        private int _killCount = 0;
        private int _deathCount = 0;

        // Delayed message queue for kills/deaths - bypasses normal cooldown
        private class DelayedMessage
        {
            public string Message { get; set; }
            public float SendTime { get; set; }
            public float BaseDelay { get; set; } // Base delay before sending
            public float ReadTime { get; set; } // Additional time based on message length (time to "type")
        }
        private List<DelayedMessage> _delayedMessages = new List<DelayedMessage>();
        private const float BaseReactionDelay = 3f; // 3-8 seconds base delay
        private const float MaxReactionDelay = 8f;
        private const float TypingSpeed = 15f; // Characters per second typing speed


        private static readonly string[] _generalTaunts =
        {
    "report my team",
    "gg ez",
    "this team is holding me back",
    "hardstuck players smh",
    "i cant carry any harder",
    "anyone else on my team even trying?",
    "ff 15 my team is inting",
    "i am literally 1v9 right now",
    "where is my team lol",
    "this is why i have trust issues",
    "uninstall please",
    "my goldfish could play better",
    "i need better teammates",
    "actual bots on my team (no offense to me)",
    "alt f4 for a free skin"
};

        private static readonly string[] _dyingTaunts =
        {
    "LAG",
    "my mouse slipped",
    "i wasnt even trying",
    "this keyboard is trash",
    "didnt want that kill anyway",
    "that was definitely a misplay on my part... jk report jungle",
    "nice hack",
    "reported",
    "my cat walked on my keyboard",
    "whatever i have 400 ping",
    "open mid",
    "OMG REPORT THIS TROLL I SWEAR ON MY MOMS LIFE I WILL FIND YOU I HAVE YOUR IP I AM A HACKER IN REAL LIFE I WORK FOR THE PENTAGON YOU ARE FINISHED DOG.",
    "My team is doing 0 dmg report them all",
    "Better nerf irelia",
    "My team is feeders",
    "You are playing a braindead champion, of course you're winning",
    "Omg this team 0 vision wtf",
    "WHAT IS THIS BULLSHIT",
    "ARE YOU KIDDING ME?",
    "Ooh I got rekt yeah yeah. Go tell your mom",
    "Wtf that was a bug, it doesn't count",
    "Just end this"





};

        private static readonly string[] _killingTaunts =
        {
    "too easy",
    "did you even go to practice tool?",
    "LOL",
    "get rekt",
    "skill diff",
    "come back when you hit gold",
    "you should switch to a simpler game",
    "EZ Clap",
    "i was shopping that fight btw",
    "delete this game bro",
    "go play animal crossing",
    "thank you for the gold very generous",
    "you are finished dog",
    "your father left because of your positioning",
    "stick to minecraft",
    "try turning on your monitor",
    "nice flash",
    "ez",
    "I’ve seen better mechanics in a 2008 Honda Civic.",
    "thanks for the free lp",
    "That was a nice attempt. Emphasis on attempt.",
    "I see you're playing the new tutorial difficulty",
    "You're doing great! ...at feeding",
    "Your mechanics are sponsored by PowerPoint",
    "You mad cuz bad",
    "Git gud",
    "Do you have are stupid",
    "RIP bozo",
    "Get pwned n00b",
    "This is the worst team I've ever had in my entire life",
    "You are so bad it's not even funny LMAO",
    "Boosted bonobo",
    "I'm not like the rest of you. I'm stronger, I'm smarter, I'm better",
    "Survival instinct of an orange cat",
    "Sell your items, it gives you more damage btw",
    "Do you even know how to move?",
    "lmao skill issue",
    "Your positioning is avant-garde",
    "your skills are trash",
    "Impressive skill issue",
    "I respect the confidence more than the execution"

};

        private static readonly string[] _lowHealthTaunts =
        {
    "YOLO",
    "this is fine",
    "i play better when im tilted",
    "dont worry i meant to do that",
    "low health = more damage everyone knows this",
};

        private static readonly string[] _generalGameTaunts =
        {
    "mid diff",
    "jungle diff",
    "support diff",
    "this is a skill based game and it shows",
    "i peaked plat and it wasnt even hard",
    "diamond is literally elo hell",
    "bro think he's faker",
    "just ward bro its not that hard",
    "how are you level 4 right now",
    "you built that?",
};

        private void InitializeTrashTalk()
        {
            // Randomize cooldown per bot (25-45 seconds) to prevent synchronized spam
            _trashTalkCooldown = 25f + (float)(_personalityRandom.NextDouble() * 20f);
            _logger.Debug($"Trash talk cooldown set to {_trashTalkCooldown:F1} seconds for {EzrealInstance.Model}");
        }

        private void TryTrashTalk(string[] pool = null)
        {
            // Check if trash talk is enabled via BotSettings or local toggle
            if ((!TrashTalkEnabled && !_botSettings.EnableTrashTalk) || _gameTime - _lastTrashTalkTime < _trashTalkCooldown)
                return;

            // Use the new API for contextual trash talk
            SendContextualTrashTalk(EzrealInstance, _botSettings, GetHealthPercentage(), _killCount, _deathCount);

            _lastTrashTalkTime = _gameTime;
            // Randomize next cooldown slightly
            _trashTalkCooldown = 25f + (float)(_personalityRandom.NextDouble() * 20f);
        }

        /// <summary>
        /// Queues a kill/death reaction message to be sent after a realistic delay.
        /// This bypasses the normal cooldown and considers message length for typing time.
        /// </summary>
        private void QueueReactionMessage(string[] pool)
        {
            if (!TrashTalkEnabled || pool == null || pool.Length == 0) return;

            string message = pool[_personalityRandom.Next(pool.Length)];

            // Calculate base delay (3-8 seconds)
            float baseDelay = BaseReactionDelay + (float)(_personalityRandom.NextDouble() * (MaxReactionDelay - BaseReactionDelay));

            // Calculate typing time based on message length (longer messages = more time to "type")
            float typingTime = message.Length / TypingSpeed;

            // Total delay = base reaction time + typing time
            float totalDelay = baseDelay + typingTime;

            var delayedMsg = new DelayedMessage
            {
                Message = message,
                SendTime = _gameTime + totalDelay,
                BaseDelay = baseDelay,
                ReadTime = typingTime
            };

            _delayedMessages.Add(delayedMsg);
            _logger.Debug($"[TrashTalk] Queued reaction message (delay: {totalDelay:F1}s - base: {baseDelay:F1}s, typing: {typingTime:F1}s): {message}");
        }

        /// <summary>
        /// Processes any pending delayed messages. Call this in OnUpdate.
        /// </summary>
        private void ProcessDelayedMessages()
        {
            if (!TrashTalkEnabled || _delayedMessages.Count == 0) return;

            // Find messages that are ready to send
            var readyMessages = _delayedMessages.Where(m => _gameTime >= m.SendTime).ToList();

            foreach (var msg in readyMessages)
            {
                SendChatMessage(msg.Message);
                _delayedMessages.Remove(msg);
                _logger.Debug($"[TrashTalk] Sent delayed message after {msg.BaseDelay + msg.ReadTime:F1}s delay");
            }
        }

        private string[] PickContextualPool()
        {
            if (GetHealthPercentage() < 0.2f)
                return _lowHealthTaunts;

            if (_killCount >= KillStreakThreshold)
                return _killingTaunts;

            if (_deathCount >= DeathStreakThreshold)
                return _dyingTaunts;

            // Alternate between general pools using personality random
            return _personalityRandom.NextDouble() < 0.5 ? _generalTaunts : _generalGameTaunts;
        }

        /// <summary>
        /// Sends a chat message formatted like a real player message.
        /// Shows proper team colors (green for allies, red for enemies) and champion name.
        /// </summary>
        private void SendChatMessage(string message, bool allChat = true)
        {
            if (!TrashTalkEnabled || EzrealInstance == null) return;

            // Use the new team-specific chat method
            // This sends the message with proper formatting:
            // - Green [All] prefix and champion name to own team
            // - Red [All] prefix and champion name to enemy team
            ApiFunctionManager.PrintPlayerChat(
                EzrealInstance.Name,
                EzrealInstance.Model,
                EzrealInstance.Team,
                message,
                allChat
            );

            _logger.Debug($"[TrashTalk] {EzrealInstance.Name} ({EzrealInstance.Model}): {message}");
        }
    }
}