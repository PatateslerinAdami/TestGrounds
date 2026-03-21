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
using Spells;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using System.Threading.Tasks;
using GameServerCore.NetInfo;
using LeagueSandbox.GameServer.GameObjects;
using GameMaths;
using System.Diagnostics;
using static GameServerLib.GameObjects.AttackableUnits.DamageData;
using LeagueSandbox.GameServer.Content.Navigation;
using LeaguePackets.Game.Events;
using static LENet.Protocol;

namespace AIScripts
{
    public class EzrealBot : IAIScript
    {
        // Champion reference
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData();

        private Champion EzrealInstance;
        private static ILog _logger = LoggerProvider.GetLogger();
        private Vector2 _midLanePosition = new Vector2(7500f, 7500f);


        // State management
        private BotState _currentState;
        private float _gameTime;
        private float _lastDamageTakenTime;
        private bool isInCombat;
        private bool isUnderTower;
        private bool MovingToLane;
        private Champion _followTarget;
        private const float CombatCooldownTime = 5.0f; // Time to consider out of combat (seconds)

        // Spell slots
        private readonly byte QSlot = 0;
        private readonly byte WSlot = 1;
        private readonly byte ESlot = 2;
        private readonly byte RSlot = 3;
        private readonly byte IgniteSlot = 0; // Summoner spell slot
        private Dictionary<byte, float> _lastCastTime = new Dictionary<byte, float>();


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
            DeadState
        }

        public void OnActivate(ObjAIBase owner)
        {
            if (owner is Champion champion)
            {
                EzrealInstance = champion;
                EzrealInstance.IsBot = true;

                _currentState = BotState.MovingToLane;
                ApiEventManager.OnTakeDamage.AddListener(this, EzrealInstance, OnTakeDamage, false);

                // Initialize with first skill point
                EzrealInstance.LevelUpSpell(QSlot); // Start with Q for lane poke

                Console.WriteLine("Ezreal Bot initialized with state: " + _currentState);
            }
        }

        public void OnUpdate(float diff)
        {
            _gameTime += diff / 1000f; // Convert to seconds

            // Check if combat state has expired
            if (isInCombat && _gameTime - _lastDamageTakenTime > CombatCooldownTime)
            {
                isInCombat = false;
            }

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
            if (!EzrealInstance.IsDead && !isInCombat)
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
                    if (isUnderTower && !IsAllyTower())
                    {
                        _currentState = BotState.Retreating;
                    }
                    else if (nearbyEnemies.Count > 0 && CanPoke())
                    {
                        _currentState = BotState.Poking;
                    }
                    // Add the check for chase state here
                    else if (ShouldChaseEnemy())
                    {
                        _logger.Debug("Found vulnerable enemy - switching to chase mode");
                        _currentState = BotState.Chasing;
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
                        foreach (var enemy in nearbyEnemies)
                        {
                            if (IsChampionVulnerable(enemy))
                            {
                                _currentState = BotState.Chasing;
                                foundVulnerable = true;
                                break;
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
                        _currentState = BotState.Retreating;
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
            }
        }

        // Movement and positioning
        private void MoveToLane()
        {
            // Movement code to go to lane
            Vector2 lanePosition = GetLanePosition();
            MoveToPosition(_midLanePosition);
            MovingToLane = true;
        }

        private void Farm()
        {
            // Reset last chase target when back to farming
            _lastChaseTarget = null;

            Vector2 botPosition = EzrealInstance.Position;
            List<AttackableUnit> units = GetUnitsInRange(botPosition, 800f, true);

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
                EzrealInstance.MoveOrder = OrderType.AttackTo;
                EzrealInstance.TargetUnit = targetMinion;
            }
            else
            {
                var qKillableMinions = GetUnitsInRange(botPosition, QRange, true).OfType<Minion>()
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
                    MoveToPosition(GetIdealFarmingPosition());
                }
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

            // Only auto-attack if we're already in a good position and it's safe
            Champion nearbyTarget = GetClosestEnemyChampion();
            if (nearbyTarget != null && IsInAutoAttackRange(nearbyTarget) && !IsSafeToEngageChampion(nearbyTarget))
            {
                _logger.Debug($"Auto attacking {nearbyTarget.Name} during poke");
                _currentState = BotState.Poking;
                EzrealInstance.MoveOrder = OrderType.AttackTo;
                EzrealInstance.TargetUnit = nearbyTarget;
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

            // Auto attack if in range
            if (IsInAutoAttackRange(target))
            {
                EzrealInstance.MoveOrder = OrderType.AttackTo;
                EzrealInstance.TargetUnit = target;
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

                // Engine requires at least one target in the list — pass self as a dummy
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

                // Consider an enemy vulnerable if their health is below certain threshold
                float enemyHealthPct = enemy.Stats.CurrentHealth / enemy.Stats.HealthPoints.Total;
                int nearbyAllies = GetNearbyAlliesOfTarget(enemy, 800f);
                float distance = Vector2.Distance(EzrealInstance.Position, enemy.Position);

                // Calculate a "vulnerability score" for this enemy
                float score = 0f;

                // Low health is good
                if (enemyHealthPct < 0.3f)
                    score += 3.0f;
                else if (enemyHealthPct < 0.5f)
                    score += 1.5f;

                // Isolated targets are good
                if (nearbyAllies == 0)
                    score += 1.0f;

                // Closer targets are better (but not too much weight on this)
                score += (1500f - distance) / 1500f;

                // Debug output
                _logger.Debug($"Enemy {enemy.Name}: health={enemyHealthPct:F2}, allies={nearbyAllies}, distance={distance:F0}, score={score:F2}");

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
                _logger.Debug($"Auto attacking {target.Name} during chase");
                EzrealInstance.MoveOrder = OrderType.AttackTo;
                EzrealInstance.TargetUnit = target;
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
                _logger.Debug($"Auto attacking {target.Name} during chase");
                EzrealInstance.MoveOrder = OrderType.AttackTo;
                EzrealInstance.TargetUnit = target;
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
            // Return number of allies near the target
            // Implementation depends on your game engine
            return 0;
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
            // Just return mid lane position
            return _midLanePosition;
        }


        private Vector2 GetIdealFarmingPosition()
        {
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

                Minion frontMinion = enemyMinions
                    .OrderBy(m => Vector2.Distance(m.Position, ourBase))
                    .FirstOrDefault();

                if (frontMinion != null)
                {
                    // Position behind our minions but at max range from enemy minions
                    Vector2 directionFromMinion = Vector2.Normalize(EzrealInstance.Position - frontMinion.Position);
                    return frontMinion.Position + (directionFromMinion * IDEAL_DISTANCE);
                }
            }

            return _midLanePosition;
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

        private void MoveToPosition(Vector2 targetPosition)
        {
            Vector2 botPosition = EzrealInstance.Position;

            // If the target itself isn't walkable, find the nearest walkable point
            Vector2 safeTarget = FindNearestWalkable(targetPosition);

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
    }
}