using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net;
using System;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.Buffers;
using System.Linq;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class CharacterModelOverride
    {
        public string Model { get; internal set; }
        public uint SkinID { get; internal set; }
        public uint ID { get; internal set; }
        internal object Source { get; set; }
    }

    public class DelayedSpellPacketInfo
    {
        public Spell SpellToPacketize { get; }
        public float CreationTime { get; }

        public DelayedSpellPacketInfo(Spell spell, float creationTime)
        {
            SpellToPacketize = spell;
            CreationTime = creationTime;
        }
    }
    public class SpellQueueEntry
    {
        public Spell Spell { get; }
        public byte SpellSlot { get; }
        public Vector2 Start { get; }
        public Vector2 End { get; }
        public AttackableUnit TargetUnit { get; }
        public float ExpiresAt { get; }
        public ForceMovementParameters AwaitedMovement { get; set; }
        public Action OnSuccess { get; }

        public SpellQueueEntry(Spell spell, Vector2 start, Vector2 end, AttackableUnit targetUnit,
            float expiresAt, ForceMovementParameters awaitedMovement = null, Action onSuccess = null)
        {
            Spell = spell;
            SpellSlot = spell.CastInfo.SpellSlot;
            Start = start;
            End = end;
            TargetUnit = targetUnit;
            ExpiresAt = expiresAt;
            AwaitedMovement = awaitedMovement;
            OnSuccess = onSuccess;
        }
    }
    /// <summary>
    /// Base class for all moving, attackable, and attacking units.
    /// ObjAIBases normally follow these guidelines of functionality: Self movement, Inventory, Targeting, Attacking, and Spells.
    /// </summary>
    public class ObjAIBase : AttackableUnit
    {
        public int hitCount = 0;
        internal readonly List<AssistMarker> AlliedAssistMarkers = new List<AssistMarker>();
        internal readonly List<AssistMarker> EnemyAssistMarkers = new List<AssistMarker>();
        // Crucial Vars
        protected float _autoAttackCurrentCooldown;
        protected bool _skipNextAutoAttack;
        protected Spell _castingSpell;
        private Spell _lastAutoAttack;
        private Spell _lastOverrideAutoAttack;
        private SpellQueueEntry _queuedSpellCast;
        private Action _spellToCastOnSuccess;
        private bool _spellToCastIsPlayerInput;
        private byte _spellToCastInputSlot;
        private int _moveOrderRevision;
        private ForceMovementParameters _movementOrderToResume;
        private bool _updatingMovement;
        private readonly Random _random = new Random();
        private readonly List<Spell> _autoAttackOverrideSpells = new List<Spell>();
        private readonly Dictionary<Spell, float> _autoAttackOverrideWeights = new Dictionary<Spell, float>();
        private Spell _autoAttackOverrideCritSpell;
        protected ItemManager _itemManager;
        protected AIState _aiState = AIState.AI_IDLE;
        protected bool _aiPaused;
        protected Pet _lastPetSpawned;
        private static ILog _logger = LoggerProvider.GetLogger();

        /// <summary>
        /// Name assigned to this unit.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Variable storing all the data related to this AI's current auto attack. *NOTE*: Will be deprecated as the spells system gets finished.
        /// </summary>
        public Spell AutoAttackSpell { get; protected set; }
        /// <summary>
        /// Spell this AI is currently channeling.
        /// </summary>
        public Spell ChannelSpell { get; protected set; }
        /// <summary>
        /// The ID of the skin this unit should use for its model.
        /// </summary>
        public int SkinID { get; set; }
        private readonly List<CharacterModelOverride> _modelOverrides = new List<CharacterModelOverride>();
        public IReadOnlyList<CharacterModelOverride> ModelOverrides
        {
            get { return _modelOverrides; }
        }
        public bool HasAutoAttacked { get; set; }
        /// <summary>
        /// Whether or not this AI has made their first auto attack against their current target. Refreshes after untargeting or targeting another unit.
        /// </summary>
        public bool HasMadeInitialAttack { get; set; }
        /// <summary>
        /// Variable housing all variables and functions related to this AI's Inventory, ex: Items.
        /// </summary>
        /// TODO: Verify if we want to move this to AttackableUnit since items are related to stats.
        public InventoryManager Inventory { get; protected set; }
        /// <summary>
        /// Whether or not this AI is currently auto attacking.
        /// </summary>
        public bool IsAttacking { get; private set; }
        public bool IsAutoAttackOverridden { get; private set; }
        /// <summary>
        /// Spell this unit will cast when in range of its target.
        /// Overrides auto attack spell casting.
        /// </summary>
        public Spell SpellToCast { get; protected set; }
        /// <summary>
        /// Whether or not this AI's auto attacks apply damage to their target immediately after their cast time ends.
        /// </summary>
        public bool IsMelee { get; set; }
        public bool IsNextAutoCrit { get; protected set; }
        /// <summary>
        /// Current order this AI is performing.
        /// </summary>
        /// TODO: Rework AI so this enum can be finished.
        public OrderType MoveOrder { get; set; }
        /// <summary>
        /// Unit this AI will auto attack or use a spell on when in range.
        /// </summary>
        public AttackableUnit TargetUnit { get; set; }
        public bool KeepChasingDuringAttackCooldown { get; set; }
        public Dictionary<short, Spell> Spells { get; }
        public ICharScript CharScript { get; private set; }
        public bool IsBot { get; set; }
        public bool IgnoreMoveOrders { get; set; }
        public IAIScript AIScript { get; protected set; }
        public List<DelayedSpellPacketInfo> delayedSpellPackets = new List<DelayedSpellPacketInfo>();
        private bool invisSent = false;
        private bool _charScriptActivated;
        private bool _charScriptPostActivated;
        private bool _scriptsEnabled = true;
        public Vector2 MovementRestrictionCenter { get; set; }
        public float MovementRestrictionRadius { get; set; }
        public Vector2 Velocity { get; set; } = Vector2.Zero;
        public float StuckTime { get; set; } = 0f;
        public ObjAIBase(Game game, string model, string name = "", int collisionRadius = 0,
            Vector2 position = new Vector2(), int visionRadius = 0, int skinId = 0, uint netId = 0, TeamId team = TeamId.TEAM_NEUTRAL, Stats stats = null, string aiScript = "", bool enableScripts = true) :
            base(game, model, collisionRadius, position, visionRadius, netId, team, stats)
        {
            _itemManager = game.ItemManager;
            _scriptsEnabled = enableScripts;

            Name = name;
            SkinID = skinId;
            Inventory = InventoryManager.CreateInventory(game.PacketNotifier);

            // TODO: Centralize this instead of letting it lay in the initialization.
            if (collisionRadius > 0)
            {
                CollisionRadius = collisionRadius;
            }
            else if (CharData.GameplayCollisionRadius > 0)
            {
                CollisionRadius = CharData.GameplayCollisionRadius;
            }
            else
            {
                CollisionRadius = 40;
            }

            if (CharData.PathfindingCollisionRadius > 0)
            {
                PathfindingRadius = CharData.PathfindingCollisionRadius;
            }
            else
            {
                PathfindingRadius = 40;
            }

            // TODO: Centralize this instead of letting it lay in the initialization.
            if (visionRadius > 0)
            {
                VisionRadius = visionRadius;
            }
            else if (CharData.PerceptionBubbleRadius > 0)
            {
                VisionRadius = CharData.PerceptionBubbleRadius;
            }
            else
            {
                VisionRadius = 1100;
            }

            Stats.CurrentMana = Stats.ManaPoints.Total;
            Stats.CurrentHealth = Stats.HealthPoints.Total;

            SpellToCast = null;

            Spells = new Dictionary<short, Spell>();

            if (!string.IsNullOrEmpty(model))
            {
                IsMelee = CharData.IsMelee;

                // SpellSlots
                // 0 - 3
                for (short i = 0; i < CharData.SpellNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(CharData.SpellNames[i]))
                    {
                        Spells[i] = new Spell(game, this, CharData.SpellNames[i], (byte)i, enableScripts);
                    }
                }

                //If character has a passive spell, it'll initialize the CharScript with it
                if (!string.IsNullOrEmpty(CharData.PassiveData.PassiveLuaName))
                {
                    Spells[(int)SpellSlotType.PassiveSpellSlot] = new Spell(game, this, CharData.PassiveData.PassiveLuaName, (int)SpellSlotType.PassiveSpellSlot, enableScripts);
                }
                //If there's no passive spell, it'll just initialize the CharScript with Spell = null
                else if (enableScripts)
                {
                    LoadCharScript();
                }

                Spells[(int)SpellSlotType.SummonerSpellSlots] = new Spell(game, this, "BaseSpell", (int)SpellSlotType.SummonerSpellSlots, enableScripts);
                Spells[(int)SpellSlotType.SummonerSpellSlots + 1] = new Spell(game, this, "BaseSpell", (int)SpellSlotType.SummonerSpellSlots + 1, enableScripts);

                // InventorySlots
                // 6 - 12 (12 = TrinketSlot)
                for (byte i = (int)SpellSlotType.InventorySlots; i < (int)SpellSlotType.BluePillSlot; i++)
                {
                    Spells[i] = new Spell(game, this, "BaseSpell", i, enableScripts);
                }

                Spells[(int)SpellSlotType.BluePillSlot] = new Spell(game, this, "BaseSpell", (int)SpellSlotType.BluePillSlot, enableScripts);
                Spells[(int)SpellSlotType.TempItemSlot] = new Spell(game, this, "BaseSpell", (int)SpellSlotType.TempItemSlot, enableScripts);

                // RuneSlots
                // 15 - 44
                for (short i = (int)SpellSlotType.RuneSlots; i < (int)SpellSlotType.ExtraSlots; i++)
                {
                    Spells[(byte)i] = new Spell(game, this, "BaseSpell", (byte)i, enableScripts);
                }

                // ExtraSpells
                // 45 - 60
                for (short i = 0; i < CharData.ExtraSpells.Length; i++)
                {
                    var extraSpellName = "BaseSpell";
                    if (!string.IsNullOrEmpty(CharData.ExtraSpells[i]))
                    {
                        extraSpellName = CharData.ExtraSpells[i];
                    }

                    var slot = i + (int)SpellSlotType.ExtraSlots;
                    Spells[(byte)slot] = new Spell(game, this, extraSpellName, (byte)slot, enableScripts);
                    Spells[(byte)slot].LevelUp();
                }

                Spells[(int)SpellSlotType.RespawnSpellSlot] = new Spell(game, this, "BaseSpell", (int)SpellSlotType.RespawnSpellSlot, enableScripts);
                Spells[(int)SpellSlotType.UseSpellSlot] = new Spell(game, this, "BaseSpell", (int)SpellSlotType.UseSpellSlot, enableScripts);

                // BasicAttackNormalSlots & BasicAttackCriticalSlots
                // 64 - 72 & 73 - 81
                for (short i = 0; i < CharData.BasicAttacks.Count; i++)
                {
                    if (!string.IsNullOrEmpty(CharData.BasicAttacks[i].Name))
                    {
                        int slot = i + (int)SpellSlotType.BasicAttackNormalSlots;
                        Spells[(byte)slot] = new Spell(game, this, CharData.BasicAttacks[i].Name, (byte)slot, enableScripts);
                    }
                }

                AutoAttackSpell = GetNewAutoAttack();
            }
            else
            {
                IsMelee = true;
            }

            // Ensure CharScript is initialized if it wasn't loaded (e.g. enableScripts=false or no model)
            if (CharScript == null)
            {
                CharScript = new CharScriptEmpty();
            }

            AIScript = string.IsNullOrEmpty(aiScript)
                || aiScript == "Core.Scripting.CSharp.EmptyAIScript"
                || aiScript == typeof(EmptyAIScript).FullName
                ? new EmptyAIScript()
                : game.ScriptEngine.CreateObject<IAIScript>("AIScripts", aiScript) ?? new EmptyAIScript();
            if (enableScripts)
            {
                try
                {
                    AIScript.OnActivate(this);
                }
                catch (Exception e)
                {
                    _logger.Error(null, e);
                }
            }
        }

        public override void OnAdded()
        {
            base.OnAdded();
            if (_scriptsEnabled)
            {
                try
                {
                    CharScript.OnActivate(
                        this, Spells.GetValueOrDefault<short, Spell>(
                            (int)SpellSlotType.PassiveSpellSlot
                        )
                    );
                }
                catch (Exception e)
                {
                    _logger.Error(null, e);
                }

                _charScriptActivated = true;
                TryPostActivateCharScript();
                TryPostActivateSpellScripts();
            }
        }

        /// <summary>
        /// Loads the Passive Script
        /// </summary>
        public void LoadCharScript(Spell spell = null)
        {
            DeactivateCharScript();
            CharScript = _game.ScriptEngine.CreateObject<ICharScript>("CharScripts", $"CharScript{Model}") ?? new CharScriptEmpty();
            _charScriptActivated = false;
            _charScriptPostActivated = false;
        }

        private void DeactivateCharScript()
        {
            if (!_charScriptActivated || CharScript == null) return;
            _charScriptActivated = false;
            _charScriptPostActivated = false;
            try
            {
                CharScript.OnDeactivate(this, Spells.GetValueOrDefault<short, Spell>((int)SpellSlotType.PassiveSpellSlot));
            }
            catch (Exception e) 
            { 
                _logger.Error(null, e); 
            }
            finally 
            { 
                ApiEventManager.RemoveAllListenersForOwner(CharScript); 
            }
        }

        public override void OnRemoved()
        {
            foreach (var spell in Spells.Values.ToArray()) spell?.Deactivate();
            DeactivateCharScript();
            foreach (var buff in GetBuffs().ToArray()) RemoveBuff(buff);
            base.OnRemoved();
        }

        private void TryPostActivateCharScript()
        {
            if (_charScriptPostActivated || !_charScriptActivated || CharScript == null)
            {
                return;
            }

            if (!VisibleForPlayers.Any())
            {
                return;
            }

            _charScriptPostActivated = true;
            try
            {
                CharScript.OnPostActivate(
                    this, Spells.GetValueOrDefault<short, Spell>(
                        (int)SpellSlotType.PassiveSpellSlot
                    )
                );
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }
        }

        private void TryPostActivateSpellScripts()
        {
            if (Spells == null || Spells.Count == 0)
            {
                return;
            }

            foreach (var spell in Spells.Values)
            {
                spell?.TryPostActivateScript();
            }
        }

        /// <summary>
        /// Function called by this AI's auto attack projectile when it hits its target.
        /// </summary>
        public virtual void AutoAttackHit(AttackableUnit target)
        {
            if (target == null || target.IsDead)
            {
                return;
            }

            var damage = Stats.AttackDamage.Total;

            // Apply crit BEFORE building damageData, so PostMitigationDamage is correct too
            bool isCrit = IsNextAutoCrit;
            if (isCrit)
            {
                damage *= Stats.CriticalDamage.Total;
            }

            DamageData damageData = new DamageData
            {
                IsAutoAttack = true,
                Attacker = this,
                Target = target,
                Damage = damage,
                PostMitigationDamage = target.Stats.GetPostMitigationDamage(damage, DamageType.DAMAGE_TYPE_PHYSICAL, this),
                DamageSource = DamageSource.DAMAGE_SOURCE_ATTACK,
                DamageType = DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageResultType = isCrit ? DamageResultType.RESULT_CRITICAL : DamageResultType.RESULT_NORMAL
            };

            if (HasBuffType(BuffType.BLIND)
                || _random.NextDouble() < Math.Clamp(Stats.BasicAttackMissChance, 0.0f, 1.0f))
            {
                target.TakeDamage(this, 0, DamageType.DAMAGE_TYPE_PHYSICAL,
                                             DamageSource.DAMAGE_SOURCE_ATTACK,
                                             DamageResultType.RESULT_MISS);
                return;
            }

            target.TakeDamage(damageData, isCrit);
        }

        public override bool CanMove()
        {
            return !IsDead && ((MovementParameters != null)
                || (Status.HasFlag(StatusFlags.CanMove) && Status.HasFlag(StatusFlags.CanMoveEver)
                && (MoveOrder != OrderType.CastSpell && _castingSpell == null)
                && (ChannelSpell == null || (ChannelSpell != null && ChannelSpell.SpellData.CanMoveWhileChanneling))
                && (!IsAttacking || HasAutoAttacked || AutoAttackSpell.State != SpellState.STATE_CASTING
                    || !AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                && !(Status.HasFlag(StatusFlags.Netted)
                || Status.HasFlag(StatusFlags.Rooted)
                || Status.HasFlag(StatusFlags.Sleep)
                || Status.HasFlag(StatusFlags.Stunned)
                || Status.HasFlag(StatusFlags.Suppressed))));
        }

        public override bool CanChangeWaypoints()
        {
            return !IsDead
                && (MovementParameters == null || (MovementParameters != null && MovementParameters.FollowNetID != 0))
                && _castingSpell == null
                && (ChannelSpell == null || (ChannelSpell != null && !ChannelSpell.SpellData.CantCancelWhileChanneling));
        }
        public bool CanIssueMoveOrders()
        {
            if (IsDead)
                return false;

            if (IgnoreMoveOrders)
                return false;

            if (!Status.HasFlag(StatusFlags.CanMoveEver))
                return false;

            if (Status.HasFlag(StatusFlags.Stunned)
                || Status.HasFlag(StatusFlags.Suppressed)
                || Status.HasFlag(StatusFlags.Sleep)
                || Status.HasFlag(StatusFlags.Feared)
                || Status.HasFlag(StatusFlags.Taunted)
                || !Status.HasFlag(StatusFlags.CanMove))
                return false;

            return true;
        }
        /// <summary>
        /// Whether or not this AI is able to auto attack.
        /// </summary>
        /// <returns></returns>
        public bool CanAttack()
        {
            // TODO: Verify if all cases are accounted for.
            return Status.HasFlag(StatusFlags.CanAttack)
                && !Status.HasFlag(StatusFlags.Charmed)
                && !Status.HasFlag(StatusFlags.Disarmed)
                && !Status.HasFlag(StatusFlags.Feared)
                // TODO: Verify
                && !Status.HasFlag(StatusFlags.Pacified)
                && !Status.HasFlag(StatusFlags.Sleep)
                && !Status.HasFlag(StatusFlags.Stunned)
                && !Status.HasFlag(StatusFlags.Suppressed)
                && _castingSpell == null
                && ChannelSpell == null;
        }

        /// <summary>
        /// Whether or not this AI is able to cast spells.
        /// </summary>
        /// <param name="spell">Spell to check.</param>
        /// <param name="ignoreCurrentCast">Ignore the current spell windup when validating buffered input.</param>
        public bool CanCast(Spell spell = null, bool ignoreCurrentCast = false, bool ignoreForcedMovement = false)
        {
            if (!CanCastByStatus(spell, ignoreForcedMovement))
            {
                return false;
            }

            bool instant = spell?.SpellData.Flags.HasFlag(SpellDataFlags.InstantCast) == true;
            return (ignoreCurrentCast || !IsSpellBlockedByWindup(spell))
                && (ChannelSpell == null || spell?.SpellData.DoesntBreakChannels == true
                    || !ChannelSpell.SpellData.CantCancelWhileChanneling)
                && (instant || !IsAttacking || HasAutoAttacked
                    || AutoAttackSpell?.SpellData.CantCancelWhileWindingUp != true)
                && ApiEventManager.OnCanCast.Publish(this, spell);
        }

        private bool IsSpellBlockedByWindup(Spell spell)
        {
            if (_castingSpell == null)
            {
                return false;
            }

            if (spell?.SpellData.Flags.HasFlag(SpellDataFlags.InstantCast) != true)
            {
                return true;
            }

            if (spell.CastInfo.IsAutoAttack
                || spell.CastInfo.SpellSlot >= (byte)SpellSlotType.SummonerSpellSlots)
            {
                return false;
            }

            if (_castingSpell.CastInfo.IsAutoAttack
                || (_castingSpell.CastInfo.SpellSlot >= (byte)SpellSlotType.SummonerSpellSlots
                    && _castingSpell.CastInfo.SpellSlot < (byte)SpellSlotType.ExtraSlots))
            {
                return false;
            }

            if (_castingSpell.State != SpellState.STATE_CASTING
                || _castingSpell.CurrentCastTime <= 0
                || !_castingSpell.SpellData.CantCancelWhileWindingUp)
            {
                return false;
            }

            var incoming = spell.SpellData.ClientCastBehavior;
            var pending = _castingSpell.SpellData.ClientCastBehavior;
            if (incoming == null || pending == null)
            {
                return false;
            }

            // Toggles/recasts and separate spellbooks keep their existing admission paths.
            if (!incoming.InstantCast
                || incoming.BelongsToAvatar
                || incoming.DoesntBreakChannels
                || incoming.IsToggleSpell)
            {
                return false;
            }

            if (pending.InstantCast
                || pending.BelongsToAvatar
                || pending.DoesntBreakChannels
                || pending.IsToggleSpell)
            {
                return false;
            }

            return pending.TriggersGlobalCooldown.HasValue
                && incoming.SubjectToGlobalCooldown.HasValue
                && _castingSpell.SpellData.TriggersGlobalCooldown
                && spell.SpellData.SubjectToGlobalCooldown;
        }

        /// <summary>
        /// Checks spell specific CC permissions without considering active casts or script listeners.
        /// Ignoring forced movement preserves all independently applied buff and base restrictions.
        /// </summary>
        public bool CanCastByStatus(Spell spell = null, bool ignoreForcedMovement = false)
        {
            var data = spell?.SpellData;
            bool allowedByMovement = spell != null
                && MovementParameters?.AllowedSpellSlots?.Contains(spell.CastInfo.SpellSlot) == true;
            var status = ignoreForcedMovement ? GetStatusWithoutForcedMovement() : Status;
            if (allowedByMovement && !ignoreForcedMovement)
            {
                status = (status & ~StatusFlags.CanCast)
                    | (GetStatusWithoutForcedMovement() & StatusFlags.CanCast);
            }
            if (!ignoreForcedMovement && spell != null
                && MovementParameters?.BlockedSpellSlots?.Contains(spell.CastInfo.SpellSlot) == true)
            {
                return false;
            }
            if (IsDead)
            {
                return data?.CanOnlyCastWhileDead == true;
            }
            if (data?.CanOnlyCastWhileDead == true)
            {
                return false;
            }

            if (status.HasFlag(StatusFlags.Suppressed) && data?.CannotBeSuppressed != true)
            {
                return false;
            }

            const StatusFlags castDisables = StatusFlags.Charmed | StatusFlags.Feared
                | StatusFlags.Pacified | StatusFlags.Silenced | StatusFlags.Sleep | StatusFlags.Stunned;
            bool castDisabled = !status.HasFlag(StatusFlags.CanCast) || (status & castDisables) != 0;
            bool taunted = status.HasFlag(StatusFlags.Taunted);

            const StatusFlags moveDisables = StatusFlags.Charmed | StatusFlags.Feared
                | StatusFlags.Immovable | StatusFlags.Netted | StatusFlags.Rooted | StatusFlags.Sleep
                | StatusFlags.Stunned | StatusFlags.Suppressed | StatusFlags.Taunted;
            bool moveDisabled = !status.HasFlag(StatusFlags.CanMove) || !status.HasFlag(StatusFlags.CanMoveEver)
                || (status & moveDisables) != 0;
            if (data?.CantCastWhileRooted == true && moveDisabled)
            {
                return false;
            }

            bool disabled = castDisabled || moveDisabled || taunted
                || !status.HasFlag(StatusFlags.CanAttack)
                || status.HasFlag(StatusFlags.Disarmed) || status.HasFlag(StatusFlags.Suppressed);
            if (data?.CanOnlyCastWhileDisabled == true && !disabled)
            {
                return false;
            }

            return data?.CanCastWhileDisabled == true
                || (!castDisabled && (!taunted || data?.Flags.HasFlag(SpellDataFlags.AllowWhileTaunted) == true));
        }

        public bool CanLevelUpSpell(Spell s)
        {
            return CharData.SpellsUpLevels[s.CastInfo.SpellSlot][s.CastInfo.SpellLevel] <= Stats.Level;
        }

        public virtual bool LevelUp(bool force = true)
        {
            Stats.LevelUp();
            _game.PacketNotifier.NotifyNPC_LevelUp(this);
            //_game.PacketNotifier.NotifyOnReplication(this, partial: false);
            ApiEventManager.OnLevelUp.Publish(this);
            return true;
        }

        /// <summary>
        /// Classifies the given unit. Used for AI attack priority, such as turrets or minions. Known in League internally as "Call for help".
        /// </summary>
        /// <param name="target">Unit to classify.</param>
        /// <returns>Classification for the given unit.</returns>
        /// TODO: Verify if we want to rename this to something which relates more to the internal League name "Call for Help".
        /// TODO: Move to AttackableUnit.
        public ClassifyUnit ClassifyTarget(AttackableUnit target, AttackableUnit victium = null)
        {
            if (target is ObjAIBase ai && victium != null) // If an ally is in distress, target this unit. (Priority 1~5)
            {
                switch (target)
                {
                    // Champion attacking an allied champion
                    case Champion _ when victium is Champion:
                        return ClassifyUnit.CHAMPION_ATTACKING_CHAMPION;
                    // Champion attacking lane minion
                    case Champion _ when victium is LaneMinion:
                        return ClassifyUnit.CHAMPION_ATTACKING_MINION;
                    // Champion attacking minion
                    case Champion _ when victium is Minion:
                        return ClassifyUnit.CHAMPION_ATTACKING_MINION;
                    // Minion attacking an allied champion.
                    case Minion _ when victium is Champion:
                        return ClassifyUnit.MINION_ATTACKING_CHAMPION;
                    case Minion _ when victium is Champion:
                        return ClassifyUnit.MINION_ATTACKING_CHAMPION;
                    // Minion attacking lane minion
                    case Minion _ when victium is LaneMinion:
                        return ClassifyUnit.MINION_ATTACKING_MINION;
                    // Minion attacking minion
                    case Minion _ when victium is Minion:
                        return ClassifyUnit.MINION_ATTACKING_MINION;
                    // Turret attacking lane minion
                    case BaseTurret _ when victium is LaneMinion:
                        return ClassifyUnit.TURRET_ATTACKING_MINION;
                    // Turret attacking minion
                    case BaseTurret _ when victium is Minion:
                        return ClassifyUnit.TURRET_ATTACKING_MINION;
                }
            }

            switch (target)
            {
                case Minion m:
                    if (m.IsLaneMinion)
                    {
                        switch ((m as LaneMinion).MinionSpawnType)
                        {
                            case MinionSpawnType.MINION_TYPE_MELEE:
                                return ClassifyUnit.MELEE_MINION;
                            case MinionSpawnType.MINION_TYPE_CASTER:
                                return ClassifyUnit.CASTER_MINION;
                            case MinionSpawnType.MINION_TYPE_CANNON:
                            case MinionSpawnType.MINION_TYPE_SUPER:
                                return ClassifyUnit.SUPER_OR_CANNON_MINION;
                        }
                    }
                    return ClassifyUnit.MINION;
                case BaseTurret _:
                    return ClassifyUnit.TURRET;
                case Champion _:
                    return ClassifyUnit.CHAMPION;
                case Inhibitor _ when !target.IsDead:
                    return ClassifyUnit.INHIBITOR;
                case Nexus _:
                    return ClassifyUnit.NEXUS;
            }

            return ClassifyUnit.DEFAULT;
        }

        private Movement.NavigationActorState _navigationState;
        public Movement.NavigationActorState NavigationState => _navigationState ??= new();
        private bool _applyingRecoveryPath;
        private const float LaneMinionCollisionRefreshMs = 500.0f;
        private float _collisionCorrectionMs;
        private Vector2 _collisionErrorDisplacement;
        private bool _collisionRefreshPending;

        public void OnMovementSnapshotCreated()
        {
            _collisionCorrectionMs = 0;
            _collisionErrorDisplacement = Vector2.Zero;
            _collisionRefreshPending = false;
        }

        public override bool SetWaypoints(List<Vector2> newWaypoints, bool isForced = false)
        {
            if (!base.SetWaypoints(newWaypoints, isForced)) return false;
            var goal = Waypoints.Last();
            bool changedGoal = !NavigationState.Goal.HasValue
                || Vector2.DistanceSquared(NavigationState.Goal.Value, goal) > 1;
            if (!_applyingRecoveryPath)
            {
                NavigationState.Goal = goal;
                if (changedGoal || this is Champion) NavigationState.NewPath();
                else NavigationState.RepairedPath();
            }
            else NavigationState.RepairedPath();
            NavigationState.Waiting = false;
            OnMovementSnapshotCreated();
            return true;
        }

        public override void StopMovement(MoveStopReason reason = MoveStopReason.CrowdControl, bool networked = true)
        {
            Velocity = Vector2.Zero;
            NavigationState.AcceptedMovement = Vector2.Zero;
            NavigationState.Goal = null;
            NavigationState.Waiting = false;
            NavigationState.Progress();
            base.StopMovement(reason, networked);
        }

        public void RepairCrowdPath()
        {
            if (!CanMove() || MovementParameters != null) return;
            var state = NavigationState;
            if (this is LaneMinion lane && TargetUnit != null && !TargetUnit.IsDead)
            {
                lane.TryUpdateAttackApproach(Stats.Range.Total + TargetUnit.CollisionRadius, recovery: true);
                return;
            }
            if (!state.Goal.HasValue || state.RetrySeconds > 0
                || !_game.MovementManager.TryReservePathSearch(this)) return;
            var path = _game.Map.PathingHandler.GetActorPath(this, state.Goal.Value, allowPartial: true);
            ApplyRecoveryPath(path);
            state.RepathAttempt(this is Champion || this is Pet);
        }

        protected void ApplyRecoveryPath(List<Vector2> path)
        {
            var state = NavigationState;
            _applyingRecoveryPath = true;
            try
            {
                if (path != null && path.Count > 1 && SetWaypoints(path)) state.LastReason = "route repaired";
                else state.LastReason = "waiting for route";
            }
            finally { _applyingRecoveryPath = false; }
        }

        private float RemainingRouteDistance()
        {
            float distance = 0;
            var start = Position;
            for (int i = CurrentWaypointKey; i < Waypoints.Count; i++)
            {
                distance += Vector2.Distance(start, Waypoints[i]);
                start = Waypoints[i];
            }
            return distance;
        }

        public override bool Move(float diff)
        {
            if (!float.IsFinite(diff) || diff <= 0) return false;
            bool moved = false;
            var tickStart = Position;
            float remaining = diff;
            int limit = Waypoints.Count + 1;

            while (remaining > 0.0001f && limit-- > 0)
            {
                int before = CurrentWaypointKey;
                float slice = remaining;
                if (before < Waypoints.Count && GetMoveSpeed() > 0)
                {
                    float distance = Vector2.Distance(Position, Waypoints[before]);
                    if (distance > 0.00001f)
                        slice = Math.Min(slice, distance / GetMoveSpeed() * 1000.0f);
                }
                moved |= MoveSegment(slice);
                remaining -= slice;
                if (CurrentWaypointKey <= before || IsPathEnded()) break;
            }
            Velocity = Position - tickStart;
            NavigationState.LastDeltaSeconds = diff / 1000.0f;
            NavigationState.AcceptedMovement = Velocity;
            _game.Map.CollisionHandler.Actors.Update(this);
            return moved;
        }

        private bool MoveSegment(float diff)
        {
            if (MoveOrder == OrderType.CastSpell || MoveOrder == OrderType.OrderNone
                || MoveOrder == OrderType.Stop || MoveOrder == OrderType.Taunt)
            {
                Velocity = Vector2.Zero;
                return false;
            }
            var state = NavigationState;
            if (IsPathEnded())
            {
                if (state.Goal.HasValue && Vector2.DistanceSquared(Position, state.Goal.Value) > 1)
                    RepairCrowdPath();
                if (IsPathEnded()) { Velocity = Vector2.Zero; return false; }
            }
            if (_game.Map.NavigationGrid.CastCircle(Position, CurrentWaypoint, PathfindingRadius))
            {
                RepairCrowdPath();
                if (_game.Map.NavigationGrid.CastCircle(Position, CurrentWaypoint, PathfindingRadius))
                {
                    state.LastReason = "terrain blocked";
                    if (!state.Waiting) _movementUpdated = true;
                    state.Waiting = true;
                    Velocity = Vector2.Zero;
                    return false;
                }
            }
            var oldPosition = Position;
            int oldWaypointKey = CurrentWaypointKey;
            var desired = BuildDesiredPathMovement(diff, out int idealWaypointKey);
            state.RequestedMovement += desired;
            if (desired.LengthSquared() <= 1e-8f)
            {
                CurrentWaypointKey = idealWaypointKey;
                Velocity = Vector2.Zero;
                if (IsPathEnded())
                {
                    _movementUpdated = true;
                    if (state.Goal.HasValue && Vector2.DistanceSquared(Position, state.Goal.Value) <= 1)
                        state.Goal = null;
                }
                return true;
            }
            float seconds = diff * .001f;
            var movement = this is Champion champion
                ? _game.MovementManager.Champions.Solve(champion, desired, seconds)
                : _game.MovementManager.AiPaths.Solve(this, desired, seconds);
            state.CandidateMovement += movement;
            // All collision and overlap work is already included in this displacement.
            Position = oldPosition + movement;
            Velocity = movement;
            bool waiting = movement.LengthSquared() < .01f;
            if (waiting != state.Waiting)
            {
                _movementUpdated = true;
            }
            state.Waiting = waiting;
            bool followsPath = Vector2.DistanceSquared(movement, desired) <= .0001f;
            if (followsPath)
                CurrentWaypointKey = idealWaypointKey;
            else
            {
                CurrentWaypointKey = oldWaypointKey;
                AdvanceWaypointsFromActualMovement(oldPosition, Position);
            }

            if (!waiting)
            {
                _collisionCorrectionMs += diff;
                _collisionErrorDisplacement += this is LaneMinion
                    ? state.SolverConstraintDelta : movement - desired;
                // Remember even brief steering until a fresh origin is sent.
                // A later straight step or cancelling adjustment must not erase it.
                _collisionRefreshPending |= this is LaneMinion && !followsPath;
            }
            float correctionThreshold = Math.Max(8, PathfindingRadius * .5f);
            bool constraintCorrectionDue = _collisionCorrectionMs >= 250
                && _collisionErrorDisplacement.LengthSquared() >= correctionThreshold * correctionThreshold;
            bool steeringRefreshDue = !waiting && _collisionRefreshPending
                && _collisionCorrectionMs >= LaneMinionCollisionRefreshMs;
            if (constraintCorrectionDue || steeringRefreshDue)
            {
                _movementUpdated = true;
            }
            if (state.ObserveProgress(seconds, RemainingRouteDistance(), GetMoveSpeed()))
            {
                StuckTime += seconds;
                state.LastReason = "recovering blocked route";
                RepairCrowdPath();
            }
            else
            {
                StuckTime = 0;
                state.LastReason = waiting ? "yielding" : "moving";
            }
            if (IsPathEnded())
            {
                _movementUpdated = true;
                if (state.Goal.HasValue && Vector2.DistanceSquared(Position, state.Goal.Value) <= 1)
                    state.Goal = null;
            }
            return movement.LengthSquared() > 0;
        }
        private Vector2 BuildDesiredPathMovement(
    float diff,
    out int resultingWaypointKey)
        {
            resultingWaypointKey =
                CurrentWaypointKey;

            Vector2 simulatedPosition =
                Position;

            float remainingDistance =
                GetMoveSpeed() * 0.001f * diff;

            while (resultingWaypointKey < Waypoints.Count)
            {
                Vector2 waypoint =
                    Waypoints[resultingWaypointKey];

                Vector2 direction =
                    waypoint - simulatedPosition;

                float distance =
                    direction.Length();

                if (distance <= 0.00001f)
                {
                    simulatedPosition = waypoint;
                    resultingWaypointKey++;
                    continue;
                }

                if (remainingDistance < distance)
                {
                    simulatedPosition +=
                        direction / distance
                        * remainingDistance;

                    remainingDistance = 0.0f;
                    break;
                }

                simulatedPosition = waypoint;
                resultingWaypointKey++;
                break;
            }

            return simulatedPosition - Position;
        }

        private void AdvanceWaypointsFromActualMovement(
            Vector2 start,
            Vector2 end)
        {
            float reachRadius =
                Math.Clamp(
                    PathfindingRadius * 0.25f,
                    4.0f,
                    16.0f
                );

            float reachRadiusSq =
                reachRadius * reachRadius;

            while (CurrentWaypointKey < Waypoints.Count)
            {
                Vector2 waypoint =
                    Waypoints[CurrentWaypointKey];

                
                if (Vector2.DistanceSquared(
                        end,
                        waypoint) <= reachRadiusSq)
                {
                    if (CurrentWaypointKey + 1 < Waypoints.Count
                        && _game.Map.NavigationGrid.CastCircle(end,
                            Waypoints[CurrentWaypointKey + 1], PathfindingRadius)) break;
                    // The final destination must actually be reached, not just passed nearby.
                    if (CurrentWaypointKey == Waypoints.Count - 1
                        && Vector2.DistanceSquared(end, waypoint) > 0.25f) break;
                    start = waypoint;
                    CurrentWaypointKey++;
                    continue;
                }

                Vector2 movement =
                    end - start;

                float movementLengthSq =
                    movement.LengthSquared();

                if (movementLengthSq <= 0.00001f)
                {
                    break;
                }

                // Did our actual movement segment pass close enough
                // to this waypoint?
                float t =
                    Vector2.Dot(
                        waypoint - start,
                        movement
                    ) / movementLengthSq;

                t = Math.Clamp(t, 0.0f, 1.0f);

                Vector2 closest =
                    start + movement * t;

                if (Vector2.DistanceSquared(
                        closest,
                        waypoint) <= reachRadiusSq)
                {
                    if (CurrentWaypointKey + 1 < Waypoints.Count
                        && _game.Map.NavigationGrid.CastCircle(end,
                            Waypoints[CurrentWaypointKey + 1], PathfindingRadius)) break;
                    // The final destination must actually be reached, not just passed nearby.
                    if (CurrentWaypointKey == Waypoints.Count - 1
                        && Vector2.DistanceSquared(end, waypoint) > 0.25f) break;
                    start = waypoint;
                    CurrentWaypointKey++;
                    continue;
                }

                break;
            }
        }
        public void UpdatePathMovement(float diff)
        {
            NavigationState.AdvanceTime(diff * .001f);

            if (MovementParameters != null)
            {
                return;
            }

            if (!CanMove())
            {
                Velocity = Vector2.Zero;
                StuckTime = 0.0f;
                return;
            }

            bool moved = Move(diff);

            if (moved)
            {
                UpdateGrassState();
            }
            else
            {
                Velocity = Vector2.Zero;
            }

            UpdateFacing();
        }

        /// <summary>
        /// Cancels any auto attacks this AI is performing and resets the time between the next auto attack if specified.
        /// </summary>
        /// <param name="reset">Whether or not to reset the delay between the next auto attack.</param>
        /// <param name="notifyClient">False when the replacing cast sends its own native handoff packet.</param>
        public void CancelAutoAttack(bool reset, bool fullCancel = false, bool notifyClient = true)
        {
            AutoAttackSpell.SetSpellState(SpellState.STATE_READY);
            if (reset)
            {
                _autoAttackCurrentCooldown = 0;
                AutoAttackSpell.ResetSpellCast();
            }

            if (fullCancel)
            {
                IsAttacking = false;
                HasMadeInitialAttack = false;
            }
            // The client stop packet also stops non-attack spells in this book.
            // Resetting an attack must preserve an unrelated windup or channel.
            if (notifyClient
                && (reset || fullCancel)
                && _castingSpell == null
                && ChannelSpell == null)
            {
                _game.PacketNotifier.NotifyNPC_InstantStop_Attack(this, false);
            }
        }

        /// <summary>
        /// Forces this AI unit to perform a dash which follows the specified AttackableUnit.
        /// </summary>
        /// <param name="target">Unit to follow.</param>
        /// <param name="dashSpeed">Constant speed that the unit will have during the dash.</param>
        /// <param name="animation">Internal name of the dash animation.</param>
        /// <param name="leapGravity">How much gravity the unit will experience when above the ground while dashing.</param>
        /// <param name="keepFacingLastDirection">Whether or not the unit should maintain the direction they were facing before dashing.</param>
        /// <param name="followTargetMaxDistance">Maximum distance the unit will follow the Target before stopping the dash or reaching to the Target.</param>
        /// <param name="backDistance">Unknown parameter.</param>
        /// <param name="travelTime">Total time (in seconds) the dash will follow the GameObject before stopping or reaching the Target.</param>
        /// <param name="consideredCC">Whether or not to prevent movement, casting, or attacking during the duration of the movement.</param>
        /// TODO: Implement Dash class which houses these parameters, then have that as the only parameter to this function (and other Dash-based functions).
        public void DashToTarget
       (
           AttackableUnit target,
           float dashSpeed,
           string animation = "",
           float leapGravity = 0,
           bool keepFacingLastDirection = true,
           float followTargetMaxDistance = 0,
           float backDistance = 0,
           float travelTime = 0,
           bool consideredCC = true,
           string movementName = "",
           AttackableUnit caster = null,
           ForceMovementOrdersType movementOrdersType = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER
       )
        {
            var parameters = new ForceMovementParameters
            {
                FollowNetID = target.NetId,
                PathSpeedOverride = dashSpeed,
                Animation = animation,
                OverrideRunAnimation = true,
                ParabolicGravity = leapGravity,
                KeepFacingDirection = keepFacingLastDirection,
                FollowDistance = followTargetMaxDistance,
                FollowBackDistance = backDistance,
                FollowTravelTime = travelTime,
                MovementName = movementName,
                Caster = caster ?? this,
                MovementOrdersType = movementOrdersType,
                SetStatus = consideredCC ? (StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove) : StatusFlags.None
            };

            StartForcedMovement(parameters);
            if (MovementParameters == parameters && movementOrdersType != ForceMovementOrdersType.CANCEL_ORDER)
            {
                SetTargetUnit(target, true);
            }
        }

        /// <summary>
        /// Forces this AI unit to perform a lunge which follows the specified AttackableUnit.
        /// </summary>
        public void LungeToTarget
        (
            AttackableUnit target,
            float speed,
            string animation = "",
            float leapGravity = 0,
            bool keepFacingLastDirection = true,
            float followTargetMaxDistance = 0,
            float backDistance = 0,
            float travelTime = 0,
            bool consideredCc = false,
            ForceMovementType movementType = ForceMovementType.FURTHEST_WITHIN_RANGE
        )
        {
            DashToTarget(
                target,
                speed,
                animation,
                leapGravity,
                keepFacingLastDirection,
                followTargetMaxDistance,
                backDistance,
                travelTime,
                consideredCc
            );
        }


        public virtual void RefreshWaypoints(float idealRange)
        {
            if (MovementParameters != null || !CanMove())
            {
                return;
            }

            if (TargetUnit != null && _castingSpell == null && ChannelSpell == null && MoveOrder != OrderType.AttackTo)
            {
                UpdateMoveOrder(OrderType.AttackTo, true);
            }

            if (SpellToCast != null)
            {
                // Spell casts usually do not take into account collision radius, thus range is center -> center VS edge -> edge for attacks.
                idealRange = SpellToCast.GetCurrentCastRange();
            }

            Vector2 targetPos = Vector2.Zero;

            if (MoveOrder == OrderType.AttackTo
               && TargetUnit != null
               && !TargetUnit.IsDead)
            {
                targetPos = TargetUnit.Position;
            }

            if (MoveOrder == OrderType.AttackMove
                 || MoveOrder == OrderType.AttackTerrainOnce
                 || MoveOrder == OrderType.AttackTerrainSustained
                 && !IsPathEnded())
            {
                targetPos = Waypoints.LastOrDefault();

                if (targetPos == Vector2.Zero)
                {
                    // Neither AttackTo nor AttackMove (etc.) were successful.
                    return;
                }
            }

            // If the target is already in range, stay where we are.
            if (MoveOrder == OrderType.AttackMove
                && targetPos != Vector2.Zero
                && MovementParameters == null
                && Vector2.DistanceSquared(Position, targetPos) <= idealRange * idealRange
                && _autoAttackCurrentCooldown <= 0)
            {
                UpdateMoveOrder(OrderType.Stop, true);
            }
            // No TargetUnit
            else if (targetPos == Vector2.Zero)
            {
                return;
            }

            if (MoveOrder == OrderType.AttackTo && targetPos != Vector2.Zero)
            {
                if (KeepChasingDuringAttackCooldown
                    && IsMelee
                    && SpellToCast == null
                    && _autoAttackCurrentCooldown > 0
                    && TargetUnit is ObjAIBase movingTarget
                    && !movingTarget.IsPathEnded())
                {
                    idealRange = Math.Min(idealRange, CollisionRadius + movingTarget.CollisionRadius);
                }

                if (Vector2.DistanceSquared(Position, targetPos) <= idealRange * idealRange)
                {
                    bool isReadyToAttack = CanAttack() &&
                    _autoAttackCurrentCooldown <= 0 &&
                    AutoAttackSpell != null &&
                    AutoAttackSpell.State == SpellState.STATE_READY;

                    if (!IsPathEnded()) StopMovement(networked: !isReadyToAttack);
                }
                else
                {
                    Vector2 pathDestination = targetPos;

                    if (!_game.Map.PathingHandler.IsWalkable(pathDestination, PathfindingRadius))
                    {
                        pathDestination = _game.Map.NavigationGrid.GetClosestTerrainExit(pathDestination, PathfindingRadius);
                    }

                    bool needsNewPath = true;
                    if (Waypoints != null && !IsPathEnded())
                    {
                        Vector2 currentDestination = Waypoints.Last();
                        if (Vector2.DistanceSquared(currentDestination, pathDestination) < 50f * 50f)
                        {
                            needsNewPath = false;
                        }
                    }

                    if (needsNewPath)
                    {
                        var newWaypoints = _game.Map.PathingHandler.GetPath(Position, pathDestination, PathfindingRadius);
                        if (newWaypoints != null && newWaypoints.Count > 1)
                        {
                            SetWaypoints(newWaypoints);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a random auto attack spell from the list of auto attacks available for this AI.
        /// Will only select crit auto attacks if the next auto attack is going to be a crit, otherwise normal auto attacks will be selected.
        /// </summary>
        /// <returns>Random auto attack spell.</returns>
        public Spell GetNewAutoAttack()
        {
            var selected = CharScript?.SelectAutoAttack(this, IsNextAutoCrit);
            if (selected != null && selected.CastInfo.Owner == this && selected.CastInfo.IsAutoAttack
                && Spells.TryGetValue(selected.CastInfo.SpellSlot, out var owned) && owned == selected)
            {
                _lastAutoAttack = selected;
                return selected;
            }
            var candidates = new List<(Spell spell, float weight)>();

            if (IsNextAutoCrit)
            {
                for (short slot = (short)BasicAttackTypes.BASICATTACK_CRITICAL_SLOT1; slot <= (short)BasicAttackTypes.BASICATTACK_CRITICAL_LAST_SLOT; slot++)
                {
                    var idx = slot - 64;
                    if (idx < 0 || idx >= CharData.BasicAttacks.Count)
                    {
                        continue;
                    }

                    var prob = CharData.BasicAttacks[idx].Probability;
                    if (prob <= 0.0f)
                    {
                        continue;
                    }

                    if (Spells.TryGetValue(slot, out var spell) && spell != null)
                    {
                        candidates.Add((spell, prob));
                    }
                }
            }
            else
            {
                for (short slot = (short)SpellSlotType.BasicAttackNormalSlots; slot <= (short)BasicAttackTypes.BASICATTACK_NORMAL_LAST_SLOT; slot++)
                {
                    var idx = slot - 64;
                    if (idx < 0 || idx >= CharData.BasicAttacks.Count)
                    {
                        continue;
                    }

                    var prob = CharData.BasicAttacks[idx].Probability;
                    if (prob <= 0.0f)
                    {
                        continue;
                    }

                    if (Spells.TryGetValue(slot, out var spell) && spell != null)
                    {
                        candidates.Add((spell, prob));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                var first = IsNextAutoCrit
                    ? (short)BasicAttackTypes.BASICATTACK_CRITICAL_SLOT1
                    : (short)BasicAttackTypes.BASIC_ATTACK_TYPES_FIRST_SLOT;

                _lastAutoAttack = Spells[first];
                return _lastAutoAttack;
            }

            const int maxRerolls = 3;
            for (var attempt = 0; attempt <= maxRerolls; attempt++)
            {
                var chosen = WeightedPick(candidates);
                if (chosen != _lastAutoAttack || candidates.Count == 1 || attempt == maxRerolls)
                {
                    _lastAutoAttack = chosen;
                    return chosen;
                }
            }

            _lastAutoAttack = candidates[0].spell;
            return _lastAutoAttack;
        }

        private float GetAutoAttackProbabilityWeight(Spell spell)
        {
            if (spell == null)
            {
                return 0.0f;
            }

            var idx = spell.CastInfo.SpellSlot - (short)SpellSlotType.BasicAttackNormalSlots;
            if (idx >= 0 && idx < CharData.BasicAttacks.Count)
            {
                var configuredWeight = CharData.BasicAttacks[idx].Probability;
                if (configuredWeight > 0.0f)
                {
                    return configuredWeight;
                }
            }

            return 1.0f;
        }

        private float GetOverrideAutoAttackProbabilityWeight(Spell spell)
        {
            if (spell == null)
            {
                return 0.0f;
            }

            if (_autoAttackOverrideWeights.TryGetValue(spell, out var explicitWeight))
            {
                return explicitWeight;
            }

            return GetAutoAttackProbabilityWeight(spell);
        }

        private Spell GetNewOverrideAutoAttack()
        {
            if (_autoAttackOverrideSpells.Count == 0)
            {
                return GetNewAutoAttack();
            }

            if (_autoAttackOverrideSpells.Count == 1)
            {
                _lastOverrideAutoAttack = _autoAttackOverrideSpells[0];
                return _lastOverrideAutoAttack;
            }

            var candidates = new List<(Spell spell, float weight)>(_autoAttackOverrideSpells.Count);
            foreach (var overrideSpell in _autoAttackOverrideSpells)
            {
                if (overrideSpell == null)
                {
                    continue;
                }

                var weight = GetOverrideAutoAttackProbabilityWeight(overrideSpell);
                if (weight <= 0.0f)
                {
                    continue;
                }

                candidates.Add((overrideSpell, weight));
            }

            if (candidates.Count == 0)
            {
                return GetNewAutoAttack();
            }

            const int maxRerolls = 3;
            for (var attempt = 0; attempt <= maxRerolls; attempt++)
            {
                var chosen = WeightedPick(candidates);
                if (chosen != _lastOverrideAutoAttack || candidates.Count == 1 || attempt == maxRerolls)
                {
                    _lastOverrideAutoAttack = chosen;
                    return chosen;
                }
            }

            _lastOverrideAutoAttack = candidates[0].spell;
            return _lastOverrideAutoAttack;
        }

        private Spell GetNextOverriddenAutoAttackForCast(bool isCritAttack)
        {
            if (isCritAttack && _autoAttackOverrideCritSpell != null)
            {
                return _autoAttackOverrideCritSpell;
            }

            if (_autoAttackOverrideSpells.Count > 1)
            {
                return GetNewOverrideAutoAttack();
            }

            if (_autoAttackOverrideSpells.Count == 1)
            {
                return _autoAttackOverrideSpells[0];
            }

            return GetNewAutoAttack();
        }

        private Spell WeightedPick(List<(Spell spell, float weight)> candidates)
        {
            float total = 0.0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                total += candidates[i].weight;
            }

            if (total <= 0.0f)
            {
                return candidates[0].spell;
            }

            var roll = (float)(_random.NextDouble() * total);
            float acc = 0.0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                acc += candidates[i].weight;
                if (roll <= acc)
                {
                    return candidates[i].spell;
                }
            }

            return candidates[candidates.Count - 1].spell;
        }

        private void PrepareAutoAttackSpellForCast(Spell spell)
        {
            if (spell == null)
            {
                return;
            }

            if (spell.State != SpellState.STATE_CASTING && spell.State != SpellState.STATE_CHANNELING)
            {
                spell.ResetSpellCast();
                spell.CastInfo.Targets.Clear();
            }

            spell.CastInfo.IsSecondAutoAttack = HasMadeInitialAttack;
        }

        private float GetAutoAttackCooldownSeconds(Spell spell)
        {
            float baseAttackSpeed = Math.Max(0.0001f, Stats.GetTotalAttackSpeed());
            float cooldown = 1.0f / baseAttackSpeed;

            if (spell == null || !spell.CastInfo.IsAutoAttack)
            {
                return cooldown;
            }

            if (spell.CastInfo.SpellSlot >= (byte)SpellSlotType.BasicAttackNormalSlots)
                // Windup/animation timing must not determine attack cadence.
                // Use the same authoritative attack speed exposed by Stats.
                return cooldown;

            // Auto-attack overrides in non-basic slots can have longer cycle times.
            if (spell.CastInfo.SpellSlot < (byte)SpellSlotType.BasicAttackNormalSlots)
            {
                float attackSpeedModifier = Math.Max(0.0001f, spell.CastInfo.AttackSpeedModifier);
                float overrideCycleTime = spell.CastInfo.DesignerTotalTime / attackSpeedModifier;
                if (overrideCycleTime > cooldown)
                {
                    cooldown = overrideCycleTime;
                }
            }

            return cooldown;
        }

        public Spell GetAutoAttackSpell(string name)
        {
            foreach (var spell in Spells.Values)
            {
                if (spell == null)
                {
                    continue;
                }

                if (spell.CastInfo.IsAutoAttack && spell.SpellName == name)
                {
                    return spell;
                }
            }

            return null;
        }

        public Spell GetSpell(string name)
        {
            foreach (var s in Spells.Values)
            {
                if (s == null)
                {
                    continue;
                }

                if (s.SpellName == name)
                {
                    return s;
                }
            }

            return null;
        }

        public virtual Spell LevelUpSpell(byte slot)
        {
            var s = Spells[slot];

            if (s == null || !CanLevelUpSpell(s))
            {
                //Don't know what problems it might cause in the future but making a mental note for now for karma r
                //return null;
            }

            s.LevelUp();
            ApiEventManager.OnLevelUpSpell.Publish(s);
            return s;
        }

        /// <summary>
        /// Removes the spell instance from the given slot (replaces it with an empty BaseSpell).
        /// </summary>
        /// <param name="slot">Byte slot of the spell to remove.</param>
        public void RemoveSpell(byte slot)
        {
            if (Spells[slot].CastInfo.IsAutoAttack)
            {
                return;
            }
            // Verify if we want to support removal/re-addition of character scripts.
            //Removes normal Spells
            else
            {
                Spells[slot].Deactivate();
            }
            Spells[slot] = new Spell(_game, this, "BaseSpell", slot); // Replace previous spell with empty spell.
            SetSpellInputEnabled(slot, false);
        }

        public Spell OverrideBasicAttackSlot(byte slot, string newSpellName)
        {
            var old = Spells.TryGetValue(slot, out var existing) ? existing : null;
            Spells[slot] = new Spell(_game, this, newSpellName, slot, _scriptsEnabled);
            return old;
        }

        public void SetAutoAttackOverride(bool overridden)
        {
            IsAutoAttackOverridden = overridden;
            if (!overridden)
            {
                _autoAttackOverrideSpells.Clear();
                _autoAttackOverrideWeights.Clear();
                _autoAttackOverrideCritSpell = null;
                _lastOverrideAutoAttack = null;
            }
        }

        public void RestoreBasicAttackSlot(byte slot, Spell previous)
        {
            if (previous == null)
            {
                return;
            }

            Spells[slot] = previous;
        }

        /// <summary>
        /// Sets this AI's current auto attack to their base auto attack.
        /// </summary>
        public void ResetAutoAttackSpell()
        {
            _autoAttackOverrideSpells.Clear();
            _autoAttackOverrideWeights.Clear();
            _autoAttackOverrideCritSpell = null;
            _lastOverrideAutoAttack = null;
            IsAutoAttackOverridden = false;
            AutoAttackSpell = GetNewAutoAttack();
            PrepareAutoAttackSpellForCast(AutoAttackSpell);
        }

        /// <summary>
        /// Sets this unit's auto attack spell that they will use when in range of their target (unless they are going to cast a spell first).
        /// </summary>
        /// <param name="spell">Spell instance to set.</param>
        /// <param name="isReset">Whether or not setting this spell causes auto attacks to be reset (cooldown).</param>
        public void SetAutoAttackSpell(Spell spell, bool isReset)
        {
            if (spell == null)
            {
                return;
            }

            _autoAttackOverrideSpells.Clear();
            _autoAttackOverrideWeights.Clear();
            _autoAttackOverrideCritSpell = null;
            AutoAttackSpell = spell;
            _autoAttackOverrideSpells.Add(AutoAttackSpell);
            _lastOverrideAutoAttack = AutoAttackSpell;
            PrepareAutoAttackSpellForCast(AutoAttackSpell);
            IsAutoAttackOverridden = true;

            if (isReset)
            {
                CancelAutoAttack(true);
            }
        }

        /// <summary>
        /// Sets this unit's auto attack spell that they will use when in range of their target (unless they are going to cast a spell first).
        /// </summary>
        /// <param name="name">Internal name of the spell to set.</param>
        /// <param name="isReset">Whether or not setting this spell causes auto attacks to be reset (cooldown).</param>
        /// <returns>Spell set.</returns>
        public Spell SetAutoAttackSpell(string name, bool isReset)
        {
            AutoAttackSpell = GetAutoAttackSpell(name) ?? GetSpell(name);
            if (AutoAttackSpell == null)
            {
                return null;
            }

            _autoAttackOverrideSpells.Clear();
            _autoAttackOverrideWeights.Clear();
            _autoAttackOverrideCritSpell = null;
            _autoAttackOverrideSpells.Add(AutoAttackSpell);
            _lastOverrideAutoAttack = AutoAttackSpell;
            PrepareAutoAttackSpellForCast(AutoAttackSpell);
            IsAutoAttackOverridden = true;
            if (isReset)
            {
                CancelAutoAttack(true);
            }

            return AutoAttackSpell;
        }

        public Spell SetAutoAttackSpells(bool isReset, params string[] names)
        {
            _autoAttackOverrideSpells.Clear();
            _autoAttackOverrideWeights.Clear();
            _autoAttackOverrideCritSpell = null;
            _lastOverrideAutoAttack = null;

            if (names == null || names.Length == 0)
            {
                return null;
            }

            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var overrideSpell = GetAutoAttackSpell(name) ?? GetSpell(name);
                if (overrideSpell == null || _autoAttackOverrideSpells.Contains(overrideSpell))
                {
                    continue;
                }

                _autoAttackOverrideSpells.Add(overrideSpell);
            }

            if (_autoAttackOverrideSpells.Count == 0)
            {
                return null;
            }

            AutoAttackSpell = GetNewOverrideAutoAttack();
            PrepareAutoAttackSpellForCast(AutoAttackSpell);
            IsAutoAttackOverridden = true;
            if (isReset)
            {
                CancelAutoAttack(true);
            }

            return AutoAttackSpell;
        }

        public Spell SetAutoAttackSpells(bool isReset, params (string name, float weight)[] weightedNames)
        {
            _autoAttackOverrideSpells.Clear();
            _autoAttackOverrideWeights.Clear();
            _autoAttackOverrideCritSpell = null;
            _lastOverrideAutoAttack = null;

            if (weightedNames == null || weightedNames.Length == 0)
            {
                return null;
            }

            foreach (var weightedName in weightedNames)
            {
                if (string.IsNullOrEmpty(weightedName.name) || weightedName.weight <= 0.0f)
                {
                    continue;
                }

                var overrideSpell = GetAutoAttackSpell(weightedName.name) ?? GetSpell(weightedName.name);
                if (overrideSpell == null || _autoAttackOverrideSpells.Contains(overrideSpell))
                {
                    continue;
                }

                _autoAttackOverrideSpells.Add(overrideSpell);
                _autoAttackOverrideWeights[overrideSpell] = weightedName.weight;
            }

            if (_autoAttackOverrideSpells.Count == 0)
            {
                return null;
            }

            AutoAttackSpell = GetNewOverrideAutoAttack();
            PrepareAutoAttackSpellForCast(AutoAttackSpell);
            IsAutoAttackOverridden = true;
            if (isReset)
            {
                CancelAutoAttack(true);
            }

            return AutoAttackSpell;
        }

        public Spell SetAutoAttackSpellWithCrit(string normalName, string critName, bool isReset)
        {
            var selected = SetAutoAttackSpell(normalName, isReset);
            if (selected == null)
            {
                return null;
            }

            _autoAttackOverrideCritSpell = GetAutoAttackSpell(critName) ?? GetSpell(critName);
            return selected;
        }

        public Spell SetAutoAttackSpellsWithCrit(bool isReset, string critName, params string[] names)
        {
            var selected = SetAutoAttackSpells(isReset, names);
            if (selected == null)
            {
                return null;
            }

            _autoAttackOverrideCritSpell = GetAutoAttackSpell(critName) ?? GetSpell(critName);
            return selected;
        }

        public Spell SetAutoAttackSpellsWithCrit(bool isReset, string critName, params (string name, float weight)[] weightedNames)
        {
            var selected = SetAutoAttackSpells(isReset, weightedNames);
            if (selected == null)
            {
                return null;
            }

            _autoAttackOverrideCritSpell = GetAutoAttackSpell(critName) ?? GetSpell(critName);
            return selected;
        }

        /// <summary>
        /// Forces this AI to skip its next auto attack. Usually used when spells intend to override the next auto attack with another spell.
        /// </summary>
        public void SkipNextAutoAttack()
        {
            _skipNextAutoAttack = true;
        }

        /// <summary>
        /// Sets the spell for the given slot to a new spell of the given name.
        /// </summary>
        /// <param name="name">Internal name of the spell to set.</param>
        /// <param name="slot">Slot of the spell to replace.</param>
        /// <param name="enabled">Whether or not the new spell should be enabled.</param>
        /// <param name="networkOld">Whether or not to notify clients of this change using an older packet method.</param>
        /// <param name="networked">Whether to send spell-name changes. Item purchases install their client spells through BuyItemAns.</param>
        /// <returns>Newly created spell set.</returns>
        public Spell SetSpell(string name, byte slot, bool enabled, bool networkOld = false, bool networked = true)
        {
            if (!Spells.ContainsKey(slot) || Spells[slot].CastInfo.IsAutoAttack)
            {
                return null;
            }

            var toReturn = Spells[slot];
            var oldSpell = Spells[slot];
            var oldCurrentCooldown = oldSpell?.CurrentCooldown ?? 0.0f;
            var oldTotalCooldown = oldSpell?.CooldownTotalTime ?? 0.0f;
            bool replaced = name != oldSpell.SpellName;

            if (replaced)
            {
                var oldLevel = oldSpell?.CastInfo.SpellLevel ?? (byte)0;

                if (oldSpell != null)
                {
                    oldSpell.Deactivate();
                }

                toReturn = new Spell(_game, this, name, slot);
                toReturn.SetLevel(oldLevel);

                toReturn.ToolTipData.CopyFrom(oldSpell.ToolTipData);
                if (this is Champion tipOwner && toReturn.ToolTipData.Changed)
                    tipOwner.AddToolTipChange(toReturn.ToolTipData);

                Spells[slot] = toReturn;
            }
            SetSpellInputEnabled(slot, enabled);

            if (networked && this is Champion champion)
            {
                int userId = _game.PlayerManager.GetClientInfoByChampion(champion).ClientId;
                // TODO: Verify if this is all that is needed.
                _game.PacketNotifier.NotifyChangeSlotSpellData(userId, champion, slot, ChangeSlotSpellDataType.SpellName, slot == 4 || slot == 5, newName: name);
                if (networkOld)
                {
                    _game.PacketNotifier.NotifyS2C_SetSpellData(userId, NetId, name, slot);
                }
            }

            if (replaced && oldCurrentCooldown > 0.0f)
                toReturn.SetCooldown(oldCurrentCooldown, true, oldTotalCooldown);
            return toReturn;
        }

        private void SetSpellInputEnabled(byte slot, bool enabled)
        {
            if (slot == 4 || slot == 5) Stats.SetSummonerSpellEnabled((byte)(slot - 4), enabled);
            else Stats.SetSpellEnabled(slot, enabled);
        }

        private bool GetSpellInputEnabled(byte slot) => slot == 4 || slot == 5
            ? Stats.GetSummonerSpellEnabled((byte)(slot - 4)) : Stats.GetSpellEnabled(slot);

        /// <summary>
        /// Sets the spell that this unit will cast when it gets in range of the spell's target.
        /// Overrides auto attack spell casting.
        /// </summary>
        /// <param name="s">Spell that will be cast.</param>
        /// <param name="location">Location to cast the spell on. May set to Vector2.Zero if unit parameter is used.</param>
        /// <param name="unit">Unit to cast the spell on.</param>
        public void SetSpellToCast(Spell s, Vector2 location, AttackableUnit unit = null)
        {
            SpellToCast = s;
            _spellToCastOnSuccess = null;
            _spellToCastIsPlayerInput = false;

            if (s == null)
            {
                return;
            }

            if (location != Vector2.Zero)
            {
                var exit = _game.Map.NavigationGrid.GetClosestTerrainExit(location, PathfindingRadius);
                var path = _game.Map.PathingHandler.GetPath(Position, exit, PathfindingRadius);

                if (path != null)
                {
                    SetWaypoints(path);
                }
                else
                {
                    SetWaypoints(new List<Vector2> { Position, exit });
                }

                UpdateMoveOrder(OrderType.MoveTo, true);
            }

            if (unit != null)
            {
                // Unit targeted.
                SetTargetUnit(unit, true);
                UpdateMoveOrder(OrderType.AttackTo, true);
            }
            else
            {
                SetTargetUnit(null, true);
            }
        }

        /// <summary>
        /// Sets the spell this unit is currently casting.
        /// Buffered input is released after cast lifecycle callbacks finish, during Update.
        /// </summary>
        /// <param name="s">Spell that is being cast, or null when the cast ends.</param>
        public void SetCastSpell(Spell s)
        {
            _castingSpell = s;
        }

        /// <summary>
        /// Gets the spell this unit is currently casting.
        /// </summary>
        /// <returns>Spell that is being cast.</returns>
        public Spell GetCastSpell()
        {
            return _castingSpell;
        }

        /// <summary>
        /// Buffers input blocked by a spell windup or an opted in forced movement.
        /// CC permissions, resources, slot identity and script conditions still apply.
        /// Newer input overwrites older input (one-slot buffer).
        /// </summary>
        /// <param name="s">Spell to queue.</param>
        /// <param name="start">Start position of the cast.</param>
        /// <param name="end">End position of the cast.</param>
        /// <param name="unit">Target unit, if any.</param>
        /// <returns>True if the spell was successfully queued; false if conditions don't allow buffering.</returns>
        public bool TryQueueSpell(Spell s, Vector2 start, Vector2 end, AttackableUnit unit, Action onSuccess = null)
        {
            if (!IsSpellReadyForInput(s) || !IsValidSpellInput(s, start, end, unit))
            {
                return false;
            }
            bool movementBlocks = MovementParameters != null && !CanCastByStatus(s)
                && CanCastByStatus(s, ignoreForcedMovement: true);
            bool castBlocks = IsSpellBlockedByWindup(s);
            if ((!movementBlocks && !castBlocks)
                || (movementBlocks && !MovementParameters.BufferSpellCasts)
                || !CanCast(s, ignoreCurrentCast: true, ignoreForcedMovement: movementBlocks))
            {
                return false;
            }
            float window = movementBlocks ? MovementParameters.SpellBufferWindow : 0.5f;
            if (!float.IsFinite(window) || window <= 0) return false;
            _queuedSpellCast = new SpellQueueEntry(s, start, end, unit,
                _game.GameTime + window * 1000, movementBlocks ? MovementParameters : null, onSuccess);
            return true;
        }

        /// <summary>Validates input readiness without changing spell or targeting state.</summary>
        public bool IsSpellReadyForInput(Spell spell)
        {
            if (spell?.CastInfo == null || spell.CastInfo.Owner != this || spell.CastInfo.IsAutoAttack
                || spell.CastInfo.SpellSlot >= (byte)SpellSlotType.PassiveSpellSlot
                || !Spells.TryGetValue(spell.CastInfo.SpellSlot, out var current) || current != spell
                // Items and recall use rank zero, only champion abilities need a learned rank.
                || (spell.CastInfo.SpellSlot < (byte)SpellSlotType.SummonerSpellSlots && spell.CastInfo.SpellLevel == 0)
                || spell.State != SpellState.STATE_READY
                || spell.CurrentCooldown > 0 || spell.CurrentAmmo <= 0)
            {
                return false;
            }
            byte slot = spell.CastInfo.SpellSlot;
            bool enabled = GetSpellInputEnabled(slot);
            int level = spell.CastInfo.SpellLevel;
            return enabled && level < spell.SpellData.ManaCost.Length
                && (_game?.Config?.GameFeatures.HasFlag(FeatureFlags.EnableManaCosts) != true
                    || spell.GetManaCost(level) * (1 - Stats.SpellCostReduction) <= Stats.CurrentMana);
        }

        private bool IsValidSpellInput(Spell spell, Vector2 start, Vector2 end, AttackableUnit unit)
        {
            return float.IsFinite(start.X) && float.IsFinite(start.Y)
                && float.IsFinite(end.X) && float.IsFinite(end.Y)
                && (unit != null || spell.SpellData.TargetingType != TargetingType.Target)
                && (unit == null || unit == this || (!unit.IsToRemove()
                    && spell.SpellData.IsValidTarget(this, unit)));
        }

        /// <summary>Accepts a player request, with one completion callback for actual execution.</summary>
        public bool TryCastRequestedSpell(Spell spell, Vector2 start, Vector2 end, AttackableUnit unit = null, Action onSuccess = null)
        {
            // A newer cast input supersedes an older deferred request even if it is rejected.
            ClearQueuedSpell();
            SetSpellToCast(null, Vector2.Zero);
            if (!IsSpellReadyForInput(spell) || !IsValidSpellInput(spell, start, end, unit)) return false;
            if (!CanCast(spell))
            {
                TryQueueSpell(spell, start, end, unit, onSuccess);
                return false;
            }
            return ExecuteRequestedSpell(spell, start, end, unit, onSuccess);
        }

        private bool ExecuteRequestedSpell(Spell spell, Vector2 start, Vector2 end, AttackableUnit unit, Action onSuccess)
        {
            if (spell.Cast(start, end, unit))
            {
                onSuccess?.Invoke();
                return true;
            }
            if (SpellToCast == spell)
            {
                _spellToCastOnSuccess = onSuccess;
                _spellToCastIsPlayerInput = true;
                _spellToCastInputSlot = spell.CastInfo.SpellSlot;
            }
            return false;
        }

        /// <summary>Revalidates and executes a buffered input once its temporary locks finish.</summary>
        public void FlushQueuedSpell()
        {
            var queued = _queuedSpellCast;
            if (queued == null) return;
            if (_game.GameTime > queued.ExpiresAt || IsDead || !IsSpellReadyForInput(queued.Spell)
                || queued.Spell.CastInfo.SpellSlot != queued.SpellSlot
                || !IsValidSpellInput(queued.Spell, queued.Start, queued.End, queued.TargetUnit))
            {
                ClearQueuedSpell();
                return;
            }
            if (queued.AwaitedMovement != null)
            {
                if (MovementParameters == queued.AwaitedMovement) return;
                ClearQueuedSpell();
                return;
            }
            if (IsSpellBlockedByWindup(queued.Spell))
            {
                return;
            }
            ClearQueuedSpell();
            if (CanCast(queued.Spell))
                ExecuteRequestedSpell(queued.Spell, queued.Start, queued.End, queued.TargetUnit, queued.OnSuccess);
        }

        /// <summary>Called after landing scripts; interrupted or replaced movements discard their input.</summary>
        public void OnForcedMovementEnded(ForceMovementParameters parameters, MoveStopReason reason)
        {
            if (reason == MoveStopReason.Finished && MovementParameters == null)
            {
                _movementOrderToResume = parameters;
                ResumePostponedMovementOrder();
            }
            if (_queuedSpellCast?.AwaitedMovement != parameters) return;
            if (reason != MoveStopReason.Finished || MovementParameters != null)
            {
                ClearQueuedSpell();
                return;
            }
            _queuedSpellCast.AwaitedMovement = null;
            // During Update the elapsed frame belongs to existing spells, release after their updates.
            if (!_updatingMovement) FlushQueuedSpell();
        }

        internal void PrepareForcedMovementOrder(ForceMovementParameters parameters, ForceMovementParameters previous)
        {
            _movementOrderToResume = null;
            if (parameters.MovementOrdersType == ForceMovementOrdersType.CANCEL_ORDER)
            {
                ClearQueuedSpell();
                SetSpellToCast(null, Vector2.Zero);
                SetTargetUnit(null, true);
                MoveOrder = OrderType.Stop;
                _moveOrderRevision++;
                return;
            }
            parameters.PostponedOrderRevision = _moveOrderRevision;
            if (TargetUnit == null && (MoveOrder == OrderType.MoveTo || MoveOrder == OrderType.AttackMove))
            {
                if (!IsPathEnded()) parameters.PostponedDestination = Waypoints.Last();
                else if (previous?.PostponedOrderRevision == _moveOrderRevision)
                    parameters.PostponedDestination = previous.PostponedDestination;
            }
        }

        internal void ResumePostponedMovementOrder()
        {
            var parameters = _movementOrderToResume;
            if (parameters?.PostponedDestination == null) 
            { 
                _movementOrderToResume = null; return; 
            }
            if (parameters.PostponedOrderRevision != _moveOrderRevision || IsDead || TargetUnit != null
                || (MoveOrder != OrderType.MoveTo && MoveOrder != OrderType.AttackMove)
                || MovementParameters != null || !IsPathEnded())
            {
                _movementOrderToResume = null;
                return;
            }
            if (!CanMove() || !CanChangeWaypoints()) return;
            SetPathTrueEnd(parameters.PostponedDestination.Value);
            _movementOrderToResume = null;
        }

        /// <summary>
        /// Discards any buffered spell cast.
        /// Call this on move orders, CC application, death, or any state that should cancel buffered input.
        /// </summary>
        public void ClearQueuedSpell()
        {
            _queuedSpellCast = null;
        }

        public bool HasQueuedSpell(Spell spell) => _queuedSpellCast?.Spell == spell;

        /// <summary>
        /// Forces this unit to stop targeting the given unit.
        /// Applies to attacks, spell casts, spell channels, and any queued spell casts.
        /// </summary>
        /// <param name="target"></param>
        public void Untarget(AttackableUnit target)
        {
            if (_queuedSpellCast?.TargetUnit == target) ClearQueuedSpell();
            if (SpellToCast != null && TargetUnit == target) SetSpellToCast(null, Vector2.Zero);
            if (TargetUnit == target)
            {
                SetTargetUnit(null, true);
            }

            if (_castingSpell != null)
            {
                _castingSpell.RemoveTarget(target);
            }
            if (ChannelSpell != null)
            {
                ChannelSpell.RemoveTarget(target);
            }
            if (SpellToCast != null)
            {
                SpellToCast.RemoveTarget(target);
            }
        }

        /// <summary>
        /// Sets this AI's current target unit. This relates to both auto attacks as well as general spell targeting.
        /// </summary>
        /// <param name="target">Unit to target.</param>
        public void SetTargetUnit(AttackableUnit target, bool networked = false)
        {
            if (TargetUnit == target)
            {
                return;
            }

            bool wasTargetingChampion = TargetUnit is Champion;

            if (TargetUnit != null)
            {
                TargetUnit.TargetedBy.Remove(this);
                if (target == null)
                {
                    ApiEventManager.OnTargetLost.Publish(this, TargetUnit);
                }
            }

            TargetUnit = target;

            if (TargetUnit != null)
            {
                if (!TargetUnit.TargetedBy.Contains(this))
                {
                    TargetUnit.TargetedBy.Add(this);
                }
            }

            if (networked)
            {
                _game.PacketNotifier.NotifyAI_TargetS2C(this, target);

                if (target is Champion c)
                {
                    _game.PacketNotifier.NotifyAI_TargetHeroS2C(this, c);
                }
                else if (wasTargetingChampion)
                {
                    _game.PacketNotifier.NotifyAI_TargetHeroS2C(this, null);
                }
            }
        }

        /// <summary>
        /// Swaps the spell in the given slot1 with the spell in the given slot2.
        /// </summary>
        /// <param name="slot1">Slot of the spell to put into slot2.</param>
        /// <param name="slot2">Slot of the spell to put into slot1.</param>
        public void SwapSpells(byte slot1, byte slot2, bool networked = true)
        {
            if (Spells[slot1].CastInfo.IsAutoAttack || Spells[slot2].CastInfo.IsAutoAttack)
            {
                return;
            }
            if (_queuedSpellCast != null && (_queuedSpellCast.SpellSlot == slot1 || _queuedSpellCast.SpellSlot == slot2))
                ClearQueuedSpell();
            if (SpellToCast != null && (SpellToCast.CastInfo.SpellSlot == slot1 || SpellToCast.CastInfo.SpellSlot == slot2))
                SetSpellToCast(null, Vector2.Zero);

            var slot1Name = Spells[slot1].SpellName;
            var slot2Name = Spells[slot2].SpellName;

            var enabledBuffer = GetSpellInputEnabled(slot1);
            var buffer = Spells[slot1];
            Spells[slot1] = Spells[slot2];

            Spells[slot2] = buffer;

            Spells[slot1].CastInfo.SpellSlot = slot1;
            Spells[slot2].CastInfo.SpellSlot = slot2;
            Spells[slot1].ToolTipData.SetSlot((byte)(slot1 + 60));
            Spells[slot2].ToolTipData.SetSlot((byte)(slot2 + 60));
            if (this is Champion tooltipOwner)
            {
                tooltipOwner.AddToolTipChange(Spells[slot1].ToolTipData);
                tooltipOwner.AddToolTipChange(Spells[slot2].ToolTipData);
            }

            SetSpellInputEnabled(slot1, GetSpellInputEnabled(slot2));
            SetSpellInputEnabled(slot2, enabledBuffer);

            if (networked && this is Champion champion)
            {
                int clientId = _game.PlayerManager.GetClientInfoByChampion(champion).ClientId;
                _game.PacketNotifier.NotifyS2C_SetSpellData(clientId, NetId, slot2Name, slot1);
                _game.PacketNotifier.NotifyS2C_SetSpellData(clientId, NetId, slot1Name, slot2);

                _game.PacketNotifier.NotifyS2C_SetSpellLevel(clientId, NetId, slot1, Spells[slot1].CastInfo.SpellLevel);
                _game.PacketNotifier.NotifyS2C_SetSpellLevel(clientId, NetId, slot2, Spells[slot2].CastInfo.SpellLevel);
            }
        }

        /// <summary>
        /// Sets the spell that will be channeled by this unit. Used by Spell for manual stopping and networking.
        /// </summary>
        /// <param name="spell">Spell that is being channeled.</param>
        /// <param name="network">Whether or not to send the channeling of this spell to clients.</param>
        public void SetChannelSpell(Spell spell, bool network = true)
        {
            ChannelSpell = spell;
        }

        /// <summary>
        /// Forces this AI to stop channeling based on the given condition with the given reason.
        /// </summary>
        /// <param name="condition">Canceled or successful?</param>
        /// <param name="reason">How it should be treated.</param>
        public void StopChanneling(ChannelingStopCondition condition, ChannelingStopSource reason)
        {
            if (ChannelSpell != null)
            {
                ChannelSpell.StopChanneling(condition, reason);
                ChannelSpell = null;
            }
        }

        /// <summary>
        /// Gets the most recently spawned Pet unit which is owned by this unit.
        /// </summary>
        public Pet GetPet()
        {
            return _lastPetSpawned != null && !_lastPetSpawned.IsDead && !_lastPetSpawned.IsToRemove()
                ? _lastPetSpawned : null;
        }

        /// <summary>
        /// Sets the most recently spawned Pet unit which is owned by this unit.
        /// </summary>
        public void SetPet(Pet pet)
        {
            _lastPetSpawned = pet;
        }

        public override void OnAfterSync()
        {
            if (_movementUpdated) OnMovementSnapshotCreated();
            base.OnAfterSync();
            TryPostActivateCharScript();
            TryPostActivateSpellScripts();
        }

        private readonly struct SpellUpdateSnapshot : IDisposable
        {
            private readonly (Spell Spell, uint CastVersion)[] _spells;
            private readonly int _count;

            public SpellUpdateSnapshot(Dictionary<short, Spell> spells)
            {
                _count = spells.Count;
                _spells = ArrayPool<(Spell Spell, uint CastVersion)>.Shared.Rent(_count);
                int index = 0;
                foreach (var spell in spells.Values)
                {
                    _spells[index] = (spell, spell.GetCastVersion());
                    index++;
                }
            }

            public uint? GetCastVersion(Spell spell)
            {
                for (int index = 0; index < _count; index++)
                {
                    if (_spells[index].Spell == spell)
                    {
                        return _spells[index].CastVersion;
                    }
                }

                return null;
            }

            public void Dispose()
            {
                Array.Clear(_spells, 0, _count);
                ArrayPool<(Spell Spell, uint CastVersion)>.Shared.Return(_spells);
            }
        }

        public override void Update(float diff)
        {
            using var spellUpdates = new SpellUpdateSnapshot(Spells);

            // Elapsed time belongs to the existing cooldown, never an attack
            // started by scripts or targeting later in this update.
            if (_autoAttackCurrentCooldown > 0)
                _autoAttackCurrentCooldown = Math.Max(0, _autoAttackCurrentCooldown - diff / 1000.0f);
            if (delayedSpellPackets.Count > 0) invisSent = true;
            _updatingMovement = true;
            try { 
                base.Update(diff); 
            }
            finally 
            { 
                _updatingMovement = false; 
            }
            try
            {
                CharScript.OnUpdate(diff);
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }

            if (!_aiPaused)
            {
                try
                {
                    AIScript.OnUpdate(diff);
                }
                catch (Exception e)
                {
                    _logger.Error(null, e);
                }
            }

            {
                // Keep snapshot semantics, a script may replace spells while updating.
                // Local rented storage also remains safe if a callback reenters an update.
                int count = Spells.Count;
                if (count > 0)
                {
                    var snapshot = ArrayPool<Spell>.Shared.Rent(count);
                    try
                    {
                        Spells.Values.CopyTo(snapshot, 0);
                        for (int i = 0; i < count; i++)
                        {
                            var spell = snapshot[i];
                            spell.UpdateFromOwner(diff, spellUpdates.GetCastVersion(spell));
                        }
                    }
                    finally
                    {
                        Array.Clear(snapshot, 0, count);
                        ArrayPool<Spell>.Shared.Return(snapshot);
                    }
                }
            }

            if (Inventory != null)
            {
                Inventory.OnUpdate(diff);
            }

            {
                UpdateAssistMarkers();
                ResumePostponedMovementOrder();
                FlushQueuedSpell();
                UpdateTarget();
            }


            if (_lastPetSpawned != null && _lastPetSpawned.IsDead)
            {
                SetPet(null);
            }
            // i still wanna keep the delayed packet system for now since i dont know what happens when channeling and invis goes off.
            /*
            foreach (var info in delayedSpellPackets)
            {
                var spell = info.SpellToPacketize;
                var target = spell.CastInfo.Targets.FirstOrDefault()?.Unit;
                if (target == null) continue;
                var attackType = AttackType.ATTACK_TYPE_RADIAL; // Default
                if (spell.CastInfo.IsAutoAttack || spell.CastInfo.UseAttackCastTime)
                {
                    attackType = this.IsMelee ? AttackType.ATTACK_TYPE_MELEE : AttackType.ATTACK_TYPE_TARGETED;
                }
                else if (spell.SpellData.TargetingType == TargetingType.Target)
                {
                    attackType = AttackType.ATTACK_TYPE_TARGETED;
                }
                float delayInSeconds = (_game.GameTime - info.CreationTime) / 1000.0f;
                var spellCastPacket = _game.PacketNotifier.ConstructCastSpellPacket(spell, delayInSeconds);
                var lookAtPacket = new S2C_UnitSetLookAt
                {
                    SenderNetID = this.NetId,
                    LookAtType = (byte)attackType,
                    TargetNetID = target.NetId,
                    TargetPosition = target.GetPosition3D()
                };
                foreach (TeamId team in Enum.GetValues(typeof(TeamId)))
                {
                    if (team != Team)
                    {
                        _game.PacketNotifier.NotifyNPC_CastSpellTeam(spellCastPacket, this, team);
                    }
                }
            }
            delayedSpellPackets.Clear();
            */
        }


        public override void LateUpdate(float diff)
        {
            if (TargetUnit != null && (!TargetUnit.Status.HasFlag(StatusFlags.Targetable) || !TargetUnit.GetIsTargetableToTeam(Team)))
            {
                if (TargetUnit.CharData.IsUseable)
                {
                    return;
                }
                Untarget(TargetUnit);
            }
        }

        private float _nextCallForHelpMs;
        private AttackableUnit _lastHelpAttacker;

        public override void TakeDamage(DamageData damageData, DamageResultType damageText, IEventSource sourceScript = null)
        {
            base.TakeDamage(damageData, damageText, sourceScript);
            var attacker = damageData.Attacker;
            if (attacker == null || attacker == this || attacker.Team == Team
                || damageData.DamageSource == DamageSource.DAMAGE_SOURCE_INTERNALRAW
                || Status.HasFlag(StatusFlags.SuppressCallForHelp)
                || attacker.Status.HasFlag(StatusFlags.CallForHelpSuppressor)) return;

            var settings = Content.GlobalData.CallForHelpVariables;
            float now = _game.GameTime;
            // A new attacker must not be hidden by a previous attacker's rate limit.
            if (attacker == _lastHelpAttacker && now < _nextCallForHelpMs) return;
            _lastHelpAttacker = attacker;
            _nextCallForHelpMs = now + Math.Max(0, settings.Delay) * 1000f;
            float radius = Math.Max(0, settings.Radius);
            var responders = _game.Map.CollisionHandler.GetAIUnitsInRadius(Position, radius);
            foreach (var u in responders)
            {
                if (u == this || u.IsDead || u.Team != Team || u.IsToRemove()
                    || !u.AIScript.AIScriptMetaData.HandlesCallsForHelp
                    || u.Status.HasFlag(StatusFlags.IgnoreCallForHelp)
                    || Vector2.DistanceSquared(u.Position, Position) > radius * radius) continue;
                float buffer = u is BaseTurret ? settings.TurretRadius
                    : u.IsMelee ? settings.MeleeRadius : settings.RangedRadius;
                float responseRange = Math.Max(0, u.Stats.Range.Total + attacker.CollisionRadius + buffer);
                if (Vector2.DistanceSquared(u.Position, attacker.Position) > responseRange * responseRange) continue;
                try { u.AIScript.OnCallForHelp(attacker, this); }
                catch (Exception e) { _logger.Error(null, e); }
            }
        }

        /// <summary>
        /// Updates this AI's current target and attack actions depending on conditions such as crowd control, death state, vision, distance to target, etc.
        /// Used for both auto and spell attacks.
        /// </summary>
        private void UpdateTarget()
        {
            if (IsDead)
            {
                if (TargetUnit != null)
                {
                    CancelAutoAttack(true, true);
                    SetTargetUnit(null, true);
                }
                return;
            }
            else if (TargetUnit == null)
            {
                if (IsAttacking)
                {
                    if (!HasAutoAttacked && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                    {
                        CancelAutoAttack(true, true);
                    }
                    else if (AutoAttackSpell.State == SpellState.STATE_READY)
                    {
                        IsAttacking = false;
                        HasMadeInitialAttack = false;
                    }
                }
                else if (HasMadeInitialAttack)
                {
                    HasMadeInitialAttack = false;
                }
            }
            else if (TargetUnit.IsDead
                || ((!TargetUnit.Status.HasFlag(StatusFlags.Targetable) || !TargetUnit.GetIsTargetableToTeam(Team)) && !TargetUnit.CharData.IsUseable)
                || !TargetUnit.IsVisibleByTeam(Team))
            {
                // If the attack already connected (e.g. HasAutoAttacked), let the animation
                // finish instead of cancelling it mid-animation.
                if (IsAttacking)
                {
                    if (!HasAutoAttacked && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                    {
                        CancelAutoAttack(true, true);
                    }
                    else if (AutoAttackSpell.State == SpellState.STATE_READY)
                    {
                        IsAttacking = false;
                        HasMadeInitialAttack = false;
                    }
                }

                Untarget(TargetUnit);
                return;
            }
            else if (IsAttacking)
            {
                float cancelBuffer = 300.0f;
                float maxCancelRange = Stats.Range.Total + TargetUnit.CollisionRadius + cancelBuffer;
                if (Vector2.Distance(TargetUnit.Position, Position) > maxCancelRange
                        && AutoAttackSpell.State == SpellState.STATE_CASTING && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                {
                    CancelAutoAttack(true, true);
                }

                if (AutoAttackSpell.State == SpellState.STATE_READY)
                {
                    IsAttacking = false;
                }
                else return;
            }

            var idealRange = Stats.Range.Total;
            if (SpellToCast != null && (_spellToCastIsPlayerInput && (!IsSpellReadyForInput(SpellToCast)
                    || SpellToCast.CastInfo.SpellSlot != _spellToCastInputSlot)
                || TargetUnit != null && (TargetUnit.IsToRemove() || !SpellToCast.SpellData.IsValidTarget(this, TargetUnit))))
            {
                SetSpellToCast(null, Vector2.Zero);
            }
            if (SpellToCast != null && !IsAttacking && (TargetUnit == null || SpellToCast.SpellData.IsValidTarget(this, TargetUnit)))
            {
                if (!CanCast(SpellToCast)) return;
                // Spell casts usually do not take into account collision radius, thus range is center -> center VS edge -> edge for attacks.
                idealRange = SpellToCast.GetCurrentCastRange();

                if (MoveOrder == OrderType.AttackTo
                    && TargetUnit != null
                    && Vector2.DistanceSquared(TargetUnit.Position, SpellToCast.CastInfo.Owner.Position) <= idealRange * idealRange)
                {
                    CastPendingSpell(TargetUnit);
                }
                else if (MoveOrder == OrderType.MoveTo
                        && Vector2.DistanceSquared(new Vector2(SpellToCast.CastInfo.TargetPosition.X, SpellToCast.CastInfo.TargetPosition.Z), SpellToCast.CastInfo.Owner.Position) <= idealRange * idealRange)
                {
                    CastPendingSpell(null);
                }
                else
                {
                    RefreshWaypoints(idealRange);
                }
            }
            else
            {
                // TODO: Verify if there are any other cases we want to avoid.
                if (TargetUnit != null && TargetUnit.Team != Team && MoveOrder != OrderType.CastSpell)
                {
                    idealRange = Stats.Range.Total + TargetUnit.CollisionRadius;

                    if (Vector2.DistanceSquared(Position, TargetUnit.Position) <= idealRange * idealRange && MovementParameters == null)
                    {
                        if (AutoAttackSpell.State == SpellState.STATE_READY)
                        {
                            // Stops us from continuing to move towards the target.
                            RefreshWaypoints(idealRange);

                            if (CanAttack())
                            {
                                IsNextAutoCrit = _random.Next(0, 100) < Stats.CriticalChance.Total * 100;
                                if (_autoAttackCurrentCooldown <= 0)
                                {
                                    HasAutoAttacked = false;
                                    IsAttacking = true;
                                    // TODO: ApiEventManager.OnUnitPreAttack.Publish(this);
                                    if (!_skipNextAutoAttack)
                                    {
                                        AutoAttackSpell = IsAutoAttackOverridden
                                            ? GetNextOverriddenAutoAttackForCast(IsNextAutoCrit)
                                            : GetNewAutoAttack();

                                        PrepareAutoAttackSpellForCast(AutoAttackSpell);
                                        if (AutoAttackSpell != null && AutoAttackSpell.Cast(TargetUnit.Position, TargetUnit.Position, TargetUnit))
                                        {
                                            _autoAttackCurrentCooldown = GetAutoAttackCooldownSeconds(AutoAttackSpell);
                                        }
                                    }
                                    else
                                    {
                                        _skipNextAutoAttack = false;
                                    }
                                }
                            }
                        }
                        // Update the auto attack spell target.
                        // Units outside of range are ignored.
                        else if (IsAttacking
                                 && AutoAttackSpell.CastInfo.Targets.Count > 0
                                 && (AutoAttackSpell.CastInfo.Targets[0] as CastTarget)?.Unit != TargetUnit
                                 && !(Vector2.Distance(TargetUnit.Position, Position) > (Stats.Range.Total + TargetUnit.CollisionRadius)))
                        {
                            AutoAttackSpell.SetCurrentTarget(TargetUnit);
                        }
                        else if (!IsAttacking)
                        {
                            RefreshWaypoints(idealRange);
                        }
                    }
                    else
                    {
                        RefreshWaypoints(idealRange);
                    }
                }
                else
                {
                    // Acquires the closest target.
                    // TODO: Make a function which uses this method and use it for every case of target acquisition (ex minions, turrets, attackmove).
                    if (MoveOrder == OrderType.AttackMove && !AIScript.AIScriptMetaData.HandlesTargetAcquisition)
                    {
                        if (_autoAttackCurrentCooldown > 0)
                        {
                            return;
                        }

                        var nextTarget = _game.ObjectManager.FindAttackMoveTarget(this);

                        if (nextTarget != null)
                        {
                            SetTargetUnit(nextTarget, true);
                        }
                    }

                    if (AutoAttackSpell != null && AutoAttackSpell.State == SpellState.STATE_READY && IsAttacking)
                    {
                        IsAttacking = false;
                        //HasMadeInitialAttack = false;
                    }
                }
            }
        }

        private void CastPendingSpell(AttackableUnit target)
        {
            var spell = SpellToCast;
            var onSuccess = _spellToCastOnSuccess;
            bool playerInput = _spellToCastIsPlayerInput;
            var start = new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z);
            var end = new Vector2(spell.CastInfo.TargetPositionEnd.X, spell.CastInfo.TargetPositionEnd.Z);
            if (spell.Cast(start, end, target))
            {
                onSuccess?.Invoke();
            }
            else if (SpellToCast == spell)
            {
                _spellToCastOnSuccess = onSuccess;
                _spellToCastIsPlayerInput = playerInput;
            }
        }

        /// <summary>Applies an allowed order. Lifecycle cleanup can preserve buffered player input.</summary>
        public bool UpdateMoveOrder(OrderType order, bool publish = true, bool cancelQueuedSpell = true)
        {
            if (publish)
            {
                // Return if scripts do not allow this order.
                if (!ApiEventManager.OnUnitUpdateMoveOrder.Publish(this, order))
                {
                    return false;
                }
            }

            MoveOrder = order;
            if (cancelQueuedSpell)
            {
                _moveOrderRevision++;
                _movementOrderToResume = null;
            }

            if ((MoveOrder == OrderType.OrderNone
                || MoveOrder == OrderType.Stop
                || MoveOrder == OrderType.PetHardStop)
                && !IsPathEnded())
            {
                StopMovement();
                SetTargetUnit(null, true);
            }

            if (MoveOrder == OrderType.Hold
                || MoveOrder == OrderType.Taunt)
            {
                StopMovement();
            }
            if (MoveOrder == OrderType.MoveTo
                || MoveOrder == OrderType.AttackMove
                || MoveOrder == OrderType.Stop
                || MoveOrder == OrderType.OrderNone
                || MoveOrder == OrderType.Hold)
            {
                if (cancelQueuedSpell) ClearQueuedSpell();
                if (_castingSpell != null && !_castingSpell.SpellData.CantCancelWhileWindingUp)
                {
                    _castingSpell.CancelCast();
                }

                if (IsAttacking && !HasAutoAttacked && AutoAttackSpell != null && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                {
                    CancelAutoAttack(true, true);
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the state of this unit's AI.
        /// </summary>
        public AIState GetAIState()
        {
            return _aiState;
        }

        /// <summary>
        /// Sets the state of this unit's AI.
        /// </summary>
        /// <param name="newState">State to set.</param>
        public void SetAIState(AIState newState)
        {
            _aiState = newState;
        }

        /// <summary>
        /// Whether or not this unit's AI is innactive.
        /// </summary>
        public bool IsAIPaused()
        {
            return _aiPaused;
        }

        /// <summary>
        /// Forces this unit's AI to pause/unpause.
        /// </summary>
        /// <param name="isPaused">Whether or not to pause.</param>
        public void PauseAI(bool isPaused)
        {
            _aiPaused = isPaused;
        }

        internal void AddAssistMarker(ObjAIBase sourceUnit, float duration, DamageData damageData = null)
        {
            if (sourceUnit is Champion)
            {
                if (sourceUnit.Team == Team)
                {
                    AuxAddAssistMarker(AlliedAssistMarkers, sourceUnit, duration, damageData);
                }
                else
                {
                    AuxAddAssistMarker(EnemyAssistMarkers, sourceUnit, duration, damageData);
                }
            }
        }

        void AuxAddAssistMarker(List<AssistMarker> assistList, ObjAIBase sourceUnit, float duration, DamageData damageData = null)
        {
            AssistMarker assistMarker = assistList.Find(x => x.Source == sourceUnit);
            if (assistMarker != null)
            {
                float desiredDuration = _game.GameTime + duration * 1000;
                assistMarker.StartTime = _game.GameTime;
                assistMarker.EndTime = assistMarker.EndTime < desiredDuration ? desiredDuration : assistMarker.EndTime;
            }
            else
            {
                assistMarker = new AssistMarker()
                {
                    Source = sourceUnit,
                    StartTime = _game.GameTime,
                    EndTime = _game.GameTime + duration * 1000,
                };

                assistList.Add(assistMarker);
            }

            if (damageData != null)
            {
                switch (damageData.DamageType)
                {
                    case DamageType.DAMAGE_TYPE_PHYSICAL:
                        assistMarker.PhysicalDamage += damageData.Damage;
                        break;
                    case DamageType.DAMAGE_TYPE_MAGICAL:
                        assistMarker.MagicalDamage += damageData.Damage;
                        break;
                    case DamageType.DAMAGE_TYPE_TRUE:
                        assistMarker.TrueDamage += damageData.Damage;
                        break;
                }
            }

            assistList = assistList.OrderByDescending(x => x.StartTime).ToList();
        }

        void UpdateAssistMarkers()
        {
            AlliedAssistMarkers.RemoveAll(x => x.EndTime < _game.GameTime);
            EnemyAssistMarkers.RemoveAll(x => x.EndTime < _game.GameTime);
        }

        protected override void UpdateFacing()
        {
            if (MovementParameters == null && !CanMove()) return;
            bool isCastingMobile = _castingSpell != null && _castingSpell.SpellData.CanMoveWhileChanneling;
            bool isChannelingMobile = ChannelSpell != null && ChannelSpell.SpellData.CanMoveWhileChanneling;

            if (!isCastingMobile && !isChannelingMobile)
            {
                base.UpdateFacing();
            }

        }
        public bool ChangeModelTo(string model)
        {
            if (Model.Equals(model))
            {
                return false;
            }
            Model = model;
            _game.PacketNotifier.NotifyS2C_ChangeCharacterData(this, skinID: (uint)SkinID);
            return true;
        }

        public void SetModelOverride(string model, object source, uint skinID = uint.MaxValue)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var previous = _modelOverrides.LastOrDefault(entry => entry.Source == source);
            if (previous != null && previous.Model == model && previous.SkinID == skinID)
            {
                return;
            }

            RemoveModelOverride(source);
            if (string.IsNullOrEmpty(model))
            {
                return;
            }

            var entry = new CharacterModelOverride
            {
                Model = model,
                SkinID = skinID,
                ID = _game.NetworkIdManager.GetNewNetId(),
                Source = source
            };
            _modelOverrides.Add(entry);
            _game.PacketNotifier.NotifyS2C_ChangeCharacterData(
                this,
                skinID: entry.SkinID,
                modelName: entry.Model,
                id: entry.ID
            );
        }

        public void RemoveModelOverride(object source)
        {
            for (int i = _modelOverrides.Count - 1; i >= 0; i--)
            {
                var entry = _modelOverrides[i];
                if (entry.Source != source)
                {
                    continue;
                }

                _game.PacketNotifier.NotifyS2C_PopCharacterData(this, entry.ID);
                _modelOverrides.RemoveAt(i);
            }
        }
        public override void SetPosition(Vector2 vec, bool repath = true)
        {
            if (MovementRestrictionRadius > 0)
            {
                float distSq = Vector2.DistanceSquared(MovementRestrictionCenter, vec);
                float radSq = MovementRestrictionRadius * MovementRestrictionRadius;

                if (distSq > radSq)
                {
                    Vector2 dir = Vector2.Normalize(vec - MovementRestrictionCenter);
                    vec = MovementRestrictionCenter + (dir * MovementRestrictionRadius);
                }
            }

            base.SetPosition(vec, repath);
        }
        public override void TeleportTo(Vector2 position, bool repath = false)
        {
            if (MovementRestrictionRadius > 0)
            {
                float distSq = Vector2.DistanceSquared(MovementRestrictionCenter, position);
                float radSq = MovementRestrictionRadius * MovementRestrictionRadius;

                if (distSq > radSq)
                {
                    Vector2 dir = Vector2.Normalize(position - MovementRestrictionCenter);
                    position = MovementRestrictionCenter + (dir * MovementRestrictionRadius);
                }
            }

            base.TeleportTo(position, repath);
        }
        public override void Die(DeathData data)
        {
            ClearQueuedSpell();
            SetSpellToCast(null, Vector2.Zero);
            SetTargetUnit(null, true);
            CancelAutoAttack(true, true);
            if (_castingSpell != null)
            {
                _castingSpell.CancelCast();
            }
            if (ChannelSpell != null)
            {
                StopChanneling(ChannelingStopCondition.Cancel, ChannelingStopSource.Die);
            }
            ClearQueuedSpell();

            base.Die(data);
        }
    }
}