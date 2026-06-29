using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
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

        // Lost-target "go to last known location" (Riot BASEAITASK_GotoUnitsLastSpot_Attack, Champion-only).
        // Set when a hard-engaged champion's target leaves vision (TargetLostReason.LostVisibility); the
        // active TargetUnit is still cleared, but the lost unit + its last-seen position are remembered so
        // the AI can walk there and re-acquire on sight. Consumed by the GetLostTargetIfVisible / IsTargetLost
        // primitives + HeroAI (P2/P3, docs/LOST_TARGET_REACQUISITION_PLAN.md).
        private AttackableUnit _lostTargetUnit;
        private Vector2 _lostTargetLastKnownPosition;

        /// <summary>
        /// Game-time (ms) at which the post-attack movement-issue lockout expires. Set by
        /// `Spell.FinishCasting` for AA when <see cref="CharData.PostAttackMoveDelay"/> &gt; 0.
        /// While `_game.GameTime &lt; _postAttackMoveLockEndsMs`, <see cref="CanIssueMoveOrders"/>
        /// returns false → server rejects move-orders. Mirrors Riot's per-character anti-kite
        /// stat. Most champions have PostAttackMoveDelay = 0 → no lockout, no behavior change.
        /// </summary>
        private float _postAttackMoveLockEndsMs;

        private bool _skipNextAutoAttack;
        // Re-path throttle for the chase branch of RefreshWaypoints: the full A* + GetClosestAttackPoint
        // recompute is skipped while we already have a live path toward roughly the same target spot.
        // Riot recomputes the approach on the 0.25s brain sweep / when the target drifts, not every
        // frame. NetId 0 / sentinel position force a recompute on the first chase tick.
        private uint _repathTargetNetId;
        private Vector2 _repathTargetPos = new Vector2(float.NaN, float.NaN);
        private const float REPATH_TARGET_DRIFT = 75.0f;
        // Lane-minion chase commit window (anti-wobble, 2026-06-21): with the stage-B actor-aware
        // chase path, the route around MOVING allies changes slightly each recompute and the cell can
        // be contested (partial/short path → instant IsPathEnded → immediate recompute). Left ungated
        // that recomputed ~7×/s, and each 0x61 re-orients the client → visible wobble/"spin" for the
        // most-constrained (last) minion. Pace SAME-TARGET path-ran-out recomputes to this interval
        // (≈ the 0.25s brain sweep) so the minion commits to its routed path; a target switch or >75u
        // drift still recomputes immediately.
        private float _lastChaseRepathMs = float.NegativeInfinity;
        private const float MIN_CHASE_REPATH_MS = 250.0f;

        // Stop+Hold reconciliation: pressing the Hold key sends AI_STOP immediately followed by
        // AI_HOLD (~65ms apart, verified on the wire), and the Hold packet does NOT carry the held
        // target's NetID. The leading Stop clears the target; we remember it here so the trailing
        // Hold can restore it — Hold semantics are "keep current target, clear path, don't chase".
        private AttackableUnit _stopClearedTarget;
        private float _stopClearedTimeMs = float.NegativeInfinity;

        // Set by RefreshWaypoints when the path to the attack target comes back unreachable;
        // consumed (published as OnPathToTargetBlocked) at the top of the next Update so the AI
        // reacts without re-entering the pathing pass. See ApiEventManager.OnPathToTargetBlocked.
        private bool _pathToTargetBlocked;

        // CHASE INTENT (Order/State-Split P5 decouple — docs/AI_ORDER_STATE_SPLIT_PLAN.md): "is this unit
        // actively chasing/tracking its TargetUnit" = Riot's Actor.TrackUnitID concept, conceptually distinct
        // from TargetUnit (the attack target) and from MoveOrder (the input order). RefreshWaypoints reads
        // THIS to decide chase-vs-stand instead of MoveOrder==AttackTo. SLICE 2 (explicit field, still
        // behaviour-neutral): kept in sync with the AttackTo order in UpdateMoveOrder — every AttackTo-set
        // goes through there (the player attack-order AND the engine auto-engage). The only direct MoveOrder
        // assignments are ctor-time MoveTo/Hold/Stop (LaneMinion/Minion), where this is already false → no
        // staleness. A later slice sets it DIRECTLY on combat-engage, decoupling it from the MoveOrder=AttackTo
        // mutation, after which MoveOrder stops being the combat-movement driver.
        private bool _chaseIntent;
        protected bool ChaseIntent => _chaseIntent;

        // ORDER STATUS (IssueOrders state machine, S2 — docs/ISSUE_ORDERS_STATE_MACHINE_PLAN.md): Riot's
        // order_status_e lifecycle (CLEAR/PENDING/POSTPONED/EXECUTED) tracked by IssueOrders::savedOrderStatus.
        // PHASE 1 (neutral scaffold): set on UpdateMoveOrder (PENDING) + SetSpellToCast (POSTPONED = move-to-
        // cast). Nothing reads it for control yet → 0 behaviour change. Phase 2 adds TryToExecuteOrder/
        // ExecuteOrder/RouteOrder so non-executable orders actually wait as POSTPONED + retry per tick.
        public OrderState OrderStatus { get; private set; } = OrderState.Clear;

        // Saved-order record mirroring Riot IssueOrders::savedOrderPos / savedOrderObj (the order command
        // itself ≡ our MoveOrder = savedOrderCmd). Recorded by HandleNewOrder at the issue point. NOT read
        // for control in Phase 2 — Phase 3's per-tick RouteOrder retry of a POSTPONED order re-reads these
        // to re-execute it. Kept here purely as the faithful saved-order tuple until then.
        private Vector2 _savedOrderPos = Vector2.Zero;
        private AttackableUnit _savedOrderObj;

        // Postponed move-to-cast target (Riot mPostponedSpell.mTargetID / a TEMP_CASTSPELL order's
        // savedOrderObj), kept SEPARATE from the attack target (TargetUnit ≈ Riot mEnemyID). A TARGETED
        // move-to-cast stores its target HERE — not in TargetUnit — so the caster chases it to cast range
        // while the cast target NEVER becomes an auto-attack target (the AutoAttackComponent fires off
        // TargetUnit only). Riot does the same: PostponeSpell sets mPostponedSpell but never SetEnemyID, so
        // the auto-attack (which reads mEnemyID) ignores it. Cleared together with SpellToCast in
        // SetSpellToCast(null) (cast finished or cancelled). Null = no pending targeted move-to-cast.
        public AttackableUnit PostponedCastTarget { get; private set; }

        // The unit currently being chased/tracked (Riot Actor.TrackUnitID): the attack TargetUnit normally,
        // the postponed cast target during a targeted move-to-cast. RefreshWaypoints chases THIS. Equals
        // TargetUnit whenever no move-to-cast is pending, so the normal chase path is unchanged.
        private AttackableUnit ChaseTrackUnit => PostponedCastTarget ?? TargetUnit;

        // ---------------- Crowd-control movement plumbing (B.1) ----------------
        // Riot's model: a CC buff only sets the status FLAG; the per-unit AI script drives the
        // wander/flee/taunt movement (Hero.lua for champions, Aggro/Minion.lua for minions). The
        // AI layer needs to know who applied the CC, so the buff records it here on apply and the
        // shared CrowdControlComponent reads it. See project_cc_model_architecture.

        /// <summary>Unit that applied the active crowd control (fear/flee source, taunter). Set by
        /// the CC buff on apply; read by the AI's CrowdControlComponent. Null when not CC'd.</summary>
        public AttackableUnit CrowdControlSource { get; set; }

        /// <summary>Fear flavour for <see cref="CrowdControlSource"/>: true = wander randomly around
        /// the leash point (Riot AI_FEARED), false = flee directly away from the source (AI_FLEEING).</summary>
        public bool CrowdControlWander { get; set; }

        /// <summary>
        /// Attack-move destination point (the clicked ground spot), kept separate from the chase
        /// path so the unit can RESUME walking toward it after a target acquired along the way dies
        /// or leaves range. Mirrors Riot's AssignTargetPosInPos / ClearTargetPosInPos (Hero.lua) and
        /// the FindTargetOrMove → SetStateAndMoveToForwardNav resume (Minion.lua). Vector2.Zero = none.
        /// Order/State split: the a-move state machine (acquire → soft-drop → resume) now lives in the
        /// HeroAI script, driven by <c>_aiState == AI_SOFTATTACK</c>; this is just the stored destination.
        /// </summary>
        public Vector2 AttackMoveDestination
        {
            get => _attackMoveDestination;
            set => _attackMoveDestination = value;
        }
        private Vector2 _attackMoveDestination = Vector2.Zero;
        private Spell _castingSpell;
        private Spell _lastAutoAttack;
        private Spell _lastOverrideAutoAttack;
        private SpellQueueEntry _queuedSpellCast;
        private readonly Random _random = new Random();
        private readonly List<Spell> _autoAttackOverrideSpells = new List<Spell>();
        // Reused scratch buffer for the per-tick spell update so we don't allocate a new List every
        // tick per unit (see SpellsUpdate). The defensive copy is still needed because a spell's
        // Update can add/remove entries in Spells.
        private readonly List<Spell> _spellUpdateBuffer = new List<Spell>();
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
        /// The player's "Auto Acquire Target" option (Riot OPT_AutoAcquireTarget, default true),
        /// pushed from the client via <c>C2S_UpdateGameOptions</c>. When enabled, the engine lets a
        /// champion that is in a soft idle (after a move/attack completes) auto-acquire the nearest
        /// enemy in acquisition range — mirrors Riot's <c>IsAutoAcquireTargetEnabled()</c> gate
        /// (Hero.lua TimerDistanceScan, AI_STANDING/AI_IDLE). A hard stop (S-key) does NOT auto-acquire.
        /// Not consumed by minions/turrets/pets (they have their own AI).
        /// </summary>
        public bool AutoAcquireTargetEnabled { get; set; } = true;

        /// <summary>True while a normal (non-channel) spell cast is in progress. Read accessor for AI
        /// scripts (the field is private); used by HeroAI's relocated combat selection.</summary>
        public bool IsCasting => _castingSpell != null;

        /// <summary>True while the auto-attack is on cooldown. Read accessor for AI scripts (the field
        /// is private); used by HeroAI's relocated attack-move acquire (don't re-acquire mid-cooldown).</summary>
        public bool IsAutoAttackOnCooldown => _autoAttackCurrentCooldown > 0;

        /// <summary>
        /// Set by the AI script (HeroAI) when it owns champion combat SELECTION — Order/State split
        /// Phase 2: idle auto-acquire (2a) + attack-move acquire / soft-drop / resume (2b) moved into
        /// the champion brain. The matching engine UpdateTarget blocks are skipped for such units so
        /// selection isn't done twice. Non-HeroAI units (bots/pets) keep the engine path until migrated.
        /// </summary>
        public bool ScriptOwnsCombatSelection { get; set; }

        // The script-controlled auto-attack toggle (TurnOnAutoAttack/TurnOffAutoAttack — Riot's "target
        // set" != auto-fire). The engine only swings while this is on for the current target; the shared
        // AutoAttackComponent (attached to every BaseAIScript) drives it by range. P5.6 removed the former
        // `ScriptOwnsAutoAttack` compat flag — every archetype is migrated, so the toggle is now the
        // universal gate and the legacy "auto-fire on target+range" path is gone. Per-swing timing stays
        // engine-owned once toggled on. _autoAttackEnabledTarget pins the toggle to a specific unit so a
        // stale enable can't fire at a newly-set target the script hasn't turned on yet.
        private bool _autoAttackEnabled;
        private AttackableUnit _autoAttackEnabledTarget;

        private AttackableUnit _goldRedirectTarget;
        /// <summary>
        /// The unit this unit's gold/credit redirects to (Riot GetGoldRedirectTarget). Set on
        /// autonomous pets to their summoner (so kills credit the owner and the pet's AI leashes /
        /// acquires from the owner's perspective), and usable by gold-share items (Relic Shield /
        /// Spoils of War). Assigning it replicates PKT_UpdateGoldRedirectTarget (0x7). null = none.
        /// </summary>
        public AttackableUnit GoldRedirectTarget
        {
            get => _goldRedirectTarget;
            set
            {
                _goldRedirectTarget = value;
                _game.PacketNotifier.NotifyUpdateGoldRedirectTarget(this, value);
            }
        }

        /// <summary>
        /// Variable storing all the data related to this AI's current auto attack. *NOTE*: Will be deprecated as the spells system gets finished.
        /// </summary>
        public Spell AutoAttackSpell { get; protected set; }
        /// <summary>
        /// Stable per-life missile NetID this unit reuses for EVERY auto-attack, lazily allocated on the
        /// first AA. Replay-verified (26ec2d65): heroes get a fresh missile NetID per auto-attack, but
        /// non-hero units (minions, pets like Tibbers) reuse a SINGLE NetID for all their AAs across one
        /// life. Handing the client a new NetID each swing made it spawn a fresh phantom AA-missile per
        /// attack, whose instant melee "arrival" re-rendered the hit FX at the swing's wind-down (the
        /// pet double-hit-FX bug). 0 = not yet allocated.
        /// </summary>
        public uint AutoAttackMissileNetId { get; set; }
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
        /// Whether the next auto attack misses (rolled against <see cref="StatsNS.Stats.MissChance"/>
        /// at windup, alongside <see cref="IsNextAutoCrit"/>). Baked into the wire CastInfo's HitResult
        /// (HIT_Miss) at cast time so in-flight missiles resolve the cast-time roll, not a later re-roll.
        /// Blind raises MissChance to 1.0 → always true.
        /// </summary>
        public bool IsNextAutoMiss { get; protected set; }
        /// <summary>
        /// Whether the next auto attack is DODGED by its target (rolled against the TARGET's
        /// <see cref="StatsNS.Stats.Dodge"/> in <see cref="RollDodge"/> at windup, alongside crit/miss).
        /// Baked into the wire CastInfo's HitResult (HIT_Dodge) at cast time so in-flight missiles resolve
        /// the cast-time roll. Bypassed when this attacker has <see cref="DodgePiercing"/>.
        /// </summary>
        public bool IsNextAutoDodged { get; protected set; }
        /// <summary>
        /// When true, this unit's auto attacks CANNOT be dodged (Riot CharacterState DodgePiercing,
        /// set by <c>BBSetDodgePiercing</c>; many empowered/spell attacks set it). Checked in <see cref="RollDodge"/>.
        /// </summary>
        public bool DodgePiercing { get; set; }
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
        /// <summary>
        /// Radius within which this unit's death shares experience to enemy champions. Defaults to the
        /// engine-wide <c>ai_ExpRadius2</c> (1600); lane minions override it to their map-script value
        /// (SR = 1400, 4.20 LevelScript.lua EXP_GIVEN_RADIUS) — that is a per-minion give-radius distinct
        /// from the engine default, which still applies to champions and other units.
        /// </summary>
        public float ExperienceGiveRadius { get; set; } = LeagueSandbox.GameServer.Content.GlobalData.ObjAIBaseVariables.ExpRadius2;
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
            // Seed the model/skin stack's base with the real spawn skinID (set after the base ctor).
            CharacterDataStack.OverwriteBaseSilently(Model, (uint)SkinID);
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

                // Riot layout: 61 = use-spell, 62 = passive (no respawn slot exists at Riot).
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
                    using var _scope = Profiler.Scope($"script:{AIScript.GetType().Name}.OnActivate", "scripts");
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
                    using var _scope = Profiler.Scope($"script:{CharScript.GetType().Name}.OnActivate", "scripts");
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
                using var _scope = Profiler.Scope($"script:{CharScript.GetType().Name}.OnPostActivate", "scripts");
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
        /// <param name="wireHitResult">The HitResult baked into the attack's wire CastInfo
        /// at cast time (CastInfo.Targets[0].HitResult). MUST be passed by every delayed
        /// hit (missiles): IsNextAutoCrit is re-rolled when the NEXT attack's windup
        /// begins, so a ranged attack still in flight would otherwise read the next
        /// attack's roll — crit FX (driven by the cast-time value via crit-spell selection
        /// and the wire byte) and damage then disagree. Null = read the live flag
        /// (instant hits only).</param>
        public virtual void AutoAttackHit(AttackableUnit target, HitResult? wireHitResult = null)
        {
            if (target == null || target.IsDead)
            {
                return;
            }

            // Miss (Blind raises MissChance to 1.0, but any miss-chance debuff applies) — resolved from
            // the cast-time roll baked into the wire HitResult so an in-flight missile agrees with the
            // FX selected at cast, not a later re-roll. Deals 0 damage + RESULT_MISS, no on-hit effects.
            bool isMiss = wireHitResult.HasValue
                ? wireHitResult.Value == HitResult.HIT_Miss
                : IsNextAutoMiss;
            if (isMiss)
            {
                target.TakeDamage(this, 0, DamageType.DAMAGE_TYPE_PHYSICAL,
                                             DamageSource.DAMAGE_SOURCE_ATTACK,
                                             DamageResultType.RESULT_MISS);
                return;
            }

            // Dodge (target-side): the target evaded this attack. Like miss, deals 0 damage with its own
            // result (RESULT_DODGE → "Dodge!" client text). Resolved from the cast-time roll baked into the
            // wire HitResult so an in-flight missile agrees with the cast. TakeDamage publishes
            // OnDodge/OnBeingDodged off RESULT_DODGE (no need to publish here).
            bool isDodge = wireHitResult.HasValue
                ? wireHitResult.Value == HitResult.HIT_Dodge
                : IsNextAutoDodged;
            if (isDodge)
            {
                target.TakeDamage(this, 0, DamageType.DAMAGE_TYPE_PHYSICAL,
                                             DamageSource.DAMAGE_SOURCE_ATTACK,
                                             DamageResultType.RESULT_DODGE);
                return;
            }

            var damage = Stats.AttackDamage.Total;

            // Apply crit to the raw Damage BEFORE building damageData, so the sink mitigates the crit-inclusive value
            bool isCrit = wireHitResult.HasValue
                ? wireHitResult.Value == HitResult.HIT_Critical
                : IsNextAutoCrit;
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
                // PostMitigationDamage is computed in the central TakeDamage sink (mitigation moved there).
                DamageSource = DamageSource.DAMAGE_SOURCE_ATTACK,
                DamageType = DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageResultType = isCrit ? DamageResultType.RESULT_CRITICAL : DamageResultType.RESULT_NORMAL
            };

            target.TakeDamage(damageData, isCrit);
        }

        public override bool CanMove()
        {
            // Zombies (Karthus Death Defied etc.) move via the normal branch below — under Model B a
            // zombie has IsDead=false, so no special-casing is needed here. Death does not clear the
            // CanMove capability (only CC does), so a script opts a zombie OUT by clearing CanMove.
            return (!IsDead
                && MovementParameters != null)
                || (Status.HasFlag(StatusFlags.CanMove) && Status.HasFlag(StatusFlags.CanMoveEver)
                && (MoveOrder != OrderType.CastSpell && _castingSpell == null)
                && (ChannelSpell == null || (ChannelSpell != null && ChannelSpell.SpellData.CanMoveWhileChanneling))
                && (!IsAttacking || !AutoAttackSpell.SpellData.CantCancelWhileWindingUp));
                // M2 Phase 3: movement-disabling CC (stun/root/net/sleep/suppress) now clears the CanMove
                // capability via BuffType.ToCapabilityDisable, so the explicit CC-flag list is gone.
        }

        public override bool CanChangeWaypoints()
        {
            // Under Model B a zombie has IsDead=false, so !IsDead already permits re-pathing it.
            return !IsDead
                && (MovementParameters == null || (MovementParameters != null && MovementParameters.FollowNetID != 0))
                && _castingSpell == null
                && (ChannelSpell == null || (ChannelSpell != null && !ChannelSpell.SpellData.CantCancelWhileChanneling));
        }
        /// <summary>
        /// Engages the post-attack move-issue lockout for <paramref name="delaySec"/> seconds.
        /// While active, <see cref="CanIssueMoveOrders"/> returns false. Negative or zero
        /// values clear the lockout immediately. Called from <c>Spell.FinishCasting</c> when
        /// an AA fires, using <see cref="CharData.PostAttackMoveDelay"/>.
        /// </summary>
        public void EngagePostAttackMoveLock(float delaySec)
        {
            if (delaySec <= 0f)
            {
                _postAttackMoveLockEndsMs = 0f;
                return;
            }
            _postAttackMoveLockEndsMs = _game.GameTime + delaySec * 1000f;
        }


        public bool CanIssueMoveOrders()
        {
            // Under Model B a zombie has IsDead=false, so it passes this gate by default and may be
            // ordered to move. A script clearing the CanMove capability (immobile ghosts, e.g.
            // Karthus) is still rejected by the CanMove check further down.
            if (IsDead)
                return false;

            if (IgnoreMoveOrders)
                return false;

            // PostAttackMoveDelay lockout — server rejects move-issue while the AA-fire
            // post-window is still active. Most champions have PostAttackMoveDelay = 0 →
            // _postAttackMoveLockEndsMs stays at 0 → never blocks.
            if (_postAttackMoveLockEndsMs > _game.GameTime)
                return false;

            if (!Status.HasFlag(StatusFlags.CanMoveEver))
                return false;

            // Reject move-order issuance under any movement-disabling CC. Stun/snare/net/sleep/suppress
            // clear the CanMove CAPABILITY (BuffType.ToCapabilityDisable), so !CanMove covers them. Charm/
            // fear/taunt are listed explicitly: they do NOT clear CanMove (the AI drives the unit — flee /
            // walk to charmer / walk to taunter), but the PLAYER still must not be able to issue move orders.
            if (!Status.HasFlag(StatusFlags.CanMove)
                || Status.HasFlag(StatusFlags.Feared)
                || Status.HasFlag(StatusFlags.Taunted)
                || Status.HasFlag(StatusFlags.Charmed))
                return false;

            return true;
        }
        /// <summary>
        /// Whether or not this AI is able to auto attack.
        /// </summary>
        /// <returns></returns>
        public bool CanAttack()
        {
            // M2 Phase 3: attack-disabling CC (stun/disarm/charm/fear/sleep/suppress) clears the CanAttack
            // capability via BuffType.ToCapabilityDisable, so it's covered by the CanAttack bit alone.
            return Status.HasFlag(StatusFlags.CanAttack)
                && _castingSpell == null
                && ChannelSpell == null;
        }

        /// <summary>
        /// Whether or not this AI is able to cast spells.
        /// </summary>
        /// <param name="spell">Spell to check.</param>
        public bool CanCast(Spell spell = null)
        {
            // M2 Phase 3: cast-disabling CC (stun/silence/charm/fear/sleep/suppress/taunt) clears the
            // CanCast capability via BuffType.ToCapabilityDisable, so it's covered by the CanCast bit alone.
            return ApiEventManager.OnCanCast.Publish(this, spell)
                && Status.HasFlag(StatusFlags.CanCast)
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
            // If we have waypoints, but our move order is one of these, we shouldn't move — UNLESS the
            // unit is actively chasing/tracking a target (ChaseIntent). The chase-decouple (P5) put the
            // chase on _chaseIntent over an UNCHANGED MoveOrder, mirroring Riot's movement loop which
            // follows Actor.TrackUnitID order-independently. So an engine-engaged chase that started from
            // an idle / stop / taunt order (MoveOrder left stale) must still walk its chase path; gating
            // these orders on !ChaseIntent keeps the no-move behaviour for genuinely non-chasing units.
            // CastSpell stays an UNCONDITIONAL movement lock (never move mid-cast, even while tracking).
            if (MoveOrder == OrderType.CastSpell
                || ((MoveOrder == OrderType.OrderNone
                     || MoveOrder == OrderType.Stop
                     || MoveOrder == OrderType.Taunt)
                    && !ChaseIntent))
            {
                return false;
            }

            return base.Move(diff);
        }

        /// <summary>
        /// Cancels any auto attacks this AI is performing and resets the time between the next auto attack if specified.
        /// </summary>
        /// <param name="reset">Whether or not to reset the delay between the next auto attack.</param>
        /// <param name="fullCancel">Also resets <c>IsAttacking</c> + <c>HasMadeInitialAttack</c> so the
        /// next Update tick treats the unit as fresh — required when handing off to a new target
        /// (e.g. KatarinaE blink): without this, the lingering <c>IsAttacking=true</c> makes
        /// `UpdateTarget` skip the new-target acquisition path and the post-blink AA never fires
        /// damage through the normal pipeline.</param>
        /// <param name="silent">Suppresses the <c>NPC_InstantStop_Attack</c> wire broadcast while
        /// keeping the server-side state reset. Use when the caller wants to do its own scope/timing
        /// for the ISA packet (e.g. blink-spells that gate ISA on whether an AA-windup was active).</param>
        public void CancelAutoAttack(bool reset, bool fullCancel = false, bool silent = false,
            AutoAttackStopReason reason = AutoAttackStopReason.OtherImmediately, bool respectWindupLock = false)
        {
            // Riot OnCancelAttack: fires when an in-progress auto-attack WINDUP is cancelled (the swing
            // hadn't connected yet). Captured before the state is reset below; gated on STATE_CASTING so a
            // cleanup call on an idle unit (or a repeated cancel) doesn't spuriously fire.
            bool wasWindingUp = AutoAttackSpell != null && AutoAttackSpell.State == SpellState.STATE_CASTING;

            // Riot IsCancelBlockedShared: a swing flagged CantCancelWhileWindingUp cannot be INTERRUPTED
            // mid-windup — it runs to its damage point. Opt-in (respectWindupLock) so only genuine interrupt
            // cancels (CC, target-lost) honour it; AA-reset / override-setup / death-teardown still proceed
            // (Riot's ForceStop bypass). No state touched and no OnCancelAttack fired — nothing was cancelled.
            if (respectWindupLock && wasWindingUp && AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
                return;

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
            // forceClient for client-autonomous units (Minion/Monster): their attack loop runs on the
            // client with no server packets and no self-cancel (AIMinionClient.cpp), so a non-forced stop
            // is ignored while the loop is active → the swing keeps animating with no damage. Forcing it
            // breaks the hardcode-attack state. Champions are server-driven per swing, so the default
            // (non-forced) stop is correct for them.
            if ((reset || fullCancel) && !silent)
                _game.PacketNotifier.NotifyNPC_InstantStop_Attack(this, false, forceClient: this is Minion);

            if (wasWindingUp)
                ApiEventManager.OnCancelAttack.Publish(this, reason);
        }

        /// <summary>
        /// Cancels an in-progress basic-attack WINDUP (no damage) — LoL's auto-attack windup-cancel:
        /// the swing only commits at its damage point (windup end = <c>FinishCasting</c>). An attack
        /// still in <see cref="SpellState.STATE_CASTING"/> has not dealt its hit yet, so a hard
        /// attack-disabling CC (stun/suppress/sleep/charm/fear/disarm) landing now interrupts it; an
        /// attack already past the damage point is left alone (it committed). Attacks flagged
        /// <c>CantCancelWhileWindingUp</c> (special uncancellable swings) are never interrupted.
        /// Called from <see cref="AttackableUnit.RecomputeBuffEffects"/> on the CC transition.
        /// </summary>
        public void CancelAutoAttackIfWindingUp()
        {
            if (IsAttacking
                && AutoAttackSpell != null
                && AutoAttackSpell.State == SpellState.STATE_CASTING
                && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
            {
                CancelAutoAttack(reset: true, fullCancel: true);
            }
        }

        /// <summary>
        /// Forces this AI unit to perform a dash which follows the specified AttackableUnit (re-targeting
        /// each tick). This is the engine follow-unit-path force-move primitive — Riot's
        /// <c>Actor_Common::ServerForceFollowUnitPath</c> (the line variant is
        /// <see cref="AttackableUnit.ServerForceLinePath"/>). Script-facing callers use the
        /// ForceMoveToUnit verb in ApiFunctionManager, not this directly.
        /// </summary>
        /// <param name="target">Unit to follow.</param>
        /// <param name="speed">Constant speed that the unit will have during the dash.</param>
        /// <param name="gravity">How much gravity the unit will experience when above the ground while dashing.</param>
        /// <param name="keepFacingLastDirection">Whether or not the unit should maintain the direction they were facing before dashing.</param>
        /// <param name="followTargetMaxDistance">Maximum distance the unit will follow the Target before stopping the dash or reaching to the Target.</param>
        /// <param name="backDistance">Unknown parameter.</param>
        /// <param name="travelTime">Total time (in seconds) the dash will follow the GameObject before stopping or reaching the Target.</param>
        /// <param name="lockActions">Whether or not to prevent movement, casting, or attacking during the duration of the movement.</param>
        /// TODO: Implement Dash class which houses these parameters, then have that as the only parameter to this function (and other Dash-based functions).
        public void ServerForceFollowUnitPath
        (
            AttackableUnit target,
            float speed,
            float gravity = 0,
            bool keepFacingLastDirection = true,
            float followTargetMaxDistance = 0,
            float backDistance = 0,
            float travelTime = 0,
            bool lockActions = true,
            string movementName = "",
            AttackableUnit caster = null,
            ForceMovementOrdersType movementOrdersType = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER
        )
        {
            // Displacement immunity (see AttackableUnit.ServerForceLinePath): an Imobile/epic unit cannot be
            // pulled/dragged by an external follow-force. A self-initiated follow-dash (caster == this) passes.
            if (caster != null && caster != this && (IsDisplacementImmune || IsCrowdControlImmune))
            {
                return;
            }

            if (MovementParameters != null)
            {
                SetForceMovementState(false, MoveStopReason.ForceMovement);
            }

            SetWaypoints(new List<Vector2> { Position, target.Position }, true);

            SetTargetUnit(target, true);

            // TODO: Take into account the rest of the arguments
            MovementParameters = new ForceMovementParameters
            {
                SetStatus = StatusFlags.None,
                ElapsedTime = 0,
                PathSpeedOverride = speed,
                ParabolicGravity = gravity,
                ParabolicStartPoint = Position,
                KeepFacingDirection = keepFacingLastDirection,
                FollowNetID = target.NetId,
                FollowDistance = followTargetMaxDistance,
                FollowBackDistance = backDistance,
                FollowTravelTime = travelTime,
                MovementName = movementName,
                MovementOrdersType = movementOrdersType,
                Caster = caster ?? this
            };

            if (lockActions)
            {
                MovementParameters.SetStatus = StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove;
            }

            // Dashes go over WaypointGroupWithSpeed (0x64) like ServerForceLinePath — replay-verified as
            // Riot's ONLY dash wire (0x64 ×13849 across 38 replays). The previous WaypointListHeroWithSpeed
            // (0x83) is sent 0× by Riot. The follow-target tracking is carried by the SpeedParams the
            // builder reads from MovementParameters (FollowNetID/FollowDistance/FollowBackDistance/
            // FollowTravelTime), set above — so no params are lost by dropping the explicit-arg overload.
            _game.PacketNotifier.NotifyWaypointGroupWithSpeed(this);
            // NOTE: do NOT send MovementDriverReplication (0x3C) here. A follow-target dash replicates
            // fully via the SpeedParams above (FollowNetID/FollowDistance/FollowTravelTime carry the
            // tracking). Riot moves dashes — incl. Zed R — purely over SpeedParams: 0x3C appears 0× in
            // 34 replays (incl. the Zed game), and the homing-driver path is a separate, 4.x-unused
            // mechanism (our NotifyMovementDriverReplication also has the latent MovementTypeID=0 bug).
            // See docs/FORCED_MOVEMENT_REWRITE_PLAN.md / project_forced_movement_rewrite.
            SetForceMovementState(true);

            _movementUpdated = false;
            // TODO: Verify if we want to use NotifyWaypointListWithSpeed instead as it does not require conversions.
        }

        /// <summary>
        /// Honours <see cref="ForceMovementParameters.MovementOrdersType"/> when a forced movement ends
        /// (Riot ForceMovementOrdersType). POSTPONE_CURRENT_ORDER leaves the order intact so the AI brain /
        /// player resumes it next tick (legacy behavior); CANCEL_ORDER means the dash replaced the order, so
        /// the unit goes idle (Stop) when the forced movement ends. The OrdersType was previously accepted
        /// at the API boundary but silently dropped. See docs/FORCED_MOVEMENT_REWRITE_PLAN.md P1b.
        /// </summary>
        public override void SetForceMovementState(bool state, MoveStopReason reason = MoveStopReason.Finished)
        {
            // Capture the policy before base clears MovementParameters.
            bool endingForcedMove = !state && MovementParameters != null;
            var ordersType = MovementParameters?.MovementOrdersType ?? ForceMovementOrdersType.POSTPONE_CURRENT_ORDER;
            var postponedMoveDest = MovementParameters?.PostponedMoveDestination ?? Vector2.Zero;

            base.SetForceMovementState(state, reason);

            if (endingForcedMove)
            {
                if (ordersType == ForceMovementOrdersType.CANCEL_ORDER)
                {
                    UpdateMoveOrder(OrderType.Stop, true);
                }
                else if (ordersType == ForceMovementOrdersType.POSTPONE_CURRENT_ORDER
                         && postponedMoveDest != Vector2.Zero)
                {
                    // Resume the pre-dash walk-to-point (Riot ORDER_STATUS_POSTPONED re-execute). The
                    // destination was snapshotted at dash-begin because it lived in Waypoints, which the
                    // dash cleared. AttackTo needs no snapshot — its TargetUnit survives and the brain
                    // re-acquires. SetWaypoints marks _movementUpdated so the resumed path broadcasts.
                    var path = _game.Map.PathingHandler.GetPath(this, postponedMoveDest);
                    if (path != null && path.Count > 1)
                    {
                        SetWaypoints(path);
                        // Re-issue as the SAME order kind that was postponed: a positional move-to-cast
                        // (TempCastSpell with an active SpellToCast and no cast target — option A) must stay
                        // TempCastSpell so the UpdateTarget retry still fires the cast at cursor range after
                        // the dash; re-issuing MoveTo here would silently drop the postponed cast. A plain
                        // MoveTo resumes as MoveTo. (Only positional reaches here — targeted move-to-cast
                        // never snapshots a destination, it re-chases PostponedCastTarget.)
                        UpdateMoveOrder(
                            SpellToCast != null && PostponedCastTarget == null
                                ? OrderType.TempCastSpell
                                : OrderType.MoveTo,
                            true);
                    }
                }
            }
        }

        /// <summary>
        /// Forces this AI unit to perform a lunge which follows the specified AttackableUnit.
        /// Compatibility wrapper for scripts that distinguish lunges from regular dashes.
        /// </summary>
        /// <param name="target">Unit to follow.</param>
        /// <param name="speed">Constant speed that the unit will have during the lunge.</param>
        /// <param name="gravity">How much gravity the unit will experience when above the ground while lunging.</param>
        /// <param name="keepFacingLastDirection">Whether or not the unit should maintain the direction they were facing before lunging.</param>
        /// <param name="followTargetMaxDistance">Maximum distance the unit will follow the target before stopping or reaching the target.</param>
        /// <param name="backDistance">Additional stopping distance from the target.</param>
        /// <param name="travelTime">Total time (in seconds) the lunge may follow the target before stopping.</param>
        /// <param name="lockActions">Whether or not to prevent movement, casting, or attacking during the duration of the movement.</param>
        /// <param name="movementType">Force movement type. Included for API compatibility.</param>
        public void LungeToTarget
        (
            AttackableUnit target,
            float speed,
            float gravity = 0,
            bool keepFacingLastDirection = true,
            float followTargetMaxDistance = 0,
            float backDistance = 0,
            float travelTime = 0,
            bool lockActions = false,
            ForceMovementType movementType = ForceMovementType.FURTHEST_WITHIN_RANGE
        )
        {
            ServerForceFollowUnitPath(
                target,
                speed,
                gravity,
                keepFacingLastDirection,
                followTargetMaxDistance,
                backDistance,
                travelTime,
                lockActions
            );
        }

        /// <summary>
        /// Function which refreshes this AI's waypoints if they have a target.
        /// </summary>
        /// <summary>
        /// Invalidates the chase re-path throttle so the next <see cref="RefreshWaypoints"/> recomputes
        /// the approach path immediately. Call when the player issues a fresh unit-targeted order.
        ///
        /// Without this, ordering an attack on a target while already walking an earlier MoveTo path
        /// kept following the OLD path until it ran out — the throttle (<c>needRepath</c>) saw no
        /// path-end, no target drift (stationary target), and no target-NetID change (re-attacking
        /// the same target, where <see cref="SetTargetUnit"/> early-returns). The chase branch only
        /// stops unconditionally INSIDE attack range, which is exactly why the stale-path bug showed
        /// only for orders issued while the target was OUT of range.
        /// </summary>
        public void ForceChaseRepath()
        {
            _repathTargetNetId = 0;
            _repathTargetPos = new Vector2(float.NaN, float.NaN);
        }

        /// <summary>
        /// Records the target a player-issued Stop is about to clear, so a Hold order arriving
        /// immediately after (the Stop+Hold pair the client sends for the Hold key) can restore it.
        /// Call right before the Stop clears the target.
        /// </summary>
        public void NoteStopClearedTarget(AttackableUnit target)
        {
            _stopClearedTarget = target;
            _stopClearedTimeMs = _game.GameTime;
        }

        /// <summary>
        /// Returns and consumes the target cleared by a Stop within <paramref name="withinMs"/>,
        /// or null. Used by the Hold handler to restore the held target across the Stop+Hold pair.
        /// </summary>
        public AttackableUnit ConsumeRecentStopClearedTarget(float withinMs)
        {
            var t = _stopClearedTarget;
            _stopClearedTarget = null;
            if (t == null || t.IsDead || _game.GameTime - _stopClearedTimeMs > withinMs)
            {
                return null;
            }
            return t;
        }

        public virtual void RefreshWaypoints(float idealRange)
        {
            if (MovementParameters != null)
            {
                return;
            }

            // NOTE: no CanMove() gate here — a chase under movement-disabling CC is stopped at the
            // SetWaypoints chokepoint (it rejects moving paths while Stun/Root/Sleep/Suppress/Net is
            // active). Gating RefreshWaypoints on the full CanMove() instead broke combat re-pathing
            // (minions clumping / ranged into melee), so the CC block lives only in SetWaypoints.

            if (TargetUnit != null && _castingSpell == null && ChannelSpell == null
                && !ChaseIntent && MoveOrder != OrderType.Hold)
            {
                // Combat-engage: the engine acquired (or kept) an attack target on a unit that is not
                // already chasing, so raise the explicit chase-intent DIRECTLY instead of mutating
                // MoveOrder to AttackTo. This is the Order/State-split decouple (P5): MoveOrder stays the
                // ORDER the player/script issued (MoveTo lane-push, AttackMove, OrderNone idle) while
                // _chaseIntent — Riot's Actor.TrackUnitID "follow this unit" flag — carries the chase, so
                // the engine no longer overloads the player-order enum as its combat driver. Explicit
                // player/script AttackTo still flows through UpdateMoveOrder (which sets _chaseIntent in
                // sync), so this only adds the ENGINE-initiated engage that minions/monsters rely on.
                // Hold excluded: a Holding unit keeps its target but must NOT chase; the in-range
                // auto-attack branch still fires.
                _chaseIntent = true;
            }

            // The attack-chase ends when its target is gone: clear the chase-intent so the underlying
            // order (AttackMove / MoveTo lane-push / idle) resumes its own path instead of lingering
            // "in chase" with nothing to chase (Riot reverts AI_ATTACKTO → the prior state on target
            // loss). Scoped to attack-chase (PostponedCastTarget == null); a postponed targeted
            // move-to-cast keeps its own retry/clear. Explicit player/script AttackTo also lands here,
            // but its target-dead end-state is identical (idle) so this only clears the otherwise-stale
            // flag — no behaviour change for explicit orders.
            if (_chaseIntent && PostponedCastTarget == null && (TargetUnit == null || TargetUnit.IsDead))
            {
                _chaseIntent = false;
            }

            if (SpellToCast != null)
            {
                // Spell casts usually do not take into account collision radius, thus range is center -> center VS edge -> edge for attacks.
                idealRange = SpellToCast.GetCurrentCastRange();
            }

            Vector2 targetPos = Vector2.Zero;

            // The chased unit is the attack target normally, or the postponed cast target during a targeted
            // move-to-cast (Riot Actor.TrackUnitID). Equals TargetUnit when no move-to-cast is pending, so
            // the normal chase is unchanged; during move-to-cast TargetUnit is null and this is the cast target.
            var chaseUnit = ChaseTrackUnit;

            if (ChaseIntent
                && chaseUnit != null
                && !chaseUnit.IsDead)
            {
                targetPos = chaseUnit.Position;
            }

            // No chase target → nothing to move toward here. (P5 chase-decouple: RefreshWaypoints is now
            // PURELY the chase executor — it follows the tracked unit set above, ≈ Riot Actor::
            // TrackTargetUnit. The former a-move / attack-terrain handling here — targetPos = Waypoints.last
            // + the in-range settle `UpdateMoveOrder(Stop)` — was the pre-decouple engine a-move DRIVER and
            // is now removed: a-move/attack-terrain walk their Waypoints via Move(), and their arrival /
            // acquire is owned by the per-archetype scripts (HeroAI clears AttackMoveDestination on
            // IsPathEnded; minions forward-nav), mirroring Hero.lua TimerDistanceScan → NetSetState(AI_STANDING)
            // on arrival. The blocks were unreachable for MoveOrder==AttackMove anyway (RefreshWaypoints is
            // only called with a TargetUnit → auto-promote raises ChaseIntent → not the a-move branch, or with
            // SpellToCast → MoveOrder==TempCastSpell). This removes the last engine MoveOrder mutation in the
            // chase path → MoveOrder is now purely the wire-input order, _chaseIntent (≈ TrackUnitID) the chase.
            if (targetPos == Vector2.Zero)
            {
                return;
            }

            if (ChaseIntent && targetPos != Vector2.Zero)
            {
                // In attack range → STOP and attack in place; out of range → chase to the distinct
                // GetClosestAttackPoint cell.
                //
                // REMOVED the ATTACK_SETTLE_HYSTERESIS hold-band (2026-06-21, AADIAG-confirmed bug):
                // it let a "settled" unit HOLD anywhere within idealRange + 150, i.e. it would stand
                // still in the (idealRange, idealRange+150] dead-zone — OUT of actual attack range,
                // so neither attacking NOR closing in. Repro: attack-move a target, engage, walk out,
                // attack-move the same target again → she re-acquires already "settled", the target
                // sits ~58u past range, and she holds there forever ("stops at attack range, doesn't
                // attack"; AADIAG: dist=618 ideal=560 settled pathEnded notAttacking). A unit out of
                // attack range MUST re-chase; anti-churn on the re-chase is the REPATH_TARGET_DRIFT
                // throttle in the chase branch, not a positional hold-band.
                if (Vector2.DistanceSquared(Position, targetPos) <= idealRange * idealRange)
                {
                    // In attack range → STOP and attack in place. NETWORKED stop (replay-verified,
                    // 26ec2d65): Riot broadcasts a 1-waypoint WaypointGroup at the chase→attack
                    // transition; StopMovement early-outs at Count==1.
                    //
                    // NOTE (2026-06-21): a "keep walking to the committed distinct cell while in range"
                    // variant was tried to fan allied minions apart, but it re-created the BEHIND-WAVE
                    // failure (clash7/9/22): the committed chase path to a stand cell near a backline
                    // target routes AROUND the enemy front (actor-aware A*), so following it all the
                    // way walked minions behind their target. Halting at the boundary avoids that. The
                    // allied-spacing spread needs front-of-wave targeting + near-side-only paths, not
                    // an in-range chase — see the gap-analysis memory.
                    StopMovement();
                }
                else
                {
                    // Out of range → chase toward the distinct recommended cell.
                    // F2 Phase 2: full client `Actor_Common::GetClosestAttackPoint` chain — the
                    // C++ backend of the Lua API `SetStateAndCloseToTarget` that every AI script
                    // (Minion.lua / Hero.lua / BaronMinionAI.lua / Aggro.lua / Pet AIs) uses to
                    // approach a target. Path-based: predicts the target's position along its
                    // path, paths to it, and recommends the waypoint ~2 past the point where the
                    // path enters attack range (so approaching units commit slightly into range
                    // instead of stopping on the boundary). Falls back to the Phase-1 geometric
                    // stand position when no path exists (target unreachable / degenerate).
                    //
                    // Re-path throttle: keep following the existing waypoints unless the path ran out,
                    // the target changed, or the target drifted more than REPATH_TARGET_DRIFT since the
                    // last recompute. Without this the full A* below ran every tick per chasing unit
                    // (its result usually discarded by IsPathTheSame) — Riot only recomputes the
                    // approach on the 0.25s brain sweep / on target drift, so this is both cheaper and
                    // closer to client behavior.
                    bool needRepath = IsPathEnded()
                        || _repathTargetNetId != chaseUnit.NetId
                        || float.IsNaN(_repathTargetPos.X)
                        || Vector2.DistanceSquared(_repathTargetPos, targetPos) > REPATH_TARGET_DRIFT * REPATH_TARGET_DRIFT;
                    if (!needRepath)
                    {
                        return;
                    }

                    // Anti-wobble commit window (lane minions): if this recompute is for the SAME
                    // target at roughly the same spot (i.e. it fired because our actor-aware path just
                    // ran out / hit a contested cell, NOT because the target moved), pace it to
                    // MIN_CHASE_REPATH_MS so we commit to the routed path instead of re-routing every
                    // tick around moving allies (the client-side wobble). A target switch or >75u drift
                    // skips this and recomputes now.
                    bool sameTargetSameSpot = _repathTargetNetId == chaseUnit.NetId
                        && !float.IsNaN(_repathTargetPos.X)
                        && Vector2.DistanceSquared(_repathTargetPos, targetPos) <= REPATH_TARGET_DRIFT * REPATH_TARGET_DRIFT;
                    if (this is LaneMinion && sameTargetSameSpot
                        && _game.GameTime - _lastChaseRepathMs < MIN_CHASE_REPATH_MS)
                    {
                        return;
                    }
                    _lastChaseRepathMs = _game.GameTime;
                    _repathTargetNetId = chaseUnit.NetId;
                    _repathTargetPos = targetPos;

                    Vector2 pathDestination = targetPos;
                    bool gotAttackPoint = false;
                    Vector2 recommended = targetPos;
                    if (chaseUnit != null)
                    {
                        gotAttackPoint = _game.Map.PathingHandler.GetClosestAttackPoint(
                            this, chaseUnit, idealRange, out recommended, out _, out _);
                        pathDestination = gotAttackPoint
                            ? recommended
                            : _game.Map.PathingHandler.GetAttackStandPosition(this, chaseUnit, idealRange);
                    }

                    if (!_game.Map.PathingHandler.IsWalkable(pathDestination, PathfindingRadius))
                    {
                        pathDestination = _game.Map.NavigationGrid.GetClosestTerrainExit(pathDestination, PathfindingRadius);
                    }

                    // Unit-aware overload threads the actor-blocked predicate (A1) so the search
                    // routes around blocking units instead of producing a path that would
                    // immediately need post-process collision correction.
                    //
                    // STAGE B (allied-spacing fan-out, 2026-06-21): for LANE MINIONS, skip the
                    // straight-line LOS fast-path so the actor-aware A* actually runs on the approach
                    // — a follower whose straight line to its stand cell is blocked by a LEADING ALLY
                    // routes AROUND it and arrives from a distinct angle, so the wave fans onto
                    // distinct boundary points instead of piling on one line (the user's "ally in the
                    // way → I go around" model). Scoped to lane minions ONLY: they target the FRONT of
                    // the wave (stage A — CallForHelp no longer redirects them to backline minions),
                    // so "route around" only ever means around ALLIES between them and a front target,
                    // never around the enemy front to reach a backline target (which is what produced
                    // behind-wave for the unscoped chase, clash22). Champions/pets keep the LOS-first
                    // path, where skip-LOS WOULD route behind a backline target. The actor-aware
                    // smoothing (B1) keeps the bend from being flattened back to a straight line.
                    bool actorAwareChase = this is LaneMinion;
                    var newWaypoints = _game.Map.PathingHandler.GetPath(this, pathDestination,
                        skipLineOfSight: actorAwareChase);
                    if (newWaypoints != null && newWaypoints.Count > 1)
                    {
                        SetWaypoints(newWaypoints, pathReason: "attack");
                    }

                    // Path to the attack target is blocked: the goal is unreachable, so the
                    // pathfinder returned no path or only a partial one (lands at the closest
                    // reachable cell). Flag it; the engine fires OnPathToTargetBlocked at the top
                    // of the next Update so the AI re-acquires (Aggro.lua: AddToIgnore + re-find).
                    if (newWaypoints == null || newWaypoints.IsPartial)
                    {
                        _pathToTargetBlocked = true;
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
            // S4-faithful pick (BipedHelpers::GetAnimationFromProbability,
            // CharacterData.cpp:1890): ONE [0,1) roll, cumulative threshold walk in
            // slot order — the first slot whose cumulative probability exceeds the
            // roll wins. NO normalization, NO anti-repeat rerolls (every attack rolls
            // independently). Slots behind a cumulative sum >= 1.0 are unreachable by
            // design: that is what keeps conditional attacks (form/passive/fossil
            // entries, loaded with the 2.0 catch-all default) out of the rotation,
            // while a reachable catch-all (Lulu: base=0.5 + valueless Extra1) takes
            // the whole remainder.
            short firstSlot = IsNextAutoCrit
                ? (short)BasicAttackTypes.BASICATTACK_CRITICAL_SLOT1
                : (short)BasicAttackTypes.BASIC_ATTACK_TYPES_FIRST_SLOT;
            short lastSlot = IsNextAutoCrit
                ? (short)BasicAttackTypes.BASICATTACK_CRITICAL_LAST_SLOT
                : (short)BasicAttackTypes.BASICATTACK_NORMAL_LAST_SLOT;

            float roll = (float)_random.NextDouble();
            float cumulative = 0.0f;

            for (short slot = firstSlot; slot <= lastSlot; slot++)
            {
                var idx = slot - 64;
                if (idx < 0 || idx >= CharData.BasicAttacks.Count)
                {
                    continue;
                }

                cumulative += CharData.BasicAttacks[idx].Probability;
                if (cumulative > roll)
                {
                    // Riot returns the slot unconditionally (their spell registry has
                    // an entry for every declared slot); we need a resolvable spell —
                    // a dead slot falls through to the first-slot fallback, mirroring
                    // the walk's own tail behavior.
                    if (Spells.TryGetValue(slot, out var spell) && spell != null)
                    {
                        _lastAutoAttack = spell;
                        return spell;
                    }
                    break;
                }
            }

            _lastAutoAttack = Spells[firstSlot];
            return _lastAutoAttack;
        }

        // Per-unit karma streak state (only champions consult it, mirroring AIHero's
        // mKarma member).
        private readonly KarmaRandom _critKarma = new KarmaRandom();
        private readonly KarmaRandom _dodgeKarma = new KarmaRandom();

        /// <summary>
        /// Rolls whether the upcoming auto attack crits — S4-faithful split
        /// (AIHero::ComputeCriticalSuccess, AIHero.cpp:838): CHAMPIONS roll through the
        /// karma PRD with independent per-target-class streams (0 = vs enemy heroes,
        /// 2 = vs minions/monsters incl. pets/wards); anything else (structures, ally
        /// heroes) returns false WITHOUT touching the counters. Non-hero units use the
        /// plain uniform float roll (obj_AI_Base::ComputeCriticalSuccess base behavior;
        /// moot in practice — their crit chance is 0), structures excluded for safety.
        /// </summary>
        private bool RollAutoAttackCrit(AttackableUnit target)
        {
            if (this is Champion)
            {
                if (target is Champion && target.Team != Team)
                {
                    return _critKarma.Roll(Stats.CriticalChance.Total, 0);
                }
                if (target is Minion)
                {
                    return _critKarma.Roll(Stats.CriticalChance.Total, 2);
                }
                return false;
            }

            if (target is ObjBuilding || target is BaseTurret)
            {
                return false;
            }

            return _random.NextDouble() < Stats.CriticalChance.Total;
        }

        /// <summary>
        /// Rolls whether the next auto attack misses, against this unit's <see cref="StatsNS.Stats.MissChance"/>
        /// [0..1]. Blind raises MissChance to 1.0 → guaranteed miss. A flat roll (no Karma smoothing): unlike
        /// crit, Riot does not pseudo-randomize miss. MissChance defaults to 0, so this is false for everyone
        /// not blinded/affected by a miss-chance debuff.
        /// </summary>
        private bool RollAutoAttackMiss(AttackableUnit target)
        {
            float missChance = Stats.MissChance.Total;
            if (missChance <= 0f)
            {
                return false;
            }
            return _random.NextDouble() < missChance;
        }

        /// <summary>
        /// Rolls whether THIS unit (the target) dodges an incoming auto attack from <paramref name="attacker"/>,
        /// against this unit's <see cref="StatsNS.Stats.Dodge"/> [0..1]. Target-side mirror of Riot's
        /// obj_AI_Base::ComputeDodgeSuccess: champions use the pseudo-random Karma roll (like crit, bucketed by
        /// attacker type), other units a flat roll. An attacker with <see cref="DodgePiercing"/> can't be dodged.
        /// Dodge defaults to 0, so this is false for everyone not under a dodge effect (e.g. Jax E / JaxEvasion = 1.0).
        /// </summary>
        private bool RollDodge(AttackableUnit attacker)
        {
            if (attacker is ObjAIBase ai && ai.DodgePiercing)
            {
                return false;
            }

            float dodge = Stats.Dodge.Total;
            if (dodge <= 0f)
            {
                return false;
            }

            if (this is Champion)
            {
                // Bucket selection mirrors AIHero::ComputeDodgeSuccess (different RollKarma seed vs minions).
                int bucket = attacker is Minion ? 2 : 0;
                return _dodgeKarma.Roll(dodge, bucket);
            }

            return _random.NextDouble() < dodge;
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
            // DesignerTotalTime is already attack-speed-scaled at cast time (Spell.Cast AA
            // block divides by AttackSpeedModifier, replay-verified on
            // TalonNoxianDiplomacyAttack) — don't divide a second time here.
            if (spell.CastInfo.SpellSlot < (byte)SpellSlotType.BasicAttackNormalSlots)
            {
                float overrideCycleTime = spell.CastInfo.DesignerTotalTime;
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
        /// Switches this AI's attack target to <paramref name="target"/> AND fully resets the
        /// auto-attack pipeline so the next Update tick starts a fresh AA on the new target with
        /// proper damage application. Sends the wire-side target-switch pair Riot uses
        /// (<c>Basic_Attack_Pos</c> + <c>NPC_InstantStop_Attack</c>, replay-verified KatarinaE
        /// id=18097 + id=18102, t=303818).
        ///
        /// <para><b>Why the pipeline reset matters</b>: a stale <c>IsAttacking=true</c> from a
        /// pre-blink AA windup makes <c>UpdateTarget</c> skip the new-target acquisition path
        /// (line 1963 — the <c>else if (IsAttacking)</c> branch continues the OLD windup), so the
        /// post-blink AA fires through a degenerate code path and applies no damage. Calling
        /// <see cref="CancelAutoAttack"/> with <c>reset+fullCancel</c> clears
        /// <c>AutoAttackSpell.State</c>, <c>_autoAttackCurrentCooldown</c>, <c>IsAttacking</c>, and
        /// <c>HasMadeInitialAttack</c> so the unit is treated as fresh.</para>
        ///
        /// <para><b>Wire pattern</b>: Riot does NOT send <c>AI_TargetS2C</c> for these switches
        /// (0 occurrences in the reference Katarina replay). The target-switch on the wire IS the
        /// BAP+ISA pair. We pass <c>networked: false</c> to <see cref="SetTargetUnit"/> to skip the
        /// AI_TargetS2C broadcast and emit BAP/ISA manually.</para>
        /// </summary>
        /// <param name="target">Unit to retarget to. Must be non-null and a valid AA target.</param>
        /// <param name="emitInstantStop">Whether to broadcast the <c>NPC_InstantStop_Attack</c> packet.
        /// Replay-empirical (Kat-perspective, 79 E casts): Riot only fires this packet on ~27% of E
        /// casts — apparently only when there's an active AA-windup to cancel. Pass
        /// <c>this.IsAttacking</c> at the call site to match Riot's wire pattern. The server-side
        /// pipeline reset still happens regardless — only the wire packet is gated.</param>
        public void RetargetAttackToWithHandoff(AttackableUnit target, bool emitInstantStop = true)
        {
            if (target == null) return;

            // Pipeline reset (always): mirrors a full AA-reset (e.g. Yi Q reset, Riven Q3 reset).
            // `silent: !emitInstantStop` suppresses the ISA broadcast embedded in CancelAutoAttack
            // when the caller has decided no in-flight AA exists to cancel client-side.
            CancelAutoAttack(reset: true, fullCancel: true, silent: !emitInstantStop);

            // Server-state retarget (no AI_TargetS2C broadcast — that's empirically not used for
            // blink-target-switches; the BAP packet is the wire signal).
            SetTargetUnit(target, networked: false);

            var futureProjNetId = _game.NetworkIdManager.GetNewNetId();
            _game.PacketNotifier.NotifyBasic_Attack_Pos(this, target, futureProjNetId, IsNextAutoCrit);
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
                // Clearing the postponed cast (Riot ClearPostponedSpells → savedOrderCmd=NONE +
                // ORDER_STATUS_CLEAR + savedOrderObj=0). Drop the separate cast target here too — this is
                // the single point where a move-to-cast ends (cast finished via Spell.FinishCasting, or
                // cancelled via a new order), so the targeted/positional retry can't re-fire afterwards.
                if (OrderStatus == OrderState.Postponed)
                {
                    OrderStatus = OrderState.Clear;
                }
                PostponedCastTarget = null;
                return;
            }

            // Positional move-to-cast: walk toward the cursor point. (location and unit are mutually
            // exclusive across the callers — targeted passes location=Zero+unit, positional passes
            // location+no unit.)
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
            }

            // Move-to-cast keeps the cast target SEPARATE from the attack target (faithful Riot: PostponeSpell
            // stores mPostponedSpell.mTargetID and never SetEnemyID). So clear any prior ATTACK target — the
            // caster must not auto-attack while running to cast range — and store the cast target (targeted;
            // null for positional) in the dedicated postpone field. Set BEFORE the order so the ChaseIntent
            // derivation in UpdateMoveOrder (which keys a TempCastSpell chase on PostponedCastTarget != null)
            // sees it: targeted → ChaseIntent true (chase to cast range); positional → false (walks waypoints).
            SetTargetUnit(null, true);
            PostponedCastTarget = unit;

            // IssueOrders S2 Phase 3 (P3a): a postponed cast is an AI_TEMP_CASTSPELL order (Riot
            // PostponeSpell: orders.setOrder(AI_TEMP_CASTSPELL) + PostponeOrder). This replaces the former
            // AttackTo (targeted) / MoveTo (positional) order reuse, so those values no longer leak into the
            // move-to-cast path. The UpdateTarget retry now gates on MoveOrder == TempCastSpell.
            UpdateMoveOrder(OrderType.TempCastSpell, true);

            // A targeted move-to-cast must re-path toward the cast target NOW, not finish a previously
            // ordered MoveTo path first. Unlike positional (which sets fresh waypoints above), targeted sets
            // none and relies on the RefreshWaypoints chase — whose re-path throttle would otherwise keep
            // following the stale path until it ends (no path-end / same target NetID), so the champion only
            // turned toward the target after reaching the old click destination. Invalidate the throttle to
            // force the chase recompute, exactly as HandleMove does for a fresh attack order.
            if (unit != null)
            {
                ForceChaseRepath();
            }

            // Preserve the positional side-effect exactly: the former MoveTo ran through UpdateMoveOrder's
            // ClearQueuedSpell branch; AttackTo (targeted) did NOT. TempCastSpell is in neither list, so
            // replicate the queue-clear for the positional case to keep behaviour identical. (This
            // positional-only queue clear is a latent asymmetry inherited from the MoveTo reuse — preserved
            // deliberately for 0 behaviour change; revisit as its own decision.)
            if (location != Vector2.Zero)
            {
                ClearQueuedSpell();
            }

            // PostponeOrder: an out-of-range cast → POSTPONED (overrides the PENDING the UpdateMoveOrder
            // above set). Phase 3 keeps the retry in UpdateTarget (driven off MoveOrder == TempCastSpell);
            // routing it through RouteOrder (P3b) is deferred — unverifiable from decomp/lua/replay.
            OrderStatus = OrderState.Postponed;
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

        // Speed-state RUN animation watcher (Riot server engine system, replay-verified
        // 630b7ceb): the RUN slot is swapped via S2C_SetAnimStates depending on movement
        // state — run_haste while a HASTE-type buff is active (Ghost etc.), run_fast while
        // above base move speed (boots alone qualify — enter values in the replay are
        // exactly base+25 tiers), run_slow while a slow effect is registered (presence,
        // not net speed: a slowed-but-booted Jinx kept RUN_FAST because she has no
        // run_slow variant — the fall-through below reproduces that). Spell-bound anim
        // sets (Nautilus W, Jinx Q stance) stay script territory and win via the
        // source-layered SetAnimStates stack. run_slow_back (backpedal) not implemented.
        private string _runVariantApplied;
        private readonly object _runAnimSource = new object();

        private void UpdateRunAnimationVariant()
        {
            var variants = Content.RunAnimationVariants.Get(Model);
            if (variants == null)
            {
                return;
            }

            string desired = null;
            if (!IsDead)
            {
                if (HasBuffType(BuffType.HASTE))
                {
                    desired = variants.Haste ?? variants.Fast;
                }
                else if (variants.Brush != null
                    && _game.Map.NavigationGrid.GetNearestGrassGroup(Position, 0f) != 0)
                {
                    // Brush outranks Fast: the replay shows run_in_brush at above-base MS
                    // (base+boots values), so being in a brush wins over the speed tier.
                    desired = variants.Brush;
                }
                else if (Stats.HasActiveSlows && variants.Slow != null)
                {
                    desired = variants.Slow;
                }
                else if (Stats.GetTrueMoveSpeed() > Stats.MoveSpeed.BaseValue + 5.0f)
                {
                    desired = variants.Fast;
                }
            }

            if (desired != _runVariantApplied)
            {
                // asBaseLayer: script/buff RUN overrides (Aatrox R RUN_ULT, form swaps)
                // always outrank the speed variant, regardless of registration order.
                SetAnimStates(new Dictionary<string, string> { { "RUN", desired ?? "" } }, _runAnimSource, asBaseLayer: true);
                _runVariantApplied = desired;
            }
        }

        protected override void OnEnterVision(int userId, TeamId team)
        {
            base.OnEnterVision(userId, team);

            // CastSpellAns is vision-scoped, so a client acquiring vision MID-WINDUP never
            // saw the cast — without a catch-up the windup animation is simply missing and
            // the missile pops out of nowhere (fog-edge Blitz Q). Riot re-announces the
            // running cast to exactly this recipient with StartCastTime = elapsed /
            // ExtraCastTime = −elapsed (replay 630b7ceb: 3/3 catch-up ANS in the same tick
            // as the enter-visibility bundle). Base-slot basic attacks are excluded — those
            // are announced via Basic_Attack packets, never ANS.
            var inFlight = _castingSpell;
            if ((inFlight == null || inFlight.State != SpellState.STATE_CASTING)
                && AutoAttackSpell != null && AutoAttackSpell.State == SpellState.STATE_CASTING
                && AutoAttackSpell.CastInfo.SpellSlot < 64)
            {
                inFlight = AutoAttackSpell;
            }

            if (inFlight != null && inFlight.State == SpellState.STATE_CASTING
                && inFlight.CastInfo.SpellSlot < 64)
            {
                // Attack-timed casts count UP (CurrentDelayTime), normal casts count DOWN
                // from DesignerCastTime (CurrentCastTime).
                float elapsed = inFlight.CastInfo.IsAutoAttack || inFlight.CastInfo.UseAttackCastTime
                    ? inFlight.CurrentDelayTime
                    : inFlight.CastInfo.DesignerCastTime - inFlight.CurrentCastTime;

                if (elapsed > 0.001f)
                {
                    _game.PacketNotifier.NotifyNPC_CastSpellAnsCatchUp(inFlight, elapsed, userId);
                }
            }
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

            // M2 Phase 3: cast-disabling CC clears the CanCast capability (BuffType.ToCapabilityDisable).
            if (!Status.HasFlag(StatusFlags.CanCast))
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
        ///
        /// <para>When switching between two non-null targets, the AA pipeline state
        /// (<c>IsAttacking</c>, <c>HasMadeInitialAttack</c>, <c>AutoAttackSpell.State</c>,
        /// <c>_autoAttackCurrentCooldown</c>) is silently reset because windup state from the old
        /// target is stale relative to the new one. Without this, <c>UpdateTarget</c>'s
        /// <c>else if (IsAttacking)</c> branch (line 2005) keeps continuing the OLD windup against
        /// the new target — observable as Trundle/Jax/KatarinaE getting "stuck" not damaging after
        /// a target switch. The reset is silent (no <c>NPC_InstantStop_Attack</c>) so existing wire
        /// patterns are preserved; callers needing a wire-side handoff use
        /// <see cref="RetargetAttackToWithHandoff"/>.</para>
        /// </summary>
        /// <param name="target">Unit to target.</param>
        /// <param name="networked">Whether to broadcast the target change.</param>
        /// <param name="lostReason">When clearing the target (target==null), why it was lost — carried with OnTargetLost.</param>
        public void SetTargetUnit(AttackableUnit target, bool networked = false,
            TargetLostReason lostReason = TargetLostReason.Cleared)
        {
            if (TargetUnit == target)
            {
                return;
            }
            bool wasTargetingChampion = TargetUnit is Champion;
            bool isSwitchBetweenTargets = target != null && TargetUnit != null;
            if (target == null && TargetUnit != null)
            {
                ApiEventManager.OnTargetLost.Publish(this, (TargetUnit, lostReason));
            }

            TargetUnit = target;
            // Acquiring a fresh target abandons any remembered "lost" target (go-to-last-known).
            if (target != null)
            {
                _lostTargetUnit = null;
            }

            if (isSwitchBetweenTargets)
            {
                CancelAutoAttack(reset: true, fullCancel: true, silent: true);
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
            base.Update(diff);
            UpdateRunAnimationVariant();
            try
            {
                using var _scope = Profiler.Scope($"script:{CharScript.GetType().Name}.OnUpdate", "scripts");
                CharScript.OnUpdate(diff);
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }

            if (!_aiPaused)
            {
                // Deferred from last tick's UpdateTarget (RefreshWaypoints) to avoid re-entrant
                // target/waypoint mutation inside the pathing pass — fire before the AI brain so
                // its re-acquisition is seen by this tick's timers.
                if (_pathToTargetBlocked)
                {
                    _pathToTargetBlocked = false;
                    ApiEventManager.OnPathToTargetBlocked.Publish(this, TargetUnit);
                }

                try
                {
                    using var _scope = Profiler.Scope($"script:{AIScript.GetType().Name}.OnUpdate", "scripts");
                    AIScript.OnUpdate(diff);
                }
                catch (Exception e)
                {
                    _logger.Error(null, e);
                }
            }

            using (Profiler.Scope("ObjAI.SpellsUpdate"))
            {
                // Snapshot into a reused buffer (allocation-free) instead of `new List<Spell>` per
                // tick — same defensive copy (a spell's Update can mutate Spells), no GC churn.
                _spellUpdateBuffer.Clear();
                _spellUpdateBuffer.AddRange(Spells.Values);
                foreach (var s in _spellUpdateBuffer)
                {
                    s.Update(diff);
                }
            }

            if (Inventory != null)
            {
                using var _invScope = Profiler.Scope("ObjAI.InventoryUpdate");
                Inventory.OnUpdate(diff);
            }

            using (Profiler.Scope("ObjAI.AssistMarkers"))
            {
                UpdateAssistMarkers();
            }
            using (Profiler.Scope("ObjAI.UpdateTarget"))
            {
                UpdateTarget();
            }

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
                // LookAtType != AttackType (see PacketNotifier.NotifyS2C_UnitSetLookAt): attacks on a target
                // unit orient toward the Unit, only radial attacks toward a Direction. (Pet double-hit-FX fix.)
                var lookAtPacket = new S2C_UnitSetLookAt
                {
                    SenderNetID = this.NetId,
                    LookAtType = (byte)(attackType == AttackType.ATTACK_TYPE_RADIAL ? LookAtType.Direction : LookAtType.Unit),
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
            // Drop a target that is no longer targetable by this unit (Riot IsTargetableByUnit:
            // global + per-team, plus minion ward/acquirable gates), keeping the useable exception
            // (wards/plants have their own rules). The prior condition (`!global && perTeam`) could
            // never be true — GetIsTargetableToTeam already returns false when the global flag is
            // clear — so it never dropped anything.
            if (TargetUnit != null && !TargetUnit.IsTargetableByUnit(this))
            {
                if (TargetUnit.CharData.IsUseable)
                {
                    return;
                }
                Untarget(TargetUnit);
            }
        }

        // Game time (ms) this unit last issued a Call For Help (cfh_Delay throttle) / last responded
        // to one (cfh_Duration dedup + cfh_Stick anti-distraction window).
        private float _lastCallForHelpIssuedMs = float.NegativeInfinity;
        private float _lastCallForHelpRespondedMs = float.NegativeInfinity;

        public override void TakeDamage(DamageData damageData, DamageResultType damageText, IEventSource sourceScript = null)
        {
            base.TakeDamage(damageData, damageText, sourceScript);

            var attacker = damageData.Attacker;
            if (attacker == null)
            {
                return;
            }

            // Call For Help broadcast, driven by the real 4.20 cfh_* constants (Map Constants.var).
            var cfh = LeagueSandbox.GameServer.Content.GlobalData.CallForHelpVariables;

            // Important / forced Call For Help — the turret focus-lock (tower-dive aggro). Decomp:
            // DamageEffect::ForceCallForHelp (DamageCallback.h:0x56) routed to a turret-only handler
            // (Turret.lua OnReceiveImportantCallForHelp). Triggered explicitly by a script-set flag, or
            // implicitly when an enemy champion damages an allied champion (the established tower-dive
            // rule; user-chosen predicate, see docs/TURRET_AI_PORT_PLAN.md §3/§6). Idempotent on the
            // turret side (PutTargetInTargetList dedupes), so this is NOT throttled by cfh_* — it runs
            // BEFORE the regular cfh_Delay gate below. Only allied turrets in range react; every other
            // archetype's handler is a no-op.
            if (damageData.ForceCallForHelp || (attacker is Champion && this is Champion))
            {
                float importantHearSq = cfh.Radius * cfh.Radius;
                foreach (var it in _game.ObjectManager.GetObjects())
                {
                    if (it.Value is not BaseTurret turret
                        || turret.IsDead
                        || turret.Team != Team
                        || !turret.AIScript.AIScriptMetaData.HandlesCallsForHelp)
                    {
                        continue;
                    }

                    // Pre-gate by the turret's own attack range + cfh_TurretRadius so a turret across
                    // the map doesn't lock; the script re-checks ObjectInAttackRange before adding.
                    float engageRange = turret.Stats.Range.Total + cfh.TurretRadius;
                    if (Vector2.DistanceSquared(turret.Position, attacker.Position) > engageRange * engageRange)
                    {
                        continue;
                    }

                    try
                    {
                        using var _scope = Profiler.Scope($"script:{turret.AIScript.GetType().Name}.OnReceiveImportantCallForHelp", "scripts");
                        turret.AIScript.OnReceiveImportantCallForHelp(attacker, this);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(null, e);
                    }
                }
            }

            // cfh_Delay: a unit issues a Call For Help at most once per Delay seconds (not every
            // damage tick).
            if (_game.GameTime - _lastCallForHelpIssuedMs < cfh.Delay * 1000f)
            {
                return;
            }
            _lastCallForHelpIssuedMs = _game.GameTime;

            // cfh_Radius: only allies within Radius of the victim hear the call.
            float hearRadiusSquared = cfh.Radius * cfh.Radius;

            var objects = _game.ObjectManager.GetObjects();
            foreach (var it in objects)
            {
                if (it.Value is not ObjAIBase u
                    || u == this
                    || u.IsDead
                    || u.Team != Team
                    || !u.AIScript.AIScriptMetaData.HandlesCallsForHelp)
                {
                    continue;
                }

                // Turret AND lane-minion aggro rule: they only answer a regular Call For Help when the
                // VICTIM is an allied CHAMPION (defend the champion). Attacking an allied LANE MINION must
                // NOT pull the wave / the tower onto the attacker — replay-verified (28 4.20 replays): of
                // 325 minion→champion attacks, the vast majority were already on the champion or spontaneous
                // (proximity); only ~6% even coincided with the champion having just hit an allied minion,
                // and those are ambiguous re-targets — so champ-attacks-lane-minion is NOT a meaningful
                // wave-aggro trigger. Lane minions keep their normal nearest-front targeting; the
                // champion-on-champion dive routes through the important CFH (force-flagged →
                // OnReceiveImportantCallForHelp / turretTargetList sticky-lock). NOTE the type test is
                // LaneMinion-specific: jungle monsters (Monster : Minion, NOT LaneMinion) must still answer
                // a camp-mate's CFH (camp-assist, victim = monster), and pets (Pet : Minion) are unaffected.
                if ((u is BaseTurret || u is LaneMinion) && this is not Champion)
                {
                    continue;
                }

                // cfh_Radius: the responder must be within Radius of the victim to hear the call.
                if (Vector2.DistanceSquared(u.Position, Position) > hearRadiusSquared)
                {
                    continue;
                }

                // cfh_MeleeRadius / cfh_RangedRadius / cfh_TurretRadius: a responder only answers if
                // the attacker is within its own attack range plus the buffer for its responder type.
                float buffer = u is BaseTurret ? cfh.TurretRadius : (u.IsMelee ? cfh.MeleeRadius : cfh.RangedRadius);
                float engageRange = u.Stats.Range.Total + buffer;
                if (Vector2.DistanceSquared(u.Position, attacker.Position) > engageRange * engageRange)
                {
                    continue;
                }

                float sinceResponded = _game.GameTime - u._lastCallForHelpRespondedMs;
                // cfh_Duration: the responder already answered an ongoing call this recently — skip.
                if (sinceResponded < cfh.Duration * 1000f)
                {
                    continue;
                }
                // cfh_Stick: for the (longer) Stick window, ignore calls that are not strictly higher
                // priority than the current target, so the responder is not yanked between equal/lower
                // priority threats. Contextual ClassifyTarget(attacker, victim) gives the attacker its
                // call-for-help priority (lower value = higher priority); the current target is graded
                // without context, which can only under-stick (err toward responding), never strand the
                // responder on a stale target.
                if (sinceResponded < cfh.Stick * 1000f
                    && u.TargetUnit != null
                    && u.ClassifyTarget(attacker, this) >= u.ClassifyTarget(u.TargetUnit))
                {
                    continue;
                }
                u._lastCallForHelpRespondedMs = _game.GameTime;

                try
                {
                    using var _scope = Profiler.Scope($"script:{u.AIScript.GetType().Name}.OnCallForHelp", "scripts");
                    u.AIScript.OnCallForHelp(attacker, this);
                }
                catch (Exception e)
                {
                    _logger.Error(null, e);
                }
            }
        }

        /// <summary>
        /// Resumes an attack-move toward <see cref="AttackMoveDestination"/> after the target that was
        /// acquired along the way is gone. Paths to the stored point and re-issues the AttackMove order;
        /// if the point is unreachable (or already reached), stops and clears the destination.
        /// </summary>
        /// <summary>
        /// Nearest enemy in AcquisitionRange (distance only — player attack-move acquisition, see the
        /// a-move scan in UpdateTarget). Null if none. Used both at click time (HandleMove, so an
        /// in-range target is attacked immediately without first walking toward the click) and during
        /// the outbound walk.
        /// </summary>
        /// <summary>
        /// Riot <c>obj_AI_Base::GetAcquisitionRange</c> (mac-decomp AIBase.cpp:2618): the radius within
        /// which this unit auto-acquires targets. When charmed, Riot expands it to the global
        /// <c>ar_AICharmedAcquisitionRange</c> (1000) — a charmed unit keeps engaging things along its
        /// forced-walk path; otherwise it is the unit's acquisitionRange data field + modifiers
        /// (= <see cref="Stats.AcquisitionRange"/>.Total). Riot's third branch (IsHoldingPosition →
        /// attackRange) is handled script-side in HeroAI (a held champion re-gates acquisition to attack
        /// range), so it is intentionally not folded in here.
        /// </summary>
        public float GetAcquisitionRange()
        {
            if (Status.HasFlag(StatusFlags.Charmed))
            {
                return LeagueSandbox.GameServer.Content.GlobalData.AttackRangeVariables.AICharmedAcquisitionRange;
            }
            return Stats.AcquisitionRange.Total;
        }

        public AttackableUnit AcquireAttackMoveTarget()
        {
            float range = GetAcquisitionRange();
            AttackableUnit best = null;
            float bestDistSq = range * range;
            foreach (var it in _game.ObjectManager.GetObjects())
            {
                if (!(it.Value is AttackableUnit u) || u.IsDead || u.Team == Team
                    || !u.IsTargetableByUnit(this))
                {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(Position, u.Position);
                if (distSq < bestDistSq)
                {
                    best = u;
                    bestDistSq = distSq;
                }
            }
            return best;
        }

        public void ResumeAttackMove()
        {
            var path = _game.Map.PathingHandler.GetPath(this, AttackMoveDestination);
            if (path != null && path.Count > 1)
            {
                SetWaypoints(path);
                UpdateMoveOrder(OrderType.AttackMove, true);
            }
            else
            {
                UpdateMoveOrder(OrderType.Stop, true);
                AttackMoveDestination = Vector2.Zero;
            }
        }

        /// <summary>
        /// Riot TurnOnAutoAttack(target): the script enables auto-attacking the given unit. This only
        /// flips the toggle — the engine still owns per-swing timing (windup → fire → cooldown) and only
        /// swings when the target is also in range with the cooldown ready (see UpdateTarget). Idempotent:
        /// safe to call every tick while in range; it never resets a committed windup.
        /// </summary>
        public void TurnOnAutoAttack(AttackableUnit target)
        {
            _autoAttackEnabled = true;
            _autoAttackEnabledTarget = target;
        }

        /// <summary>
        /// Riot TurnOffAutoAttack(stopReason): the script disables auto-attacking. Moving / TargetLost
        /// cancel an in-progress windup (you left range / the target is gone) but let a connected swing
        /// finish; OtherImmediately is a hard cancel (CC / halt).
        /// </summary>
        public void TurnOffAutoAttack(AutoAttackStopReason reason)
        {
            _autoAttackEnabled = false;
            _autoAttackEnabledTarget = null;

            if (reason == AutoAttackStopReason.OtherImmediately)
            {
                CancelAutoAttack(reset: true, fullCancel: true, reason: reason);
            }
            else if ((reason == AutoAttackStopReason.Moving || reason == AutoAttackStopReason.TargetLost)
                     && IsAttacking && !HasAutoAttacked)
            {
                // Cancel only an un-connected windup; a swing that already landed finishes its animation.
                CancelAutoAttack(reset: true, reason: reason);
            }
        }

        /// <summary>
        /// Whether the engine may START a new auto-attack swing this tick. The engine only swings while the
        /// script (via the shared AutoAttackComponent) has TurnOnAutoAttack'd the CURRENT target — Riot's
        /// "target set != auto-fire". P5.6: this is now the universal gate (the legacy "auto-fire on
        /// target+range" path and the ScriptOwnsAutoAttack compat flag were removed once every archetype
        /// was migrated). A unit with no AutoAttackComponent (e.g. EmptyAIScript summons = Riot idle.lua)
        /// never gets the toggle turned on, so it never auto-attacks — the faithful passive-summon default.
        /// </summary>
        private bool AutoAttackTogglePermits()
        {
            return _autoAttackEnabled && _autoAttackEnabledTarget == TargetUnit;
        }

        /// <summary>
        /// Riot IsTargetLost: this unit has a remembered "lost" target — a unit that left vision while the
        /// unit was hard-engaged (TargetLostReason.LostVisibility). It walks to that target's last-known
        /// position (<see cref="LostTargetLastKnownPosition"/>) and re-acquires on sight
        /// (<see cref="GetLostTargetIfVisible"/>). Cleared when a fresh target is acquired (SetTargetUnit).
        /// Champion-only in practice. See docs/LOST_TARGET_REACQUISITION_PLAN.md.
        /// </summary>
        public bool IsTargetLost()
        {
            return _lostTargetUnit != null;
        }

        /// <summary>
        /// Riot GetLostTargetIfVisible: returns the remembered lost target IFF it is alive and visible to
        /// this unit's team again (re-acquire on sight), else null. Pure query — re-targeting the returned
        /// unit via SetTargetUnit is what clears the lost state.
        /// </summary>
        public AttackableUnit GetLostTargetIfVisible()
        {
            if (_lostTargetUnit != null && !_lostTargetUnit.IsDead && _lostTargetUnit.IsVisibleByTeam(Team))
            {
                return _lostTargetUnit;
            }
            return null;
        }

        /// <summary>
        /// The frozen last-seen position of the lost target — the go-to-last-known destination. Valid while
        /// <see cref="IsTargetLost"/> is true.
        /// </summary>
        public Vector2 LostTargetLastKnownPosition => _lostTargetLastKnownPosition;

        /// <summary>
        /// Abandon the remembered lost target — called when the unit arrives at the last-known position
        /// without re-sighting it, or when a new player order takes over the pursuit.
        /// </summary>
        public void ClearLostTarget()
        {
            _lostTargetUnit = null;
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
                if ((IsAttacking && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp) || HasMadeInitialAttack)
                {
                    CancelAutoAttack(!HasAutoAttacked, true, reason: AutoAttackStopReason.TargetLost);
                }
                // Attack-move resume on target loss is now the HeroAI script's job (it owns the a-move
                // state machine via _aiState == AI_SOFTATTACK; see HeroAI.OnTick). Non-HeroAI units
                // never set AttackMoveDestination, so there is nothing to resume here.
            }
            // Drop the target when it goes untargetable, EXCEPT for useable units (wards/plants/pets) —
            // those have their own targetability rules and may still be valid targets despite a transient
            // StatusFlags.Targetable=false. Inverted from the prior condition (`Targetable=false &&
            // IsUseable=true` would never trigger for normal champions/minions, so a turret kept
            // attacking a champion in an untargetable revive state, e.g. Aatrox passive).
            else if (TargetUnit.IsDead || (!TargetUnit.IsTargetableByUnit(this) && !TargetUnit.CharData.IsUseable) || !TargetUnit.IsVisibleByTeam(Team))
            {
                // If the attack already connected (e.g. HasAutoAttacked), let the animation
                // finish instead of cancelling it mid-animation.
                if (IsAttacking && !HasAutoAttacked)
                {
                    CancelAutoAttack(true, true, reason: AutoAttackStopReason.TargetLost, respectWindupLock: true);
                }

                // Classify why the target became invalid (Riot OnTargetLost reason).
                TargetLostReason lostReason;
                if (TargetUnit.IsDead)
                {
                    lostReason = TargetLostReason.Death;
                }
                else if (!TargetUnit.IsTargetableByUnit(this) && !TargetUnit.CharData.IsUseable)
                {
                    lostReason = TargetLostReason.Untargetable;
                }
                else
                {
                    // Alive + targetable, only out of this team's vision → go-to-last-known (Champion-only).
                    // The active target is still cleared below; the lost unit + last-seen position are
                    // remembered for the re-acquire primitives + HeroAI. Only HARD engagements are pursued
                    // (Hero.lua: state != AI_SOFTATTACK) — a soft/attack-move-acquired target is not chased
                    // into the fog. So the soft/hard gate lives here, making IsTargetLost() mean "hard target
                    // lost to vision".
                    lostReason = TargetLostReason.LostVisibility;
                    if (this is Champion && GetAIState() != AIState.AI_SOFTATTACK)
                    {
                        _lostTargetUnit = TargetUnit;
                        _lostTargetLastKnownPosition = TargetUnit.Position;
                    }
                }

                SetTargetUnit(null, true, lostReason);
                return;
            }
            // Soft-attack drop (a-move target leaving acquisition range) is now handled by HeroAI.OnTick
            // (_aiState == AI_SOFTATTACK), not the engine.
            else if (IsAttacking)
            {
                // ar_StopAttackRangeModifier (Constants.var:62, default 100) — the buffer behind
                // the Lua AI API `TargetInCancelAttackRange` (Aggro.lua/MinionOdin.lua/pet AIs):
                // a windup is cancelled once the target leaves range + this slack. Sourced from
                // GlobalData so a config/map override is honoured instead of a magic number. The
                // previous 300 here borrowed ar_ClosingAttackRangeModifier's value (Constants.var:16)
                // — that constant belongs to GetClosestAttackPoint's close-walk scan instead.
                float cancelBuffer = LeagueSandbox.GameServer.Content.GlobalData.AttackRangeVariables.StopAttackRangeModifier;
                float maxCancelRange = Stats.Range.Total + TargetUnit.CollisionRadius + CollisionRadius + cancelBuffer;
                // Minions/monsters auto-attack CLIENT-AUTONOMOUSLY (obj_AI_Minion::UpdatePimpl): once a swing
                // starts, the client always COMPLETES it in place — it has no "target left range" cancel
                // (AIMinionClient.cpp:134-187, no movement/abort path). If the SERVER cancels the windup
                // mid-flight because the target kited out of range + StopAttackRangeModifier, it clears the
                // cooldown and immediately re-enters the chase while the client is still animating that swing
                // → the unit slides forward during the swing (the "looped-attack-then-target-exits" slide,
                // verified t=32567/34334). So for client-autonomous units the windup must run to its damage
                // point (FinishCasting engages the winddown lock), then chase cleanly. Champions/turrets keep
                // the cancel (they are server-driven per swing and orb-walk-cancel via explicit move orders).
                if (Vector2.Distance(TargetUnit.Position, Position) > maxCancelRange
                        && AutoAttackSpell.State == SpellState.STATE_CASTING && !AutoAttackSpell.SpellData.CantCancelWhileWindingUp
                        && this is not Minion)
                {
                    CancelAutoAttack(!HasAutoAttacked, true, reason: AutoAttackStopReason.Moving);
                }

                if (AutoAttackSpell.State == SpellState.STATE_READY)
                {
                    IsAttacking = false;
                }
                return;
            }

            var idealRange = Stats.Range.Total;
            // P3b: the per-tick postponed-cast retry is the named TryToExecutePostponedOrders — Riot's
            // server-side obj_AI_Base::TryToExecutePostponedOrders, confirmed (byte-matched client symbols)
            // to be driven from the AI update tick (the client has ZERO callers — only the input-driven
            // HandleNewOrder runs there). Returns true if it handled a postponed out-of-range cast this
            // tick; otherwise fall through to normal combat.
            if (!TryToExecutePostponedOrders())
            {
                // TODO: Verify if there are any other cases we want to avoid.
                if (TargetUnit != null && TargetUnit.Team != Team && MoveOrder != OrderType.CastSpell)
                {
                    // ChasingAttackRangePercent (4.20 stats.json field, NOT in the 4.17 decomp — inferred
                    // semantics, verify in-game): while CHASING, a unit commits to within attackRange ×
                    // percent before it stops + engages, instead of attacking at the very edge of range
                    // (anti-kite "stick closer"; e.g. Garen 0.5 closes deep, Velkoz 0.8 less). Applied to
                    // the ENGAGE range only — it scales the start-attack gate (below) AND the chase/stop
                    // in RefreshWaypoints (which receives this idealRange), so engage and stop stay
                    // consistent (no attack-while-moving). The DISENGAGE stays at full range: the
                    // IsAttacking cancel uses Stats.Range.Total + buffer. This engage(idealRange) <
                    // disengage(full range + StopAttackRangeModifier) gap IS the anti-churn hysteresis
                    // (a unit that engaged keeps attacking until the target leaves full range), so the
                    // former explicit ATTACK_SETTLE_HYSTERESIS hold-band was redundant and was removed
                    // (it created a dead-zone where a unit held OUT of attack range, not attacking).
                    // Default 0.95 (CharData) ≈ no-op; units whose stats omit the field fall back to
                    // 0.95. Clamped to a sane floor.
                    float chasePct = System.Math.Clamp(CharData?.ChasingAttackRangePercent ?? 0.95f, 0.1f, 1f);
                    idealRange = Stats.Range.Total * chasePct + TargetUnit.CollisionRadius + CollisionRadius;

                    if (Vector2.DistanceSquared(Position, TargetUnit.Position) <= idealRange * idealRange && MovementParameters == null)
                    {
                        if (AutoAttackSpell.State == SpellState.STATE_READY)
                        {
                            // Stops us from continuing to move towards the target.
                            RefreshWaypoints(idealRange);

                            // The chase-stop above is the engine executor (always runs while in range);
                            // STARTING a swing additionally requires the script's auto-attack toggle to be
                            // ON for this target (set by the shared AutoAttackComponent). Riot: "target set"
                            // != auto-fire. (P5.6: this toggle is the sole gate — legacy auto-fire removed.)
                            if (CanAttack() && AutoAttackTogglePermits())
                            {
                                IsNextAutoCrit = RollAutoAttackCrit(TargetUnit);
                                IsNextAutoMiss = RollAutoAttackMiss(TargetUnit);
                                // Dodge is rolled on the TARGET against this attacker (target-side stat).
                                IsNextAutoDodged = (TargetUnit as ObjAIBase)?.RollDodge(this) ?? false;
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
                        // OUT of engage range — about to chase. A client-autonomous unit (minion/monster)
                        // that has an active auto-attack loop (HasMadeInitialAttack) keeps re-swinging IN
                        // PLACE on the client with no further server packets and NO cancel of its own (decomp
                        // AIMinionClient.cpp:134-187 — the client never stops attacking on range loss). If we
                        // start moving it without telling the client, it keeps playing the swing animation
                        // (no server damage) while sliding after the target, then snaps on the next sync —
                        // the "charges its attack while chasing / plays-through-instead-of-running" desync.
                        // So the instant the unit leaves attack range (this branch) we send the client an
                        // NPC_InstantStop_Attack to break its hardcode-attack state, THEN chase. Sent here at
                        // idealRange (not the wider disengage range) so the swing cancels the moment the run
                        // begins — there is no separate hold. forceClient=true forces the stop even mid-loop.
                        // HasMadeInitialAttack is cleared so re-entering range sends a fresh Basic_Attack_Pos
                        // (target re-acquire) and the loop resumes. (We are never IsAttacking here — the
                        // IsAttacking branch above returns first — so this never interrupts a committed windup.)
                        if (this is Minion && HasMadeInitialAttack)
                        {
                            _game.PacketNotifier.NotifyNPC_InstantStop_Attack(this, isSummonerSpell: false,
                                keepAnimating: false, destroyMissile: false, overrideVisibility: false, forceClient: true);
                            HasMadeInitialAttack = false;
                        }
                        RefreshWaypoints(idealRange);
                    }
                }
                else
                {
                    // Order/State split COMPLETE: every champion archetype now owns combat selection —
                    // HeroAI (players, defaulted in Champion ctor), PetAI (pets), BotAI (bots) all set
                    // ScriptOwnsCombatSelection = true and acquire targets in their own script. The old
                    // engine MoveOrder-driven acquisition that used to live here (attack-move acquire +
                    // idle auto-acquire, both gated on !ScriptOwnsCombatSelection) was therefore dead for
                    // champions and never ran for non-champions (minions/monsters/turrets never set
                    // AttackMove and aren't Champions). Removed — the engine no longer selects combat
                    // targets for any unit; it only executes the script-set target (P5).

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

            // Keep the explicit chase-intent in sync with the chase-bearing orders. AttackTo chases its
            // target; a TARGETED TempCastSpell (move-to-cast onto a unit) likewise tracks the cast target
            // (PostponedCastTarget) while the cast is postponed (Riot's Chase + POSTPONED at once). A
            // POSITIONAL TempCastSpell (no cast target) walks to a point → no chase. A later slice sets
            // _chaseIntent directly on combat-engage so the engine no longer reuses MoveOrder for "chasing".
            _chaseIntent = order == OrderType.AttackTo
                || (order == OrderType.TempCastSpell && PostponedCastTarget != null);

            // IssueOrders S2 Phase 2: the order-status lifecycle is NOT driven here anymore. UpdateMoveOrder
            // is called both at the issue point (player order) AND by internal execution-time mutations
            // (RefreshWaypoints auto-promote to AttackTo, Spell.cs post-attack/cast-end restore, ctor) — the
            // latter are not "new orders" in Riot's sense. The PENDING→EXECUTED lifecycle now lives at the
            // single issue point (HandleNewOrder, called from HandleMove); the move-to-cast postpone stays in
            // SetSpellToCast (→ POSTPONED). Still 0 behaviour change (nothing reads OrderStatus for control).

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

            // Order/State coherence for HeroAI champions (P4 part B, P5 prep): the internal Hold-setters
            // (Spell.cs cast/channel/charge end → Hold "stand" fallback) set MoveOrder without touching
            // _aiState, leaving it stale. Sync it to AI_IDLE ("idle, no auto-acquire"). The PLAYER Hold
            // path overwrites this with AI_HARDIDLE right after via HeroAI.OnOrder (HandleMove calls
            // UpdateMoveOrder BEFORE IssueOrder), so only the internal Hold-setters land on AI_IDLE — and
            // HeroAI's Hold OnTick branch is gated on AI_HARDIDLE(_ATTACKING), so an internal-Hold AI_IDLE
            // does NOT acquire/attack (preserved). No behaviour change today; keeps the state honest.
            // Gated to HeroAI champions so the minion/monster/pet/bot state machines are untouched.
            if (MoveOrder == OrderType.Hold && ScriptOwnsCombatSelection)
            {
                _aiState = AIState.AI_IDLE;
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
        /// Whether the saved order can be carried out right now (Riot <c>IssuerInterface::
        /// TryToExecuteOrder</c>, called by <see cref="HandleNewOrder"/> and the per-tick
        /// <see cref="RouteOrder"/>). Riot's <c>obj_AI_Base</c> base returns false and lets each
        /// archetype override the "can I execute" test; for US the orders routed through
        /// <see cref="HandleNewOrder"/> (Move/Attack/Hold/Stop) are already applied synchronously by
        /// the caller (HandleMove builds the path / sets the target), so they are immediately
        /// executable → true. The move-to-cast postpone is handled separately via
        /// <see cref="SetSpellToCast"/> → POSTPONED. Phase 4 adds the real CC/cast gate here.
        /// </summary>
        public virtual bool TryToExecuteOrder()
        {
            return true;
        }

        /// <summary>
        /// Marks the saved order as carried out (Riot <c>obj_AI_Base::ExecuteOrder(IssueOrders&amp;)</c>,
        /// which in 4.17 is purely the <c>NotifyExecuted</c> status advance — the actual move/attack work
        /// lives elsewhere, NOT in this method). NotifyExecuted semantics: advance to EXECUTED unless an
        /// active POSTPONE owns the order (POSTPONED with a real order command stays POSTPONED so the
        /// move-to-cast retry isn't clobbered). The real execution still lives in HandleMove /
        /// RefreshWaypoints this phase; this only advances the status.
        /// </summary>
        public virtual void ExecuteOrder()
        {
            if (OrderStatus != OrderState.Postponed || MoveOrder == OrderType.OrderNone)
            {
                OrderStatus = OrderState.Executed;
            }
        }

        /// <summary>
        /// Issue point for a fresh order (Riot <c>IssueOrders::HandleNewOrder</c>): records the saved-order
        /// tuple (command ≡ <see cref="MoveOrder"/>, set by the caller's UpdateMoveOrder; position + target
        /// here), marks it PENDING, then tries to execute it immediately — executable → ExecuteOrder
        /// (EXECUTED), otherwise it stays PENDING for a later <see cref="RouteOrder"/> retry. This is the
        /// status machine ONLY; it is intentionally separate from the <see cref="IssueOrder"/> → OnOrder
        /// script channel (Riot's HandleNewOrder likewise contains no script callback). Returns true if the
        /// order executed this call.
        /// </summary>
        public bool HandleNewOrder(OrderType order, AttackableUnit target, Vector2 pos)
        {
            // setOrder(order, pos, obj) → records the saved tuple + PENDING. The command itself is the
            // MoveOrder the caller already set via UpdateMoveOrder; we only record pos/obj + the status.
            _savedOrderObj = target;
            _savedOrderPos = pos;
            OrderStatus = OrderState.Pending;

            if (TryToExecuteOrder())
            {
                ExecuteOrder();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Per-tick retry of the saved order (Riot <c>IssueOrders::RouteOrder</c>): re-mark PENDING and
        /// try to execute. Defined here for Phase 2 completeness but NOT yet driven per tick — Phase 3
        /// wires the POSTPONED move-to-cast retry through it (replacing the ad-hoc SpellToCast retry in
        /// UpdateTarget). Returns true if the order executed this call.
        /// </summary>
        public bool RouteOrder()
        {
            OrderStatus = OrderState.Pending;
            if (TryToExecuteOrder())
            {
                ExecuteOrder();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Per-tick retry of a postponed out-of-range cast (move-to-cast) — Riot's server-side
        /// <c>obj_AI_Base::TryToExecutePostponedOrders(bool)</c>, called from the AI update tick. Confirmed
        /// server-side + per-tick by the decompiler: the byte-matched client symbols for this and RouteOrder
        /// have ZERO callers (the client only runs the input-driven HandleNewOrder; the per-tick client
        /// Update merely reads order status to toggle AIManager's AI_WaitForCmd, never executes/retries). So
        /// the postponed re-cast is strictly server-side, and this is where Riot put it.
        ///
        /// Reads the postponed spell (<see cref="SpellToCast"/> + its CursorPos / <see cref="PostponedCastTarget"/>),
        /// re-checks cast range, and re-issues the cast once in range — targeted via PostponedCastTarget,
        /// positional via the stored CursorPos (Riot PostponedSpell stores mTargetPos = ci.CursorPos) — and
        /// chases toward cast range otherwise. Returns true if a postponed cast was handled this tick (the
        /// caller then skips normal combat selection); false if there is none / it cannot run now (no
        /// SpellToCast, mid-attack, or the cast target went invalid).
        ///
        /// Behaviour-identical to the former inline UpdateTarget block (P3a, in-game verified); P3b only
        /// NAMES + relocates it to match Riot's structure. The exact server body is NOT byte-matched — this
        /// is built to the confirmed ROLE using our working P3a logic, not reconstructed from unmatched code.
        /// </summary>
        private bool TryToExecutePostponedOrders()
        {
            if (SpellToCast == null || IsAttacking
                || !(PostponedCastTarget == null
                     || (!PostponedCastTarget.IsDead && SpellToCast.SpellData.IsValidTarget(this, PostponedCastTarget))))
            {
                return false;
            }

            // Spell casts usually do not take into account collision radius, thus range is center -> center VS edge -> edge for attacks.
            float idealRange = SpellToCast.GetCurrentCastRange();

            // Re-aim at CursorPos — the original click — not the (possibly clamped/snapped) TargetPosition.
            // Targeted (PostponedCastTarget != null) vs positional (== null) is distinguished by the cast
            // target (kept separate from the attack TargetUnit — option A). PostponedCastTarget is NOT
            // cleared here — the cast may still be winding up (re-entry casts are rejected while casting);
            // it is dropped together with SpellToCast in SetSpellToCast(null) at FinishCasting.
            if (MoveOrder == OrderType.TempCastSpell
                && PostponedCastTarget != null
                && Vector2.DistanceSquared(PostponedCastTarget.Position, SpellToCast.CastInfo.Owner.Position) <= idealRange * idealRange)
            {
                SpellToCast.Cast(new Vector2(SpellToCast.CastInfo.CursorPos.X, SpellToCast.CastInfo.CursorPos.Z), new Vector2(SpellToCast.CastInfo.TargetPositionEnd.X, SpellToCast.CastInfo.TargetPositionEnd.Z), PostponedCastTarget);
            }
            else if (MoveOrder == OrderType.TempCastSpell
                    && PostponedCastTarget == null
                    && Vector2.DistanceSquared(new Vector2(SpellToCast.CastInfo.CursorPos.X, SpellToCast.CastInfo.CursorPos.Z), SpellToCast.CastInfo.Owner.Position) <= idealRange * idealRange)
            {
                SpellToCast.Cast(new Vector2(SpellToCast.CastInfo.CursorPos.X, SpellToCast.CastInfo.CursorPos.Z), new Vector2(SpellToCast.CastInfo.TargetPositionEnd.X, SpellToCast.CastInfo.TargetPositionEnd.Z));
            }
            else
            {
                RefreshWaypoints(idealRange);
            }
            return true;
        }

        /// <summary>
        /// Routes an incoming order to this unit's AI script so it can translate it into AI state
        /// (Order/State split Phase 1, docs/AI_ORDER_STATE_SPLIT_PLAN.md). Legacy IAIScript AIs and
        /// BaseAIScript AIs that don't override OnOrder are unaffected (no-op default), so this is
        /// purely additive — the order's movement is still applied by the existing caller (HandleMove).
        ///
        /// NOTE: this is the SCRIPT-reaction channel, distinct from <see cref="HandleNewOrder"/> (the
        /// IssueOrders status machine). Riot keeps them separate too — its <c>HandleNewOrder</c> contains
        /// no OnOrder callback. (Naming caveat: Riot's <c>obj_AI_Base::IssueOrder</c> is the order FUNNEL —
        /// clamp + path-build + HandleNewOrder — which is our HandleMove, not this method.)
        /// </summary>
        public void IssueOrder(OrderType order, AttackableUnit target, Vector2 pos)
        {
            (AIScript as Behavior.BaseAIScript)?.OnOrder(order, target, pos);
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
            // Unified through the CharacterDataStack base layer; preserves this unit's current SkinID.
            return CharacterDataStack.SetBase(model, SkinID);
        }

        /// <summary>Sync the skin index when the CharacterDataStack resolves a new top layer.</summary>
        protected override void OnStackSkinResolved(uint skinID)
        {
            if (skinID != CharacterDataStack.KeepSkinID)
            {
                SkinID = (int)skinID;
            }
        }

        /// <summary>
        /// Swap the Q/W/E/R slots to another character's spells on transform (overrideSpells layer)
        /// and restore them on revert. Driven by the CharacterDataStack's resolved spell skin. Levels
        /// and cooldowns carry over (LoL shares spell levels across forms); the client loads the
        /// matching spellbook itself from the ChangeCharacterData useSpells flag, so this only keeps
        /// the server's gameplay slots and the HUD slot data in sync (via SetSpell's notify).
        /// </summary>
        protected override void OnStackSpellSkinResolved(string spellSkinCharacter)
        {
            Content.CharData cd;
            try
            {
                cd = _game.Config.ContentManager.GetCharData(spellSkinCharacter);
            }
            catch (Content.ContentNotFoundException)
            {
                // A model with no spellbook (e.g. a pure-model skin like "SwainBird") — leave spells as-is.
                return;
            }
            if (cd == null)
            {
                return;
            }
            for (byte slot = 0; slot < 4; slot++)
            {
                string newName = cd.SpellNames[slot];
                if (string.IsNullOrEmpty(newName))
                {
                    continue;
                }
                SetSpell(newName, slot, enabled: true);
            }
        }
    }
}
