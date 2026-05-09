using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Content;
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
using System.Linq;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
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
        public Vector2 Start { get; }
        public Vector2 End { get; }
        public AttackableUnit TargetUnit { get; }

        public SpellQueueEntry(Spell spell, Vector2 start, Vector2 end, AttackableUnit targetUnit)
        {
            Spell = spell;
            Start = start;
            End = end;
            TargetUnit = targetUnit;
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
        private float _autoAttackCurrentCooldown;
        private bool _skipNextAutoAttack;
        private Spell _castingSpell;
        private Spell _lastAutoAttack;
        private Spell _lastOverrideAutoAttack;
        private SpellQueueEntry _queuedSpellCast;
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
        /// Whether this entity should use the client's fast (less accurate) A* mode when
        /// requesting paths from <see cref="NavigationGrid.GetPath"/>. Mirrors the client's
        /// per-actor `m_UseSlowerButMoreAccurateSearch` flag inverted: client default is
        /// slow-accurate=false (= fast=true here); only `obj_AI_Minion` instances override
        /// the actor flag to true (S1 obj_ai_minion.cpp:1716), which corresponds to fast=false
        /// here.
        ///
        /// Default in this class is false (slow-accurate) so that all `Minion`/`Pet`/jungle
        /// subclasses get the right behavior without per-class overrides; <see cref="Champion"/>
        /// overrides to true.
        /// </summary>
        public virtual bool UsesFastPath => false;
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

        // Hysteresis state for the chase/AA range checks. Once the target enters the tight
        // trigger range (idealRange * ChasingAttackRangePercent), the unit is "engaged" and
        // keeps attacking until the target leaves the wider cancel range
        // (idealRange + StopAttackRangeModifier). Reset on target swap. Mirrors the
        // TargetInAttackRange / TargetInCancelAttackRange split from Hero.lua / Aggro.lua.
        private bool _engagedWithTarget;

        // Countdown after a fired auto-attack during which player-issued move orders are
        // ignored. Mirrors the per-character PostAttackMoveDelay from CharData (4.20 client
        // CharData::mPostAttackMoveDelay). Most champions use 0 (free stutter step), a few
        // special-cased characters use a positive value to prevent stutter cancel from
        // bypassing their windup. AI-driven repaths still go through (CanChangeWaypoints
        // doesn't check this) so chasing keeps working — only the human input is gated.
        private float _postAttackMoveDelayRemainingMs;

        // Last-known-location chase state. When TargetUnit is lost via fog or stealth (not
        // death) the unit doesn't fully drop the chase — it walks toward where the target
        // was last visible and re-acquires it without a player click if the target reappears
        // within visibility. Mirrors AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION + GetLostTargetIfVisible
        // from Hero.lua (functions 0_3 OnTargetLost and 0_16 TimerDistanceScan). Both fields
        // are cleared by any new player-issued target/move order, by reaching the last-known
        // position without re-acquire, or by the target dying.
        public AttackableUnit LostTarget { get; private set; }
        private Vector2 _lostTargetLastPosition;

        // Mirrors the client's "[General]AutoAcquireTarget" game option. When true the unit
        // scans for nearby enemies on idle/movement-stop and engages a soft attack without a
        // player click. Default false for all units. The client pushes packet 0x47
        // (C2S_UpdateGameOptions, handled in HandleAutoAttackOption) when the player ticks
        // the checkbox, which flips this on per-champion. Minions and other non-player AI
        // stay off; their target acquisition runs through dedicated AI scripts (Minion.lua,
        // LaneMinionAI, etc.), not this idle-scan path.
        public bool AutoAcquireTargetEnabled { get; set; } = false;
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

            AIScript = game.ScriptEngine.CreateObject<IAIScript>($"AIScripts", aiScript) ?? new EmptyAIScript();
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
            CharScript = CSharpScriptEngine.CreateObjectStatic<ICharScript>("CharScripts", $"CharScript{Model}") ?? new CharScriptEmpty();
            _charScriptActivated = false;
            _charScriptPostActivated = false;
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
            
            // TODO: Verify if we should use MissChance instead.
            if (HasBuffType(BuffType.BLIND))
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
            return (!IsDead
                && MovementParameters != null)
                || (Status.HasFlag(StatusFlags.CanMove) && Status.HasFlag(StatusFlags.CanMoveEver)
                && (MoveOrder != OrderType.CastSpell && _castingSpell == null)
                && (ChannelSpell == null || (ChannelSpell != null && ChannelSpell.SpellData.CanMoveWhileChanneling))
                && (!IsAttacking || !AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                && !(Status.HasFlag(StatusFlags.Netted)
                || Status.HasFlag(StatusFlags.Rooted)
                || Status.HasFlag(StatusFlags.Sleep)
                || Status.HasFlag(StatusFlags.Stunned)
                || Status.HasFlag(StatusFlags.Suppressed)));
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

            // PostAttackMoveDelay window: a fired auto-attack arms a per-character cooldown
            // during which player move orders are rejected (prevents stutter cancel bypassing
            // windup). Unit's own AI repaths still go through CanChangeWaypoints, which
            // doesn't check this — so chase keeps working.
            if (_postAttackMoveDelayRemainingMs > 0f)
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
        public bool CanCast(Spell spell = null)
        {
            // TODO: Verify if all cases are accounted for.
            return ApiEventManager.OnCanCast.Publish(this, spell)
                && Status.HasFlag(StatusFlags.CanCast)
                && !Status.HasFlag(StatusFlags.Charmed)
                && !Status.HasFlag(StatusFlags.Feared)
                // TODO: Verify what pacified is
                && !Status.HasFlag(StatusFlags.Pacified)
                && !Status.HasFlag(StatusFlags.Silenced)
                && !Status.HasFlag(StatusFlags.Sleep)
                && !Status.HasFlag(StatusFlags.Stunned)
                && !Status.HasFlag(StatusFlags.Suppressed)
                && !Status.HasFlag(StatusFlags.Taunted)
                && _castingSpell == null
                && (ChannelSpell == null || (ChannelSpell != null && !ChannelSpell.SpellData.CantCancelWhileChanneling))
                && (!IsAttacking || (IsAttacking && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp));
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

        public override bool Move(float diff)
        {
            // If we have waypoints, but our move order is one of these, we shouldn't move.
            if (MoveOrder == OrderType.CastSpell
                || MoveOrder == OrderType.OrderNone
                || MoveOrder == OrderType.Stop
                || MoveOrder == OrderType.Taunt)
            {
                return false;
            }

            return base.Move(diff);
        }

        /// <summary>
        /// Cancels any auto attacks this AI is performing and resets the time between the next auto attack if specified.
        /// </summary>
        /// <param name="reset">Whether or not to reset the delay between the next auto attack.</param>
        public void CancelAutoAttack(bool reset, bool fullCancel = false)
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
            if (reset || fullCancel) _game.PacketNotifier.NotifyNPC_InstantStop_Attack(this, false);
        }

        /// <summary>
        /// Reason-based cancel — mirrors the 4.20 client's AutoAttackStopReason / Lua
        /// STOPREASON_* values. Three behavioural tiers:
        ///
        ///   * <see cref="AutoAttackStopReason.Moving"/> /
        ///     <see cref="AutoAttackStopReason.OtherFinishAnim"/>: soft. Damage that's
        ///     already been dispatched stays out, the cooldown keeps running, but the
        ///     attack-state flags drop and the client gets a stop-attack notification.
        ///     Equivalent to the legacy <c>CancelAutoAttack(reset: false, fullCancel: true)</c>.
        ///
        ///   * <see cref="AutoAttackStopReason.AttackDelayed"/>: interrupt + re-arm. Cooldown
        ///     is reset (so a freshly-overridden auto-attack can fire immediately) and the
        ///     spell cast is rolled back, but <c>IsAttacking</c> stays so the swap is
        ///     visually fluid. Equivalent to legacy <c>CancelAutoAttack(reset: true, fullCancel: false)</c>.
        ///     Used by AA-override paths (buffs replacing auto-attacks at runtime).
        ///
        ///   * <see cref="AutoAttackStopReason.TargetLost"/> /
        ///     <see cref="AutoAttackStopReason.OtherImmediately"/>: hard kill. Windup
        ///     dies, cooldown reset, all state cleared. Equivalent to legacy
        ///     <c>CancelAutoAttack(reset: true, fullCancel: true)</c>.
        /// </summary>
        public void CancelAutoAttack(AutoAttackStopReason reason)
        {
            switch (reason)
            {
                case AutoAttackStopReason.Moving:
                case AutoAttackStopReason.OtherFinishAnim:
                    CancelAutoAttack(reset: false, fullCancel: true);
                    break;

                case AutoAttackStopReason.AttackDelayed:
                    CancelAutoAttack(reset: true, fullCancel: false);
                    break;

                case AutoAttackStopReason.TargetLost:
                case AutoAttackStopReason.OtherImmediately:
                default:
                    CancelAutoAttack(reset: true, fullCancel: true);
                    break;
            }
        }

        /// <summary>
        /// Called by Spell when an auto-attack actually fires (damage dispatched). Arms the
        /// PostAttackMoveDelay window: for the next CharData.PostAttackMoveDelay seconds
        /// player-issued move orders are rejected, preventing stutter cancel from bypassing
        /// windup on champions with non-zero values. Default 0 (most champs) makes this a
        /// no-op. AI-driven repaths and the unit's own waypoint mutation are unaffected.
        /// </summary>
        public void NotifyAutoAttackFired()
        {
            if (CharData == null || CharData.PostAttackMoveDelay <= 0f)
            {
                return;
            }
            _postAttackMoveDelayRemainingMs = CharData.PostAttackMoveDelay * 1000f;
        }

        /// <summary>
        /// Records a target as "lost via fog/stealth" — chase continues toward the last-known
        /// position and the target is auto-re-acquired if it becomes visible again before any
        /// other player order. Mirrors the second argument of OnTargetLost(LOST_VISIBILITY, target)
        /// in Hero.lua.
        /// </summary>
        public void SetLostTarget(AttackableUnit lost)
        {
            if (lost == null || lost.IsDead)
            {
                LostTarget = null;
                return;
            }
            LostTarget = lost;
            _lostTargetLastPosition = lost.Position;
        }

        /// <summary>
        /// Drops any pending last-known-location chase. Called from any new player order
        /// (Stop, MoveTo, AttackTo on a different target, AttackMove) — mirrors how Lua's
        /// OnOrder transitions out of AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION on any new state.
        /// </summary>
        public void ClearLostTarget()
        {
            LostTarget = null;
        }

        /// <summary>
        /// Scans the closest visible+targetable enemy unit within <see cref="Stats.AcquisitionRange"/>.
        /// Used by the auto-acquire-on-stop branch in <see cref="UpdateTarget"/> when the
        /// unit is idle and <see cref="AutoAcquireTargetEnabled"/> is on. Includes a
        /// visibility check (<see cref="AttackableUnit.IsVisibleByTeam"/>) so the AI doesn't
        /// snap onto fog/stealthed enemies — matches Lua's <c>CanSeeMe(target)</c> gate at
        /// Hero.lua function 0_16.
        /// </summary>
        private AttackableUnit FindAutoAcquireTarget()
        {
            float range = Stats.AcquisitionRange.Total;
            float bestDistSq = range * range;
            AttackableUnit best = null;

            foreach (var kv in _game.ObjectManager.GetObjects())
            {
                if (kv.Value is not AttackableUnit u) continue;
                if (u == this) continue;
                if (u.IsDead || u.IsToRemove()) continue;
                if (u.Team == Team) continue;
                if (!u.Status.HasFlag(StatusFlags.Targetable)) continue;
                if (!u.IsVisibleByTeam(Team)) continue;

                float dSq = Vector2.DistanceSquared(Position, u.Position);
                if (dSq > bestDistSq) continue;

                bestDistSq = dSq;
                best = u;
            }

            return best;
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
            AttackableUnit caster = null
        )
        {
            if (MovementParameters != null)
            {
                SetDashingState(false, MoveStopReason.ForceMovement);
            }

            SetWaypoints(new List<Vector2> { Position, target.Position }, true);

            SetTargetUnit(target, true);

            // TODO: Take into account the rest of the arguments
            MovementParameters = new ForceMovementParameters
            {
                SetStatus = StatusFlags.None,
                ElapsedTime = 0,
                PathSpeedOverride = dashSpeed,
                ParabolicGravity = leapGravity,
                ParabolicStartPoint = Position,
                KeepFacingDirection = keepFacingLastDirection,
                FollowNetID = target.NetId,
                FollowDistance = followTargetMaxDistance,
                FollowBackDistance = backDistance,
                FollowTravelTime = travelTime,
                MovementName = movementName,
                Caster = caster ?? this
            };

            if (consideredCC)
            {
                MovementParameters.SetStatus = StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove;
            }

            _game.PacketNotifier.NotifyWaypointListWithSpeed(
                this,
                dashSpeed,
                leapGravity,
                keepFacingLastDirection,
                target,
                followTargetMaxDistance,
                backDistance,
                travelTime
            );
            if (target != null)
            {
                _game.PacketNotifier.NotifyMovementDriverReplication(this);
            }
            SetDashingState(true);

            if (animation != null && animation != "")
            {
                var animPairs = new Dictionary<string, string> { { "RUN", animation } };
                SetAnimStates(animPairs, MovementParameters);
            }
            _movementUpdated = false;
            // TODO: Verify if we want to use NotifyWaypointListWithSpeed instead as it does not require conversions.
        }

        /// <summary>
        /// Forces this AI unit to perform a lunge which follows the specified AttackableUnit.
        /// Compatibility wrapper for scripts that distinguish lunges from regular dashes.
        /// </summary>
        /// <param name="target">Unit to follow.</param>
        /// <param name="speed">Constant speed that the unit will have during the lunge.</param>
        /// <param name="animation">Internal name of the lunge animation.</param>
        /// <param name="leapGravity">How much gravity the unit will experience when above the ground while lunging.</param>
        /// <param name="keepFacingLastDirection">Whether or not the unit should maintain the direction they were facing before lunging.</param>
        /// <param name="followTargetMaxDistance">Maximum distance the unit will follow the target before stopping or reaching the target.</param>
        /// <param name="backDistance">Additional stopping distance from the target.</param>
        /// <param name="travelTime">Total time (in seconds) the lunge may follow the target before stopping.</param>
        /// <param name="consideredCc">Whether or not to prevent movement, casting, or attacking during the duration of the movement.</param>
        /// <param name="movementType">Force movement type. Included for API compatibility.</param>
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

        /// <summary>
        /// Automatically paths this AI to a favorable auto attacking position. 
        /// Used only for Minions currently.
        /// </summary>
        /// <returns></returns>
        /// TODO: Move this to Minion? It isn't used anywhere else.
        /// TODO: Re-implement this for LaneMinions and add a patience or distance threshold so they don't follow forever.
        public bool RecalculateAttackPosition()
        {
            // If we are already where we should be, which means we are in attack range, then keep our current position.
            if (TargetUnit == null || TargetUnit.IsDead || Vector2.DistanceSquared(Position, TargetUnit.Position) <= Stats.Range.Total * Stats.Range.Total)
            {
                return false;
            }

            var nearestObjects = _game.Map.CollisionHandler.GetNearestObjects(new Circle(Position, DETECT_RANGE));

            foreach (var gameObject in nearestObjects)
            {
                var unit = gameObject as AttackableUnit;
                if (unit == null ||
                    unit.NetId == NetId ||
                    unit.IsDead ||
                    Vector2.DistanceSquared(Position, TargetUnit.Position) > DETECT_RANGE * DETECT_RANGE
                )
                {
                    continue;
                }

                var closestPoint = GameServerCore.Extensions.GetClosestCircleEdgePoint(Position, gameObject.Position, gameObject.PathfindingRadius);

                // If this unit is colliding with gameObject
                if (GameServerCore.Extensions.IsVectorWithinRange(closestPoint, Position, PathfindingRadius))
                {
                    var exitPoint = GameServerCore.Extensions.GetCircleEscapePoint(Position, PathfindingRadius + 1, gameObject.Position, gameObject.PathfindingRadius);
                    SetWaypoints(new List<Vector2> { Position, exitPoint });
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Function which refreshes this AI's waypoints if they have a target.
        /// </summary>
        public virtual void RefreshWaypoints(float idealRange)
        {
            if (MovementParameters != null)
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
                // Two-tier range hysteresis matching the 4.20 Hero.lua / Aggro.lua flow:
                //   - Tight trigger: must close to (idealRange * ChasingAttackRangePercent)
                //     before AA fires. Per-champion (ADCs 0.8, melee 0.3-0.5).
                //   - Loose hold: keep attacking until target moves beyond
                //     (idealRange + ar_StopAttackRangeModifier).
                //   - Between trigger and cancel: keep current state (no AA start, no chase).
                // Without this hysteresis the boundary at idealRange would oscillate as the
                // target jitters by sub-cell amounts, producing the "stutter cast then cancel"
                // loop visible on Trundle Q etc.
                float chasingPct = CharData?.ChasingAttackRangePercent ?? 1.0f;
                float triggerRange = idealRange * chasingPct;
                float cancelRange = idealRange + GlobalData.AttackRangeVariables.StopAttackRangeModifier;
                float distSq = Vector2.DistanceSquared(Position, targetPos);

                if (distSq <= triggerRange * triggerRange)
                {
                    _engagedWithTarget = true;
                }
                else if (distSq > cancelRange * cancelRange)
                {
                    _engagedWithTarget = false;
                }
                // else: in the hysteresis band — leave _engagedWithTarget unchanged.

                if (_engagedWithTarget)
                {
                    bool isReadyToAttack = CanAttack() &&
                       _autoAttackCurrentCooldown <= 0 &&
                       AutoAttackSpell != null &&
                       AutoAttackSpell.State == SpellState.STATE_READY;

                    if (isReadyToAttack)
                    {
                        StopMovement(networked: false);
                    }
                    else
                    {
                        UpdateMoveOrder(OrderType.Hold, true);
                    }
                }
                else
                {
                    // F2 Phase 1: path to a stand-position computed from the target's edge instead
                    // of the target's exact center. Mirrors the client's `Actor_Common::GetClosestAttackPoint`
                    // chain (S4 NavGrid.cpp:3706) -> the C++ backend of the Lua API
                    // `SetStateAndCloseToTarget` that every AI script (Minion.lua / Hero.lua /
                    // BaronMinionAI.lua / Aggro.lua / Pet AIs) uses to approach a target. Without
                    // this, ranged units overshoot their attack range while approaching, and
                    // multiple attackers cluster on the target's exact position.
                    //
                    // The instance overload of GetAttackStandPosition picks up
                    // CharData.ChasingAttackRangePercent automatically — so the path destination
                    // matches the trigger threshold above, not the legacy global 0.95 inset.
                    Vector2 pathDestination = TargetUnit != null
                        ? _game.Map.PathingHandler.GetAttackStandPosition(this, TargetUnit, idealRange)
                        : targetPos;

                    if (!_game.Map.PathingHandler.IsWalkable(pathDestination, PathfindingRadius))
                    {
                        pathDestination = _game.Map.NavigationGrid.GetClosestTerrainExit(pathDestination, PathfindingRadius);
                    }

                    // Unit-aware overload threads the actor-blocked predicate (A1) so the search
                    // routes around blocking enemy units instead of producing a path that would
                    // immediately need post-process collision correction.
                    var newWaypoints = _game.Map.PathingHandler.GetPath(this, pathDestination);
                    if (newWaypoints != null && newWaypoints.Count > 1)
                    {
                        SetWaypoints(newWaypoints);
                        // Activate per-frame chase smoothing: between AI-tick repaths,
                        // UpdateTracking drags the last waypoint toward a point idealRange *
                        // ChasingAttackRangePercent in front of the target — same inset as
                        // GetAttackStandPosition used above. Without matching insets, the
                        // orbit point would sit at a different distance than the path
                        // destination and pull the unit back onto the trigger boundary,
                        // producing the AA stutter loop. (The two-tier hysteresis below is
                        // the structural fix; matching insets is the per-tick consistency.)
                        float chaseInset = CharData?.ChasingAttackRangePercent ?? Handlers.PathingHandler.DefaultStandInset;
                        SetTracking(TargetUnit, idealRange * chaseInset);
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
            Stats.SetSpellEnabled(slot, false);
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
                // AA-override path: a buff/effect just installed a new AA spell, and the
                // caller wants the next attack to fire on a fresh cooldown. AttackDelayed
                // matches that semantic — cooldown reset, but IsAttacking isn't yanked, so
                // the swap is visually fluid.
                CancelAutoAttack(AutoAttackStopReason.AttackDelayed);
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
                // AA-override path: a buff/effect just installed a new AA spell, and the
                // caller wants the next attack to fire on a fresh cooldown. AttackDelayed
                // matches that semantic — cooldown reset, but IsAttacking isn't yanked, so
                // the swap is visually fluid.
                CancelAutoAttack(AutoAttackStopReason.AttackDelayed);
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
                // AA-override path: a buff/effect just installed a new AA spell, and the
                // caller wants the next attack to fire on a fresh cooldown. AttackDelayed
                // matches that semantic — cooldown reset, but IsAttacking isn't yanked, so
                // the swap is visually fluid.
                CancelAutoAttack(AutoAttackStopReason.AttackDelayed);
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
                // AA-override path: a buff/effect just installed a new AA spell, and the
                // caller wants the next attack to fire on a fresh cooldown. AttackDelayed
                // matches that semantic — cooldown reset, but IsAttacking isn't yanked, so
                // the swap is visually fluid.
                CancelAutoAttack(AutoAttackStopReason.AttackDelayed);
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
        /// <returns>Newly created spell set.</returns>
        public Spell SetSpell(string name, byte slot, bool enabled, bool networkOld = false)
        {
            if (!Spells.ContainsKey(slot) || Spells[slot].CastInfo.IsAutoAttack)
            {
                return null;
            }

            var toReturn = Spells[slot];
            var oldSpell = Spells[slot];

            if (name != Spells[slot].SpellName)
            {
                var oldLevel = oldSpell?.CastInfo.SpellLevel ?? (byte)0;
                var oldCurrentCooldown = oldSpell?.CurrentCooldown ?? 0.0f;

                if (oldSpell != null)
                {
                    oldSpell.Deactivate();
                }

                toReturn = new Spell(_game, this, name, slot);
                toReturn.SetLevel(oldLevel);

                if (oldCurrentCooldown > 0.0f)
                {
                    toReturn.SetCooldown(oldCurrentCooldown, true);
                }

                Spells[slot] = toReturn;
                Stats.SetSpellEnabled(slot, enabled);
            }

            if (this is Champion champion)
            {
                int userId = _game.PlayerManager.GetClientInfoByChampion(champion).ClientId;
                // TODO: Verify if this is all that is needed.
                _game.PacketNotifier.NotifyChangeSlotSpellData(userId, champion, slot, ChangeSlotSpellDataType.SpellName, slot == 4 || slot == 5, newName: name);
                if (networkOld)
                {
                    _game.PacketNotifier.NotifyS2C_SetSpellData(userId, NetId, name, slot);
                }
            }

            return toReturn;
        }

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

            if (s == null)
            {
                return;
            }

            if (location != Vector2.Zero)
            {
                var exit = _game.Map.NavigationGrid.GetClosestTerrainExit(location, PathfindingRadius);
                var path = _game.Map.PathingHandler.GetPath(Position, exit, PathfindingRadius, UsesFastPath);

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
        /// When clearing the cast (s == null), automatically attempts to fire any buffered spell.
        /// </summary>
        /// <param name="s">Spell that is being cast, or null when the cast ends.</param>
        public void SetCastSpell(Spell s)
        {
            _castingSpell = s;

            if (s == null && _queuedSpellCast != null)
            {
                var queued = _queuedSpellCast;
                _queuedSpellCast = null;

                if (CanCast(queued.Spell) && queued.Spell.State == SpellState.STATE_READY)
                {
                    queued.Spell.Cast(queued.Start, queued.End, queued.TargetUnit);
                }
            }
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
        /// Buffers a spell cast to be fired once the current cast ends.
        /// Only valid when blocked solely by an active cast — CC/silence/etc. should be checked before calling.
        /// Newer input overwrites older input (one-slot buffer).
        /// </summary>
        /// <param name="s">Spell to queue.</param>
        /// <param name="start">Start position of the cast.</param>
        /// <param name="end">End position of the cast.</param>
        /// <param name="unit">Target unit, if any.</param>
        /// <returns>True if the spell was successfully queued; false if conditions don't allow buffering.</returns>
        public bool TryQueueSpell(Spell s, Vector2 start, Vector2 end, AttackableUnit unit)
        {
            if (_castingSpell == null)
            {
                return false;
            }

            if (!Status.HasFlag(StatusFlags.CanCast)
                || Status.HasFlag(StatusFlags.Charmed)
                || Status.HasFlag(StatusFlags.Feared)
                || Status.HasFlag(StatusFlags.Pacified)
                || Status.HasFlag(StatusFlags.Silenced)
                || Status.HasFlag(StatusFlags.Sleep)
                || Status.HasFlag(StatusFlags.Stunned)
                || Status.HasFlag(StatusFlags.Suppressed)
                || Status.HasFlag(StatusFlags.Taunted))
            {
                return false;
            }

            _queuedSpellCast = new SpellQueueEntry(s, start, end, unit);
            return true;
        }

        /// <summary>
        /// Discards any buffered spell cast.
        /// Call this on move orders, CC application, death, or any state that should cancel buffered input.
        /// </summary>
        public void ClearQueuedSpell()
        {
            _queuedSpellCast = null;
        }

        /// <summary>
        /// Forces this unit to stop targeting the given unit.
        /// Applies to attacks, spell casts, spell channels, and any queued spell casts.
        /// </summary>
        /// <param name="target"></param>
        public void Untarget(AttackableUnit target)
        {
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
        public void SetTargetUnit(AttackableUnit target, bool networked = false, bool keepLostTarget = false)
        {
            if (TargetUnit == target)
            {
                // Even on a no-op (target == current), honor the LostTarget reset so callers
                // that defensively pass null while TargetUnit is already null still cancel the
                // last-known-location chase. Without this, HandleMove's trailing
                // SetTargetUnit(null) for a player MoveTo never reaches the LostTarget = null
                // line below and the chase logic later overwrites the player's waypoints.
                if (!keepLostTarget && LostTarget != null)
                {
                    LostTarget = null;
                }
                return;
            }
            bool wasTargetingChampion = TargetUnit is Champion;
            if (target == null && TargetUnit != null)
            {
                ApiEventManager.OnTargetLost.Publish(this, TargetUnit);
            }

            TargetUnit = target;
            // Target swap or loss invalidates the engagement-range hysteresis — the next
            // chase decision must re-cross the tight trigger threshold to engage.
            _engagedWithTarget = false;
            // Default: any explicit SetTargetUnit drops the last-known-location chase (the
            // player picked something new, or the AI re-acquired). UpdateTarget passes
            // keepLostTarget=true when it is itself transitioning the unit *into* the
            // last-known chase (target lost to fog).
            if (!keepLostTarget)
            {
                LostTarget = null;
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
        public void SwapSpells(byte slot1, byte slot2)
        {
            if (Spells[slot1].CastInfo.IsAutoAttack || Spells[slot2].CastInfo.IsAutoAttack)
            {
                return;
            }

            var slot1Name = Spells[slot1].SpellName;
            var slot2Name = Spells[slot2].SpellName;

            var enabledBuffer = Stats.GetSpellEnabled(slot1);
            var buffer = Spells[slot1];
            Spells[slot1] = Spells[slot2];

            Spells[slot2] = buffer;

            Spells[slot1].CastInfo.SpellSlot = slot1;
            Spells[slot2].CastInfo.SpellSlot = slot2;

            Stats.SetSpellEnabled(slot1, Stats.GetSpellEnabled(slot2));
            Stats.SetSpellEnabled(slot2, enabledBuffer);

            if (this is Champion champion)
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
            return _lastPetSpawned;
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
            base.OnAfterSync();
            TryPostActivateCharScript();
            TryPostActivateSpellScripts();
        }

        public override void Update(float diff)
        {
            if (delayedSpellPackets.Count > 0) invisSent = true;
            if (_postAttackMoveDelayRemainingMs > 0f)
            {
                _postAttackMoveDelayRemainingMs = Math.Max(0f, _postAttackMoveDelayRemainingMs - diff);
            }
            base.Update(diff);
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

            // bit of a hack
            foreach (var s in new List<Spell>(Spells.Values))
            {
                s.Update(diff);
            }

            if (Inventory != null)
            {
                Inventory.OnUpdate(diff);
            }

            UpdateAssistMarkers();
            UpdateTarget();

            if (_autoAttackCurrentCooldown > 0)
            {
                _autoAttackCurrentCooldown -= diff / 1000.0f;
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
            if (TargetUnit != null && !TargetUnit.Status.HasFlag(StatusFlags.Targetable) && TargetUnit.GetIsTargetableToTeam(Team))
            {
                if (TargetUnit.CharData.IsUseable)
                {
                    return;
                }
                Untarget(TargetUnit);
            }
        }

        public override void TakeDamage(DamageData damageData, DamageResultType damageText, IEventSource sourceScript = null)
        {
            base.TakeDamage(damageData, damageText, sourceScript);

            var attacker = damageData.Attacker;
            var objects = _game.ObjectManager.GetObjects();
            foreach (var it in objects)
            {
                if (it.Value is ObjAIBase u)
                {
                    float acquisitionRange = Stats.AcquisitionRange.Total;
                    float acquisitionRangeSquared = acquisitionRange * acquisitionRange;
                    if (
                        u != this
                        && !u.IsDead
                        && u.Team == Team
                        && u.AIScript.AIScriptMetaData.HandlesCallsForHelp
                        && Vector2.DistanceSquared(u.Position, Position) <= acquisitionRangeSquared
                        && Vector2.DistanceSquared(u.Position, attacker.Position) <= acquisitionRangeSquared
                    )
                    {
                        try
                        {
                            u.AIScript.OnCallForHelp(attacker, this);
                        }
                        catch (Exception e)
                        {
                            _logger.Error(null, e);
                        }
                    }
                }
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
                    CancelAutoAttack(AutoAttackStopReason.OtherImmediately);
                    SetTargetUnit(null, true);
                }
                ClearLostTarget();
                return;
            }

            // Player override: any non-attack movement order cancels the last-known-location
            // hunt. Mirrors the client's AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION semantics
            // where MoveTo/Stop/Hold replaces the chase, while AttackTo/AttackMove preserves
            // it. Without this, the chase block below would re-route the player's manually
            // issued waypoints back toward _lostTargetLastPosition on every UpdateTarget tick.
            if (LostTarget != null
                && MoveOrder != OrderType.AttackTo
                && MoveOrder != OrderType.AttackMove)
            {
                ClearLostTarget();
            }

            // Last-known-location pre-check (mirrors Hero.lua TimerDistanceScan branch
            // for AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION at function 0_16). If a target was
            // lost to fog/stealth, we keep watching it: once it dies, drop the hunt; once
            // it becomes visible+targetable again, re-acquire it as TargetUnit without
            // requiring a new player click.
            if (LostTarget != null)
            {
                if (LostTarget.IsDead || LostTarget.IsToRemove())
                {
                    ClearLostTarget();
                }
                else if (LostTarget.IsVisibleByTeam(Team) && LostTarget.Status.HasFlag(StatusFlags.Targetable))
                {
                    // SetTargetUnit clears LostTarget by default (keepLostTarget=false).
                    SetTargetUnit(LostTarget, true);
                }
                // else: still hidden — fall through to the chase logic below, which will
                // path the unit toward _lostTargetLastPosition.
            }

            // Lua-style LastAutoAttackFinished gate: an AA whose damage has already been
            // dispatched (HasAutoAttacked) but whose recovery animation is still running
            // (state != READY) gets to finish naturally before we touch the target. Mirrors
            // Hero.lua TimerCheckAttack's "if LastAutoAttackFinished() == false then return"
            // wait. Without this, a Stop order or target loss issued during recovery cuts
            // the wind-down animation short. Cheap, safe — recovery completes within a few
            // frames, the next UpdateTarget tick picks up where we left off.
            bool aaRecoveryPending = IsAttacking
                && HasAutoAttacked
                && AutoAttackSpell != null
                && AutoAttackSpell.State != SpellState.STATE_READY;

            if (TargetUnit == null)
            {
                if (aaRecoveryPending) return;

                // Sync the IsAttacking flag once recovery has actually finished (state has
                // gone back to READY but flag was still set from before).
                if (IsAttacking && AutoAttackSpell.State == SpellState.STATE_READY)
                {
                    IsAttacking = false;
                }

                if ((IsAttacking && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp) || HasMadeInitialAttack)
                {
                    // !HasAutoAttacked => damage was never dispatched => hard cancel of the
                    // windup. HasAutoAttacked => damage already out (animation interrupted
                    // mid-recovery), so the chain reset alone suffices.
                    CancelAutoAttack(HasAutoAttacked
                        ? AutoAttackStopReason.OtherFinishAnim
                        : AutoAttackStopReason.OtherImmediately);
                }

                // Last-known-location chase: target was lost to fog and the pre-check above
                // didn't re-acquire (still hidden). Walk toward the last-known position; if
                // we reach it without re-acquiring, give up the hunt.
                if (LostTarget != null)
                {
                    const float ARRIVAL_THRESHOLD_SQ = 25f * 25f;
                    if (Vector2.DistanceSquared(Position, _lostTargetLastPosition) < ARRIVAL_THRESHOLD_SQ)
                    {
                        ClearLostTarget();
                        return;
                    }
                    var lostPath = _game.Map.PathingHandler.GetPath(this, _lostTargetLastPosition);
                    if (lostPath != null && lostPath.Count > 1)
                    {
                        SetWaypoints(lostPath);
                    }
                    return;
                }

                // Auto-acquire-on-stop: idle/stopped unit scans for nearby visible enemies
                // and engages a soft attack. Mirrors Hero.lua TimerDistanceScan branches at
                // function 0_16 (AI_STANDING/IDLE and AI_MOVE+IsMovementStopped). Gated by
                // the per-champion AutoAcquireTargetEnabled (= the client's "Auto Attack"
                // game option, pushed via packet 0x47 / C2S_UpdateGameOptions). We avoid
                // scanning when the player is actively attack-moving — that path has its
                // own (broader) acquire pass below.
                if (AutoAcquireTargetEnabled
                    && IsPathEnded()
                    && _autoAttackCurrentCooldown <= 0f
                    && MoveOrder != OrderType.AttackMove
                    && MoveOrder != OrderType.AttackTo
                    && MoveOrder != OrderType.AttackTerrainOnce
                    && MoveOrder != OrderType.AttackTerrainSustained)
                {
                    var acquired = FindAutoAcquireTarget();
                    if (acquired != null)
                    {
                        SetTargetUnit(acquired, true);
                        UpdateMoveOrder(OrderType.AttackTo, true);
                        return;
                    }
                }
            }
            else if (TargetUnit.IsDead)
            {
                if (aaRecoveryPending) return;

                if (IsAttacking && !HasAutoAttacked)
                {
                    CancelAutoAttack(AutoAttackStopReason.TargetLost);
                }

                // Hard clear — death is permanent, no last-known chase.
                SetTargetUnit(null, true);
                return;
            }
            else if (!TargetUnit.Status.HasFlag(StatusFlags.Targetable) && TargetUnit.CharData.IsUseable)
            {
                if (aaRecoveryPending) return;

                if (IsAttacking && !HasAutoAttacked)
                {
                    CancelAutoAttack(AutoAttackStopReason.TargetLost);
                }

                // Useable units that go untargetable (Zhonya, etc.) — hard clear, the chase
                // semantics differ from fog (target is right there, just immune).
                SetTargetUnit(null, true);
                return;
            }
            else if (!TargetUnit.IsVisibleByTeam(Team))
            {
                if (aaRecoveryPending) return;

                if (IsAttacking && !HasAutoAttacked)
                {
                    CancelAutoAttack(AutoAttackStopReason.TargetLost);
                }

                // LOST_VISIBILITY — target is alive and targetable but slipped into fog or
                // stealth. Switch to last-known-location chase: remember the target and its
                // last position, drop TargetUnit but keep LostTarget so the pre-check above
                // can re-acquire on the next UpdateTarget once the target reappears. Mirrors
                // OnTargetLost(LOST_VISIBILITY, target) -> AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION
                // in Hero.lua function 0_3.
                SetLostTarget(TargetUnit);
                SetTargetUnit(null, networked: true, keepLostTarget: true);
                return;
            }
            else if (IsAttacking)
            {
                // Mid-windup cancel buffer — sourced from the CVar so it can be tuned at the
                // Globals.ini level (ar_ClosingAttackRangeModifier, default 300). Wider than
                // the StopAttackRangeModifier hold range above so the in-flight AA only gets
                // killed when the target really runs away, not on small drift.
                float cancelBuffer = GlobalData.AttackRangeVariables.ClosingAttackRangeModifier;
                float maxCancelRange = Stats.Range.Total + TargetUnit.CollisionRadius + CollisionRadius + cancelBuffer;
                if (Vector2.Distance(TargetUnit.Position, Position) > maxCancelRange
                        && AutoAttackSpell.State == SpellState.STATE_CASTING && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                {
                    // !HasAutoAttacked => damage was never dispatched => hard cancel of the
                    // windup. HasAutoAttacked => damage already out, soft cancel (recovery
                    // animation gets stopped client-side via NotifyStop, but cooldown keeps
                    // running so the next AA isn't free).
                    CancelAutoAttack(HasAutoAttacked
                        ? AutoAttackStopReason.OtherFinishAnim
                        : AutoAttackStopReason.OtherImmediately);
                }

                if (AutoAttackSpell.State == SpellState.STATE_READY)
                {
                    IsAttacking = false;
                }
                return;
            }

            var idealRange = Stats.Range.Total;
            if (SpellToCast != null && !IsAttacking && (TargetUnit == null || SpellToCast.SpellData.IsValidTarget(this, TargetUnit)))
            {
                // Spell casts usually do not take into account collision radius, thus range is center -> center VS edge -> edge for attacks.
                idealRange = SpellToCast.GetCurrentCastRange();

                if (MoveOrder == OrderType.AttackTo
                    && TargetUnit != null
                    && Vector2.DistanceSquared(TargetUnit.Position, SpellToCast.CastInfo.Owner.Position) <= idealRange * idealRange)
                {
                    SpellToCast.Cast(new Vector2(SpellToCast.CastInfo.TargetPosition.X, SpellToCast.CastInfo.TargetPosition.Z), new Vector2(SpellToCast.CastInfo.TargetPositionEnd.X, SpellToCast.CastInfo.TargetPositionEnd.Z), TargetUnit);
                }
                else if (MoveOrder == OrderType.MoveTo
                        && Vector2.DistanceSquared(new Vector2(SpellToCast.CastInfo.TargetPosition.X, SpellToCast.CastInfo.TargetPosition.Z), SpellToCast.CastInfo.Owner.Position) <= idealRange * idealRange)
                {
                    SpellToCast.Cast(new Vector2(SpellToCast.CastInfo.TargetPosition.X, SpellToCast.CastInfo.TargetPosition.Z), new Vector2(SpellToCast.CastInfo.TargetPositionEnd.X, SpellToCast.CastInfo.TargetPositionEnd.Z));
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
                    idealRange = Stats.Range.Total + TargetUnit.CollisionRadius + CollisionRadius;

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
                    if (MoveOrder == OrderType.AttackMove)
                    {
                        // Wait for the previous AA animation to finish before re-scanning.
                        // Equivalent to Lua TimerCheckAttack's LastAutoAttackFinished() gate
                        // (Hero.lua function 0_15). Previously this gated on
                        // _autoAttackCurrentCooldown > 0, which is more conservative — cooldown
                        // runs 1/AttackSpeed seconds, longer than the recovery animation, so
                        // the champion would idle 200-400 ms longer than the client AI between
                        // target-death and the next acquire scan. The animation-state gate
                        // matches client cadence and feels more responsive.
                        if (IsAttacking
                            && AutoAttackSpell != null
                            && AutoAttackSpell.State != SpellState.STATE_READY)
                        {
                            return;
                        }

                        var objects = _game.ObjectManager.GetObjects();
                        var distanceSqrToTarget = 25000f * 25000f;
                        AttackableUnit nextTarget = null;
                        // Previously `Math.Max(Stats.Range.Total, Stats.AcquisitionRange.Total)` which is incorrect
                        var range = Stats.AcquisitionRange.Total;

                        foreach (var it in objects)
                        {
                            if (!(it.Value is AttackableUnit u) ||
                                u.IsDead ||
                                u.Team == Team ||
                                Vector2.DistanceSquared(Position, u.Position) > range * range ||
                                !u.Status.HasFlag(StatusFlags.Targetable))
                            {
                                continue;
                            }

                            if (!(Vector2.DistanceSquared(Position, u.Position) < distanceSqrToTarget))
                            {
                                continue;
                            }

                            distanceSqrToTarget = Vector2.DistanceSquared(Position, u.Position);
                            nextTarget = u;
                        }

                        if (nextTarget != null)
                        {
                            SetTargetUnit(nextTarget, true);
                        }
                    }

                    if (AutoAttackSpell != null && AutoAttackSpell.State == SpellState.STATE_READY && IsAttacking)
                    {
                        IsAttacking = false;
                        HasMadeInitialAttack = false;
                    }
                }
            }
        }

        /// <summary>
        /// Sets this unit's move order to the given order.
        /// </summary>
        /// <param name="order">MoveOrder to set.</param>
        public void UpdateMoveOrder(OrderType order, bool publish = true)
        {
            if (publish)
            {
                // Return if scripts do not allow this order.
                if (!ApiEventManager.OnUnitUpdateMoveOrder.Publish(this, order))
                {
                    return;
                }
            }

            MoveOrder = order;

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
                ClearQueuedSpell();
            }
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
    }
}
