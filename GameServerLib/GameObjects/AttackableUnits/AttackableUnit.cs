using GameServerCore;
using GameServerCore.Content;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.Content;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.Content.Navigation;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits
{
    /// <summary>
    /// Base class for all attackable units.
    /// AttackableUnits normally follow these guidelines of functionality: Death state, forced movements, Crowd Control, Stats (including modifiers and basic replication), Buffs (and their scripts), and Call for Help.
    /// </summary>
    public class AttackableUnit : GameObject
    {
        // Crucial Vars.
        private float _statUpdateTimer;
        private object _buffsLock;
        private DeathData _death;
        private static ILog _logger = LoggerProvider.GetLogger();

        // Assist bookkeeping (Riot obj_AI_Base::m{Allied,Enemy}AssistMarkers, hoisted from
        // ObjAIBase to AttackableUnit: the 4.20 wire carries assist lists for turret AND
        // dampener deaths, and dampeners are plain AttackableUnits). Only hero sources leave
        // markers (S1 AddAssistMarker gates on IsHero); any attackable victim collects them.
        internal readonly List<AssistMarker> AlliedAssistMarkers = new List<AssistMarker>();
        internal readonly List<AssistMarker> EnemyAssistMarkers = new List<AssistMarker>();

        /// <summary>
        /// Variable containing all data about the this unit's current character such as base health, base mana, whether or not they are melee, base movespeed, per level stats, etc.
        /// </summary>
        public CharData CharData { get; }
        /// <summary>
        /// This unit's classification tags (Champion / Minion / Monster / Ward / Structure_* …), read from
        /// character data. Convenience accessor over <see cref="CharData"/>.UnitTags; query with the
        /// UnitTagExtensions (HasTag / ContainsAny / ContainsAll). See reference_unit_tags_model.
        /// </summary>
        public UnitTag UnitTags => CharData.UnitTags;
        /// <summary>
        /// Epic monsters (Baron Nashor, Dragon, Rift Herald) reject all crowd-control EFFECTS — keyed off
        /// the <see cref="UnitTag.Monster_Epic"/> tag, NOT a tenacity/data field. Replay-verified model
        /// (4.20 SR replays): the generic stun/slow/silence buff DOES get added to the epic (BuffAdd2 sent
        /// at full duration from champion casters — Riot does NOT block at application), but the unit is
        /// never actually CC'd in-game. So the buff object stays (FX/DoT/stat-mods/duration), and only its
        /// CONTROL effect is rejected. Drives the CC-status suppression in <see cref="RecomputeBuffEffects"/>
        /// (stun/root/charm/fear/taunt/silence/suppress/sleep/disarm/nearsight never latch) and, with
        /// <see cref="IsDisplacementImmune"/>, blocks external forced movement. The slow effect (a MoveSpeed
        /// stat-mod, not a status flag) is rejected separately via <see cref="Stats.ImmuneToSlow"/>.
        /// See reference_epic_monster_cc_immunity.
        /// </summary>
        public bool IsCrowdControlImmune => UnitTags.HasTag(UnitTag.Monster_Epic);
        /// <summary>
        /// Riot CharData "Imobile" flag (byte-verified for Baron/Dragon, Immobile=1): this unit cannot be
        /// displaced by EXTERNAL forced movement (knockup / knockback / pull). Self-initiated dashes are
        /// unaffected. Data-driven, so any unit with Imobile=1 is displacement-immune, not only epics.
        /// </summary>
        public bool IsDisplacementImmune => CharData.Immobile;
        /// <summary>
        /// Whether this unit replicates with the PAR (PrimaryAbilityResource) Map-bucket layout: MaxMP/MP
        /// prepended to the Map bucket (var0/var1) with MoveSpeed/scale/targetability shifted +2, and pulled
        /// out of Local1 (ActionState shifts 7→5). Riot keys this off the per-unit AlwaysUpdatePAR flag
        /// (mMinionFlags &amp; 8). Replay-verified (4.20): on Twisted Treeline EVERY minion-class unit (lane
        /// minions, jungle camps, capture altars, relics) uses the PAR layout, whereas on Summoner's Rift the
        /// same unit types are non-PAR (scale stays at Map var3). Because TT lane minions reuse the shared
        /// Blue/Red_Minion data (so the flag can't live purely in per-unit data without breaking SR), we OR
        /// the data flag with the TT map (Map10). Sending the wrong layout makes the client read
        /// mIsTargetableToTeamFlags as the model scale → giant unit. See project_replication_visibility_scoping.
        /// </summary>
        public bool UsesParReplication => CharData.AlwaysUpdatePAR || _game.Map.Id == 10;
        /// <summary>
        /// Whether or not this Unit is dead. Refer to TakeDamage() and Die().
        /// </summary>
        public bool IsDead { get; protected set; }
        /// <summary>
        /// Whether this unit is currently a ZOMBIE — dead but still acting (Karthus Death Defied,
        /// Sion/Yorick revive phases, dead-turret husks). Faithful to Riot's AttackableUnit::bZombie
        /// / IsZombie(). Set by <see cref="Die"/> when the death arms <see cref="DeathData.BecomeZombie"/>;
        /// cleared by <see cref="EndZombie"/> when the keep-alive expires (→ real death). Model B,
        /// faithful to Riot DoDeath (sets bZombie but NOT the dead flag): a zombie has
        /// <see cref="IsDead"/> == FALSE and behaves like a live unit — the two are orthogonal Riot
        /// states (CO_IS_DEAD vs CO_IS_DEAD_OR_ZOMBIE).
        /// </summary>
        public bool IsZombie { get; protected set; }
        /// <summary>
        /// ALIVE / ZOMBIE / DEAD, mirroring Riot's CreateHeroDeathState wire enum.
        /// </summary>
        public HeroDeathState DeathState => IsZombie ? HeroDeathState.ZOMBIE
            : IsDead ? HeroDeathState.DEAD : HeroDeathState.ALIVE;
        /// <summary>
        /// Death data captured when this unit entered the zombie state, replayed by
        /// <see cref="EndZombie"/> to finalize the real death.
        /// </summary>
        protected DeathData _zombieDeath;
        /// <summary>
        /// Engine-clock time (<see cref="Game.GameTime"/>, milliseconds) at which this unit last took
        /// real (post-mitigation > 0) damage. Mirrors Riot's <c>GameObject::mLastTookDamageTime</c>
        /// (GameObject.h:84) read via the Lua <c>GetLastTookDamageTime</c> API. Pair with
        /// <c>ApiFunctionManager.GameTime()</c> for the elapsed-since-combat pattern
        /// (<c>GetTime() - GetLastTookDamageTime()</c>, e.g. out-of-combat regen / TaskDefendStructure).
        /// Defaults to 0 (never damaged).
        /// </summary>
        public float LastTookDamageTime { get; protected set; }
        /// <summary>
        /// This Unit's current internally named model.
        /// </summary>
        public string Model { get; protected set; }
        /// <summary>
        /// Layered model/skin override stack (Riot CharacterDataStack). Drives transforms, object-data
        /// swaps and evolving skins via S2C_ChangeCharacterData / PopCharacterData / PopAllCharacterData.
        /// Scripts use the ApiFunctionManager wrappers (PushCharacterData/PopCharacterData/...);
        /// <see cref="ChangeModel"/> routes through its base layer.
        /// </summary>
        public CharacterDataStack CharacterDataStack { get; private set; }
        /// <summary>
        /// Stats used purely in networking the accompishments or status of units and their gameplay affecting stats.
        /// </summary>
        public Replication Replication { get; protected set; }
        /// <summary>
        /// Variable housing all of this Unit's stats such as health, mana, armor, magic resist, ActionState, etc.
        /// Currently these are only initialized manually by ObjAIBase and ObjBuilding.
        /// </summary>
        public Stats Stats { get; protected set; }
        /// <summary>
        /// Per-unit attack-speed cap overrides (Riot GetMaxAttackSpeedOverride / GetMinAttackSpeedOverride),
        /// set via the API <c>OverrideUnitAttackSpeedCap</c>. 0 = no override. Consumed by
        /// <c>SpellData.GetCharacterAttackDelay</c>: a MAX override lowers the attack-delay floor (1/maxAS),
        /// a MIN override raises the ceiling (1/minAS) — so the server's windup/cycle timing respects the
        /// same cap the client was told about via S2C_UpdateAttackSpeedCapOverrides.
        /// </summary>
        public float MaxAttackSpeedOverride { get; set; }
        public float MinAttackSpeedOverride { get; set; }
        /// <summary>
        /// Variable which stores the number of times a unit has teleported. Used purely for networking.
        /// Resets when reaching byte.MaxValue (255).
        /// </summary>
        public byte TeleportID { get; set; }
        /// <summary>
        /// Array of buff slots which contains all parent buffs (oldest buff of a given name) applied to this AI.
        /// Maximum of 256 slots, hard limit due to packets.
        /// </summary>
        private Buff[] BuffSlots { get; }
        /// <summary>
        /// Dictionary containing all parent buffs (oldest buff of a given name). Used for packets and assigning stacks if a buff of the same name is added.
        /// </summary>
        private Dictionary<string, Buff> ParentBuffs { get; }
        /// <summary>
        /// List of ALL buff instances on this unit (parents AND overlapping stack children). This is
        /// Riot's canonical container: 4.17 Spell::Buff::BuffManager stores a flat
        /// BuffInstanceVector spellBuffs (BuffManager.h, kMaxSpellBuffs = 64) with indexed access and
        /// name lookup — the slot array is only the wire-facing view on top. Removing this in favor
        /// of BuffSlots would invert Riot's model and lose the stack-children,
        /// which never occupy a slot (only the parent of each name does).
        /// </summary>
        private List<Buff> BuffList { get; }

        // Path/waypoint STORAGE moved to Actor (Stage 2, docs/ACTOR_CLASS_EXTRACTION_PLAN.md). These are
        // forwarding shims to AIManager.Actor so the movement/collision/broadcast code still on this class
        // (migrates in later stages) and the external readers (replication, PathingHandler, HandleMove)
        // keep their call sites. Non-AI units (buildings) have no Actor → the fallbacks reproduce the prior
        // ctor-seeded single-point stationary path exactly (OfSingle(Position), key 1).
        /// <summary>
        /// Waypoints that make up the path a game object is walking in.
        /// </summary>
        public NavigationPath Waypoints
        {
            get => (this as ObjAIBase)?.AIManager?.Actor.Waypoints ?? NavigationPath.OfSingle(Position);
            protected set { var a = (this as ObjAIBase)?.AIManager?.Actor; if (a != null) a.Waypoints = value; }
        }
        /// <summary>
        /// Index of the waypoint in the list of waypoints that the object is currently on.
        /// </summary>
        public int CurrentWaypointKey
        {
            get => (this as ObjAIBase)?.AIManager?.Actor.CurrentWaypointKey ?? 1;
            protected set { var a = (this as ObjAIBase)?.AIManager?.Actor; if (a != null) a.CurrentWaypointKey = value; }
        }
        public Vector2 CurrentWaypoint => (this as ObjAIBase)?.AIManager?.Actor.CurrentWaypoint ?? Position;
        private Vector2 OldPoint = new Vector2(0, 0);
        private Vector2 _unreplicatedDrift = Vector2.Zero;
        // Distance walked since the last movement broadcast — drives the Riot-style keepalive
        // cadence for non-champions (replay: walking minions get a WaypointGroup every ~167u
        // ≈ 0.5s at 325 movespeed, REGARDLESS of drift — each update carries Waypoint[0] =
        // current position, so collision drift is folded into small periodic corrections
        // instead of accumulating into one visible snap).
        private float _traveledSinceLastSync;
        // Time (ms) since the last movement broadcast — drives the stopped-unit position keepalive
        // (Riot re-broadcasts a standing unit's IDENTICAL position ~every 0.8s; see Move()).
        private float _timeSinceLastSync;
        // True when the waypoint LIST itself changed since the last broadcast (new order/path)
        // — the next WaypointGroup then carries the full route (client path-preview needs it;
        // Riot's occasional long lists, max 20, are these). Keepalives/drift corrections with
        // an unchanged route are capped to Position + 3 lookahead for ALL units (Riot hero
        // wire: median 2, p90 3 waypoints across 25k updates).
        public bool FullPathBroadcastPending { get; private set; }

        // Temp-ghost stuck recovery (S4 mGettingOutOfCollisionGhosted, consumed in
        // Actor_Common::GetCollisionState, Actor.cpp:2683-2691): past the threshold the unit's
        // collision state flips mIgnoreCollisions. P3 SEMANTICS (corrected 2026-07-19): in 4.17
        // that flag is consumed ONLY by the PATHING queries (HasStuckActor/HasBlockedActor via
        // the non-raw GetCollisionState; the pair rule ActorCollisionState::IgnoreCollisions) —
        // body collision (CanCollide, Actor.cpp:300-304) consults only the buff-ghost flags. So
        // a temp-ghosted unit keeps colliding bodily; its escape is that its own A* ignores all
        // actors (predicate = null) and other units' path queries ignore it. (S1 still consulted
        // the counter in body collision, actor_client.cpp:846 — 4.17 moved it to path level.)
        // Pure server-sim state, never broadcast, distinct from StatusFlags.Ghosted. Threshold
        // mUseSlowerButMoreAccurateSearch-dependent: 45 fast (minions & non-AI default) / 15
        // slower-accurate (champions/pets) — S1:846 confirms the mapping. Lifecycle: ++ per
        // in-collision pathing tick (S1:5044; the 4.17 increment is unrecovered garble), reset
        // on not-in-collision / constrained rebuild ran (:1739/:1858) / stationary / dashing.
        // GhostProof/ForAllies/ForEnemies are the pair-rule flags consumed by CanCollide (now ported
        // faithfully in ShouldIgnoreCollisionDueToGhost). Evidence chain (2026-07-19): they are
        // CharacterState bits (CharacterState.h:50-51, setter chain CharacterState::SetGhostProof* →
        // Actor_Common::SetGhostProof*), i.e. buff/script-layer capability — NOT character data (no
        // key in our stat JSONs NOR the 4.20 client inibins). They stay false by default; nothing
        // sets them yet, so for ordinary units the body collision reduces to the old "skip if the
        // other unit is Ghosted" test. Wire evidence: Azir wall soldiers carry Ghosted +
        // GhostProofForEnemies (ActionState bit13+14, rlp fc985140) — allies pass, enemies blocked —
        // which is exactly what a directional flag on a wall unit must produce. Wiring the flag onto
        // those IceBlock-archetype units server-side is the remaining step.
        // Temp-ghost state (Riot Actor_Common::mGettingOutOfCollisionGhosted) moved to Actor (Stage 5c).
        // Read externally (pathing predicates in PathingHandler) via this forwarding shim; non-AI units
        // (buildings) have no Actor and never temp-ghost.
        public bool IsTemporarilyGhosted => (this as ObjAIBase)?.AIManager?.Actor.IsTemporarilyGhosted ?? false;

        // NOTE (D11, 2026-06-21): the former `_collisionSlideSign` latch + `ResolveSlideSign`
        // helper were removed with the D0 collision rewrite. The default per-tick responder is
        // now the path STEER (SteerPathAroundColliders), which recomputes its slide sign every
        // tick from `signTable[dot(side, m_Movement) > 0]` (S4 CheckActorCollisionResponse,
        // Actor.cpp:403) with NO hysteresis — and the gated position-push fallback does the
        // same. The latch was a server-only invention to emulate the client's m_Movement
        // feedback for the per-tick push; it has no analogue in the decomp and broke parity.

        // RESOLVED 2026-06-07 (mac decomp, Actor.cpp:966-1035): the old
        // ENABLE_ACTORS_SLIDE_INTO_OCCUPIED flag here was a misreading of NSEAI.cfg's
        // `CanActorsSlideIntoOccupiedGridSquares = 1`. The client flag gates (a) clamping the
        // movement to the current cell's box when it crosses into an UNPLANNED cell and (b) the
        // per-cell ACTOR-OCCUPANCY hard-stop (`!nextTestCell.mActorList`) — with 4.20's flag=1
        // both are OFF, and we don't track per-cell occupancy anyway, so we match flag=1 for
        // free. What the client flag does NOT gate is TERRAIN: `nextTestCell.IsPassable()` is
        // checked UNCONDITIONALLY — movement into an unpassable cell is always reverted. Our
        // push applications therefore gate on IsWalkable unconditionally (the flag previously
        // sat in front of these checks as `flag || IsWalkable` and, being true, disabled the
        // terrain gate entirely — pushes could shove units into walls).

        // Stuck-recovery state (Actor_Common m_StuckTimer / m_RepathedCount) moved to Actor with
        // UpdateStuckRecovery (Stage 4, docs/ACTOR_CLASS_EXTRACTION_PLAN.md).
        // True-end goal storage moved to Actor (Stage 2) — forwarding shims (see Waypoints above).
        public bool PathHasTrueEnd
        {
            get => (this as ObjAIBase)?.AIManager?.Actor.PathHasTrueEnd ?? false;
            protected set { var a = (this as ObjAIBase)?.AIManager?.Actor; if (a != null) a.PathHasTrueEnd = value; }
        }
        public Vector2 PathTrueEnd
        {
            get => (this as ObjAIBase)?.AIManager?.Actor.PathTrueEnd ?? Vector2.Zero;
            protected set { var a = (this as ObjAIBase)?.AIManager?.Actor; if (a != null) a.PathTrueEnd = value; }
        }
        private bool _isInGrass = false;
        // _inHardCollision (body-routing trigger) + _inBodyContact (contact-keepalive flag) are now
        // Actor-only state (Stage 5c) — every producer (RunCollisionResponse) and consumer (Move /
        // UpdateStuckRecovery) lives on the Actor, so the unit no longer needs them.

        // NOTE (F3/F6 2026-07-19): the former `_pathFromBodyRouting` push-suppression flag
        // ("routed paths and pushes FIGHT", wire103 glitching) was removed — that interaction was
        // measured in the inverted-radius era (minion bodies at 0.2r); the decomp runs pushes and
        // in-collision repaths concurrently with no such flag (Actor.cpp:1830-1872), and with full
        // radii the routed path and the push agree about the geometry. If sideways resync storms
        // return in clumps, re-measure with COLLISION_LOG before reinventing a gate.
        // _collisionRepathMs / _collisionRepathCount (body-routing throttle, S1 m_RepathedCount analog)
        // moved to Actor with UpdateStuckRecovery (Stage 4).
        /// <summary>
        /// Status effects enabled on this unit. Refer to StatusFlags enum.
        /// </summary>
        /// <summary>
        /// The unit's action-state bitmask. Read-only facade over <see cref="_characterState"/> (Riot's
        /// ref-counted CharacterState model); mutate via <see cref="SetStatus"/> / RecomputeBuffEffects.
        /// </summary>
        public StatusFlags Status => _characterState.Status;
        // Ref-counted action-state backing (mirrors Riot CharacterState): capability disable-holds + plain
        // bits + buff layer. M2 rebuild Phase 1 — see docs/M2_CHARACTERSTATE_REBUILD_PLAN.md.
        private readonly CharacterState _characterState = new CharacterState();

        /// <summary>
        /// Speed scale Riot's server applies while traversing a force-move (the "reduceSpeedSlightly"
        /// factor in obj_AI_Base::MoveForwardAtMaxSpeed, AIBase.cpp:1920). Single source of truth — a
        /// dash covers its distance in distance / (speed * ForceMoveSpeedScale) seconds. Scripts that
        /// time an effect to a force-move's landing use ApiFunctionManager.GetForceMoveTravelTime.
        /// </summary>
        public const float ForceMoveSpeedScale = 0.99f;

        /// <summary>
        /// Parameters of any forced movements (dashes) this unit is performing.
        /// </summary>
        public ForceMovementParameters MovementParameters
        {
            // Riot stores force-move params on the active NavigationPath; we mirror that by keeping the
            // ForceMovementParameters on Waypoints.ForceMovement. This stays the canonical accessor so
            // the poll-sites are untouched. The dash setup (ServerForceLinePath / ServerForceFollowUnitPath)
            // always assigns the path via SetWaypoints before setting these, so Waypoints is non-null
            // when a force-move begins; the null-guard covers construction/reset where no path exists yet.
            get => Waypoints?.ForceMovement;
            protected set
            {
                if (Waypoints != null)
                {
                    Waypoints.ForceMovement = value;
                }
            }
        }

        /// <summary>
        /// Whether this unit is currently under forced movement (dash / leap / engine knock-arc).
        /// Encapsulates the legacy <c>MovementParameters != null</c> poll so consumers (AI scripts,
        /// components) don't reach into the raw field — the backing representation can change in the
        /// forced-movement rewrite (P1b) without touching call-sites. Pairs with the OnMoveBegin/OnMoveEnd
        /// events for transition reactions. See docs/FORCED_MOVEMENT_REWRITE_PLAN.md.
        /// </summary>
        public bool IsForceMoved => MovementParameters != null;
        /// <summary>
        /// Information about this object's icon on the minimap. Deliberately on AttackableUnit,
        /// NOT GameObject: the client resolves
        /// PKT_S2C_UnitSetMinimapIcon strictly through the AttackableUnits manager and drops the
        /// packet for anything else (GameClient.cpp:4457, setter =
        /// AttackableUnitSetMinimapIconOverride) — non-unit objects can never receive an icon
        /// override. Non-unit minimap markers (jungle camps) travel via their own packets
        /// (S2C_CreateMinionCamp, sent by MonsterCamp itself).
        /// </summary>
        public IconInfo IconInfo { get; protected set; }
        /// <summary>
        /// When true, this unit ignores fog-of-war: permanently visible to and spawned for both teams,
        /// like a structure (BaseTurret/ObjBuilding). Used by always-visible objective units such as the
        /// Twisted Treeline Shadow Altars (TT_Buffplat) — they are Minions but, like Riot's altars, must
        /// never be fog-gated (otherwise they only render when a champion is standing on them). Default
        /// false → normal AttackableUnit fog behaviour. IsAffectedByFoW=false alone forces the unit
        /// visible/spawned, so SpawnShouldBeHidden is not consulted in that case.
        /// </summary>
        public bool AlwaysVisible { get; set; } = false;
        /// <summary>
        /// Non-zero = this unit drives a flex particle (e.g. the TT altar capture circle) from its PAR.
        /// The S2C_AttachFlexParticle (flexID 0, cpIndex 0, this attach type) is sent right after the
        /// unit's spawn, per recipient (see NotifyEnterTeamVision). Wire: TT altars use attach type 3
        /// (PAR-driven, NO S2C_HandleCapturePointUpdate — that 0xD3/capture-point path is Dominion-only).
        /// 0 = no flex particle.
        /// </summary>
        public uint CaptureCircleFlexAttachType { get; set; } = 0;
        /// <summary>
        /// The unit's currently-active animation-state overrides (anim class -> override anim). Exposed
        /// so spawn delivery can re-send them to a recipient that wasn't connected when SetAnimStates was
        /// called (e.g. the TT altar's start-locked LOCKLOOP1 override, set at OnMatchStart before any
        /// client has the unit). See NotifyEnterTeamVision's AlwaysVisible branch.
        /// </summary>
        public IReadOnlyDictionary<string, string> ActiveAnimOverrides => animOverrides;
        public override bool IsAffectedByFoW => !AlwaysVisible;
        public override bool SpawnShouldBeHidden => true;

        private bool _teleportedDuringThisFrame = false;
        private List<GameScriptTimer> _scriptTimers;
        internal LinkedList<Shield> Shields { get; } = new LinkedList<Shield>();
        private float _revealSpecificUnitTimer = 0.0f;
        private class AnimOverrideInfo
        {
            public string OverrideValue { get; set; }
            public object Source { get; set; }
        }
        private Dictionary<string, List<AnimOverrideInfo>> _animOverrideStack;
        private Dictionary<string, string> animOverrides;

        public AttackableUnit(
            Game game,
            string model,
            int collisionRadius = 40,
            Vector2 position = new Vector2(),
            int visionRadius = 0,
            uint netId = 0,
            TeamId team = TeamId.TEAM_NEUTRAL,
            Stats stats = null
        ) : base(game, position, collisionRadius, collisionRadius, visionRadius, netId, team)

        {
            Model = model;
            // Base skinID 0 here; ObjAIBase re-seeds it silently once its SkinID is assigned.
            CharacterDataStack = new CharacterDataStack(this, _game, Model, 0);
            CharData = _game.Config.ContentManager.GetCharData(Model);
            if (stats == null)
            {
                var charStats = new Stats();
                charStats.LoadStats(CharData);
                Stats = charStats;
            }
            else
            {
                Stats = stats;
            }

            // Path storage now lives on AIManager.Actor and is seeded in the Actor ctor (Stage 2). For
            // non-AI units (no Actor) the Waypoints/CurrentWaypointKey shims return a single stationary
            // point at Position, so no init is needed here.
            SetStatus(
                StatusFlags.CanAttack | StatusFlags.CanCast |
                StatusFlags.CanMove | StatusFlags.CanMoveEver |
                StatusFlags.Targetable, true
            );
            // CharData NeverRender (TestCubeRender* dummies): mirror the client's load-time
            // CharState.SetNoRender(true) (S4 AIBase.cpp:500) in the replicated state — the
            // client's IsVisible() gates model AND health bar on it, and particles only render
            // with ForceRenderParticles. Without the server-side bit, our CharacterState
            // replication clobbers the client's locally-derived NoRender and the dummy shows a
            // floating health bar (Riot sends NO ShowHealthBar packet for these units).
            if (CharData.NeverRender)
            {
                SetStatus(StatusFlags.NoRender, true);
            }
            MovementParameters = null;
            Stats.AttackSpeedMultiplier.BaseValue = 1.0f;

            _buffsLock = new object();
            // S4 BuffManager allocates 64 buckets (BuffManager.cpp:29, 0x200 bytes / 8-byte stride);
            // GetMaximumRemainingTimeForBuffTypes iterates exactly 0x40. Replay max observed slot = 39.
            BuffSlots = new Buff[64];
            ParentBuffs = new Dictionary<string, Buff>();
            BuffList = new List<Buff>();
            IconInfo = new IconInfo(_game, this);
            _scriptTimers = new List<GameScriptTimer>();
            _animOverrideStack = new Dictionary<string, List<AnimOverrideInfo>>(StringComparer.OrdinalIgnoreCase);
            animOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the HashString for this unit's model. Used for packets so clients know what data to load.
        /// </summary>
        /// <returns>Hashed string of this unit's model.</returns>
        public uint GetObjHash()
        {
            // Riot CharacterPackage::Load (4.17 decomp PackageCharacter.cpp): the package hash is
            // SDBM("[Character]" + character + skinID as "%02d") for EVERY unit, not just champions.
            // Replay-verified: 4.20 FX PackageHashes resolve to "[Character]SRU_Baron00",
            // "[Character]SRU_Dragon00", "[Character]Jinx00", "[Character]JarvanIV06" etc. — no unit
            // ever hashes without the two-digit skin suffix. (Previously only champions got the
            // suffix, so every minion/monster/building package hash was wrong on the wire.)
            int skinId = this is ObjAIBase ai ? ai.SkinID : 0;
            return HashFunctions.HashStringNorm($"[Character]{Model}{skinId:D2}");
        }

        /// <summary>
        /// Sets the server-sided position of this object. Optionally takes into account the AI's current waypoints.
        /// </summary>
        /// <param name="vec">Position to set.</param>
        /// <param name="repath">Whether or not to repath the AI from the given position (assuming it has a path).</param>
        public void SetPosition(Vector2 vec, bool repath = true)
        {
            Position = vec;
            _movementUpdated = true;
            UpdateGrassState();
            // Teleport during a dash cancels the dash — the safe default: the
            // dash's path/remaining distance are computed from its start position, so continuing
            // after a position warp would rubber-band toward a stale endpoint. The case is nearly
            // unreachable in normal play anyway (recalls/blinks can't be cast mid-dash, respawn
            // implies death, and the terrain-collision snap skips dashing units) — this guards
            // script-driven teleports.
            // The real worry — a follow-dash TARGET teleporting away (Vi R / Lee Sin Q2 vs
            // Flash) — is handled by construction and needs nothing here: UpdateForceMovement's
            // FollowNetID branch reads unitToFollow.Position LIVE every tick, so the dash re-aims
            // at the post-teleport position next tick and keeps following (Riot's famous
            // "Vi follows Flash"; their equivalent is Actor_Common::TrackTargetUnit re-deriving
            // velocity from the current target position each tick, Actor.cpp:2256).
            if (MovementParameters != null)
            {
                SetForceMovementState(false, MoveStopReason.ForceMovement);
            }
            else if (IsPathEnded())
            {
                ResetWaypoints();
            }
            else
            {
                // Reevaluate our current path to account for the starting position being changed.
                if (repath)
                {
                    Vector2 safeExit = _game.Map.NavigationGrid.GetClosestTerrainExit(Waypoints.Last(), PathfindingRadius);
                    // Unit aware overload threads the A1 actor-blocked predicate, addressing the
                    // long standing TODO below: pathfinding now does take collision radius into
                    // account via the per-cell HasStuckActor gate. Sharp-corner repath loops
                    // (safe -> unsafe oscillation) should be reduced because the safe path now
                    // routes around the actor that caused the collision in the first place.
                    NavigationPath safePath = _game.Map.PathingHandler.GetPath(this, safeExit);

                    if (safePath != null)
                    {
                        SetWaypoints(safePath);
                    }
                    else
                    {
                        ResetWaypoints();
                    }
                }
                else
                {
                    Waypoints.Replace(0, Position);
                }
            }
        }

        // UpdateStuckRecovery + TryUnstuckRepath moved to Actor (Stage 4,
        // docs/ACTOR_CLASS_EXTRACTION_PLAN.md). Called from Update(); kept as a forwarding shim (no-op for
        // non-AI units). TryUnstuckRepath has no external caller and lives fully on the Actor. The 5 stuck/
        // body-routing fields moved with them; _stuckGhostFrames + IsTemporarilyGhosted stay here (Move()
        // still touches them) and migrate in Stage 5.
        private void UpdateStuckRecovery(float diff) => (this as ObjAIBase)?.AIManager?.Actor.UpdateStuckRecovery(diff);

        public override void Update(float diff)
        {
            using (Profiler.Scope("AttackableUnit.Timers"))
            {
                UpdateTimers(diff);
            }
            using (Profiler.Scope("AttackableUnit.Buffs"))
            {
                UpdateBuffs(diff);
            }
            UpdateRevealSpecificUnit(diff);

            using (Profiler.Scope("AttackableUnit.AssistMarkers"))
            {
                UpdateAssistMarkers();
            }

            // Regen tick: HP/mana regeneration applies in fixed windows of Stats.RegenTickMs (the
            // old "rework stat management" TODO hid a real bug here — Stats.Update was fed the full
            // ACCUMULATED timer while only one window was subtracted, double-applying the remainder:
            // up to ~6% over-regen at 30fps). Stats.Update now applies exactly one window's worth
            // internally (no duration parameter — it was always this constant), the leftover stays
            // queued for the next window, and OnUpdateStats carries the window length
            // (elapsed-per-event; every current consumer ignores it and just recomputes tooltips).
            _statUpdateTimer += diff;
            while (_statUpdateTimer >= StatsNS.Stats.RegenTickMs)
            {
                using var _statsScope = Profiler.Scope("AttackableUnit.StatsTick");
                Stats.Update(this);
                _statUpdateTimer -= StatsNS.Stats.RegenTickMs;
                API.ApiEventManager.OnUpdateStats.Publish(this, StatsNS.Stats.RegenTickMs);
            }

            using (Profiler.Scope("AttackableUnit.Replication"))
            {
                Replication.Update();
            }

            if (CanMove())
            {
                using var _moveScope = Profiler.Scope("AttackableUnit.Move");
                float remainingFrameTime = diff;
                bool moved = false;
                if (MovementParameters != null)
                {
                    remainingFrameTime = UpdateForceMovement(diff);
                    moved = true;
                }
                if (MovementParameters == null)
                {
                    moved = Move(remainingFrameTime);
                }
                if (moved)
                {
                    UpdateGrassState();
                }
                UpdateStuckRecovery(diff);
            }
            UpdateFacing();
            if (IsDead && _death != null)
            {
                Die(_death);
                _death = null;
            }
        }

        /// <summary>
        /// Temporarily marks this unit as specifically revealed through fog/stealth checks.
        /// </summary>
        /// <param name="durationSeconds">Reveal duration in seconds.</param>
        public void RevealSpecificUnit(float durationSeconds)
        {
            if (durationSeconds <= 0.0f)
            {
                return;
            }

            _revealSpecificUnitTimer = Math.Max(_revealSpecificUnitTimer, durationSeconds);
            SetStatus(StatusFlags.RevealSpecificUnit, true);
        }

        private void UpdateRevealSpecificUnit(float diff)
        {
            if (_revealSpecificUnitTimer <= 0.0f)
            {
                return;
            }

            _revealSpecificUnitTimer -= diff / 1000.0f;
            if (_revealSpecificUnitTimer <= 0.0f)
            {
                _revealSpecificUnitTimer = 0.0f;
                SetStatus(StatusFlags.RevealSpecificUnit, false);
            }
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
        }

        void UpdateAssistMarkers()
        {
            // Count guards avoid the per-tick delegate allocation on the (common) empty lists.
            if (AlliedAssistMarkers.Count > 0)
            {
                AlliedAssistMarkers.RemoveAll(x => x.EndTime < _game.GameTime);
            }
            if (EnemyAssistMarkers.Count > 0)
            {
                EnemyAssistMarkers.RemoveAll(x => x.EndTime < _game.GameTime);
            }
        }

        /// <summary>
        /// Champions holding an active enemy assist marker on this unit, excluding the killer —
        /// the Assists list of the ArgsDie announce events (OnTurretDie/OnDampenerDie).
        /// Replay-verified (4.20): the killer is never in the list, even when a minion lands
        /// the killing blow. Ordered by marker start time, matching ChampionDeathHandler.
        /// </summary>
        internal List<Champion> GetEnemyChampionAssists(AttackableUnit excluded = null)
        {
            var assists = new List<Champion>();
            foreach (var marker in EnemyAssistMarkers.OrderBy(x => x.StartTime))
            {
                if (marker.EndTime >= _game.GameTime && marker.Source is Champion c && c != excluded)
                {
                    assists.Add(c);
                }
            }
            return assists;
        }

        protected virtual void UpdateFacing()
        {
            if (Waypoints.Count - CurrentWaypointKey != 0)
            {
                if (OldPoint != CurrentWaypoint)
                {
                    var dir = Vector2.Normalize(CurrentWaypoint - Position);
                    Direction = new Vector3(dir.X, 0, dir.Y);
                    OldPoint = CurrentWaypoint;
                }
            }
        }
        /// <summary>
        /// Called when this unit collides with the terrain or with another GameObject. Refer to CollisionHandler for exact cases.
        /// </summary>
        /// <param name="collider">GameObject that collided with this AI. Null if terrain.</param>
        /// <param name="isTerrain">Whether or not this AI collided with terrain.</param>
        public override void OnCollision(GameObject collider, bool isTerrain = false)
        {
            // Fires per collision per tick. The pathing port changed the terrain branch to
            // SetPosition(repath: true), so each terrain collision now runs a full SafePath A*
            // (previously a cheap position snap). With crowds wedged against walls/buildings this
            // can mean many A* searches per tick — the prime regression suspect. Scoped "pathing"
            // so the trace surfaces collision-driven repath cost. (Returns before any real work for
            // missiles/sectors/buildings, so those add only a near-zero slice.)
            using var _scope = Profiler.Scope("AttackableUnit.OnCollision", "pathing");

            if (collider is SpellMissile || collider is ObjBuilding || (collider is Region region && region.CollisionUnit == this))
            {
                return;
            }

            if (isTerrain)
            {
                ApiEventManager.OnCollisionTerrain.Publish(this);
                if (MovementParameters != null) return;

                // MINIMAL exit (2026-06-07): the trigger above fires at radius 0 (center on a
                // blocked cell — float-precision grazes while skirting building footprints).
                // The old exit used PathfindingRadius+1 full-body clearance: a 50-90u position
                // jump for a boundary graze, broadcast as a WaypointGroup whose Waypoint[0] the
                // client HARD-SNAPS to (ClientFollowServerPath teleports on receive) — the
                // "snapping while pathing around nexus/turrets" artifact. The client never
                // snaps here at all (unpassable movement is reverted, stuck handling reroutes).
                // We keep the escape-snap for genuine wedge cases (spawned/knocked INTO a
                // footprint) but make it minimal: just get the center out of the blocked cell;
                // the radius-aware SafePath repath below routes the body out cleanly.
                Vector2 exit = _game.Map.NavigationGrid.GetClosestTerrainExit(Position, 1.0f);

                // GRAZE vs WEDGE split (2026-06-07): a corner graze (center clips a blocked
                // cell edge for one tick while skirting a building footprint — the path itself
                // does NOT lead into the blob, A* already routed around it) must not repath:
                // SetPosition(repath:true) rewrote the route and broadcast the FULL new path,
                // and the client hard-snaps to its Waypoint[0] on receive — the rare visible
                // snap/zigzag at building edges. The client reverts unpassable movement
                // silently and keeps walking its path; mirror that with a repath:false nudge
                // (keeps Waypoints, Waypoint[0] := Position, only a tiny correction goes out).
                // Repeated grazes degenerate into 1-2u nudges; genuine stuck cases are caught
                // by UpdateStuckRecovery.
                //
                // Deep wedge (spawned/knocked INTO a footprint, exit more than a cell away):
                // keep the full SafePath repath — with repath:false the goal-side waypoints
                // still point INTO the blocker and the unit drifts deeper each tick (the old
                // "clicking beyond an inhibitor pushes the unit further INTO it" bug).
                float grazeThreshold = _game.Map.NavigationGrid.CellSize;
                bool isGraze = Vector2.DistanceSquared(exit, Position) <= grazeThreshold * grazeThreshold;
                SetPosition(exit, repath: !isGraze);
            }
            else
            {
                ApiEventManager.OnCollision.Publish(this, collider);
            }
        }
        public override void Sync(int userId, TeamId team, bool visible, bool forceSpawn = false)
        {
            base.Sync(userId, team, visible, forceSpawn);
            IconInfo.Sync(userId, visible, forceSpawn);
        }

        protected override void OnSync(int userId, TeamId team)
        {
            if (Replication.Changed)
            {
                _game.PacketNotifier.HoldReplicationDataUntilOnReplicationNotification(this, userId, true);
            }
            if (_movementUpdated)
            {
                _game.PacketNotifier.HoldMovementDataUntilWaypointGroupNotification(this, userId, _teleportedDuringThisFrame);
            }
        }

        public override void OnAfterSync()
        {
            Replication.MarkAsUnchanged();
            _teleportedDuringThisFrame = false;
            if (_movementUpdated)
            {
                // The packet that just went out used the current Position as origin, so the
                // accumulated drift is now reflected on the client.
                _unreplicatedDrift = Vector2.Zero;
                _traveledSinceLastSync = 0f;
                _timeSinceLastSync = 0f;
                FullPathBroadcastPending = false;
            }
            _movementUpdated = false;
        }

        /// <summary>
        /// Returns whether or not this unit is targetable to the specified team.
        /// </summary>
        /// <param name="team">TeamId to check for.</param>
        /// <returns>True/False.</returns>
        public bool GetIsTargetableToTeam(TeamId team)
        {
            if (!Status.HasFlag(StatusFlags.Targetable))
            {
                return false;
            }

            if (Team == team)
            {
                return !Stats.IsTargetableToTeam.HasFlag(SpellDataFlags.NonTargetableAlly);
            }

            return !Stats.IsTargetableToTeam.HasFlag(SpellDataFlags.NonTargetableEnemy);
        }

        /// <summary>
        /// Whether this unit can be targeted by <paramref name="acquirer"/> — Riot's
        /// AttackableUnit::IsTargetableByUnit. The base check is global + per-team targetability
        /// (<see cref="GetIsTargetableToTeam"/>); <see cref="AI.Minion"/> overrides it to add the
        /// minion-acquirer gates (minions skip wards and non-minion-acquirable units).
        /// </summary>
        public virtual bool IsTargetableByUnit(AttackableUnit acquirer)
        {
            return GetIsTargetableToTeam(acquirer.Team);
        }

        /// <summary>
        /// Sets whether or not this unit is targetable to the specified team.
        /// </summary>
        /// <param name="team">TeamId to change.</param>
        /// <param name="targetable">True/False.</param>
        public void SetIsTargetableToTeam(TeamId team, bool targetable)
        {
            Stats.IsTargetableToTeam &= ~SpellDataFlags.TargetableToAll;
            if (team == Team)
            {
                if (!targetable)
                {
                    Stats.IsTargetableToTeam |= SpellDataFlags.NonTargetableAlly;
                }
                else
                {
                    Stats.IsTargetableToTeam &= ~SpellDataFlags.NonTargetableAlly;
                }
            }
            else
            {
                if (!targetable)
                {
                    Stats.IsTargetableToTeam |= SpellDataFlags.NonTargetableEnemy;
                }
                else
                {
                    Stats.IsTargetableToTeam &= ~SpellDataFlags.NonTargetableEnemy;
                }
            }
        }

        /// <summary>
        /// Whether or not this unit can move itself.
        /// </summary>
        /// <returns></returns>
        public virtual bool CanMove()
        {
            // Only case where AttackableUnit should move is if it is forced.
            return MovementParameters != null;
        }

        /// <summary>
        /// Whether or not this unit can modify its Waypoints.
        /// </summary>
        public virtual bool CanChangeWaypoints()
        {
            // Only case where we can change waypoints is if we are being forced to move towards a target.
            return MovementParameters != null && MovementParameters.FollowNetID != 0;
        }

        /// <summary>
        /// Whether or not this unit can take damage of the given type.
        /// </summary>
        /// <param name="type">Type of damage to check.</param>
        /// <returns>True/False</returns>
        public bool CanTakeDamage(DamageType type)
        {
            if (Status.HasFlag(StatusFlags.Invulnerable))
            {
                return false;
            }

            switch (type)
            {
                case DamageType.DAMAGE_TYPE_PHYSICAL:
                    {
                        if (Status.HasFlag(StatusFlags.PhysicalImmune))
                        {
                            return false;
                        }
                        break;
                    }
                case DamageType.DAMAGE_TYPE_MAGICAL:
                    {
                        if (Status.HasFlag(StatusFlags.MagicImmune))
                        {
                            return false;
                        }
                        break;
                    }
                case DamageType.DAMAGE_TYPE_MIXED:
                    {
                        if (Status.HasFlag(StatusFlags.MagicImmune) || Status.HasFlag(StatusFlags.PhysicalImmune))
                        {
                            return false;
                        }
                        break;
                    }
            }

            return true;
        }

        /// <summary>
        /// Adds a modifier to this unit's stats, ex: Armor, Attack Damage, Movespeed, etc.
        /// </summary>
        /// <param name="statModifier">Modifier to add.</param>
        public void AddStatModifier(StatsModifier statModifier)
        {
            Stats.AddModifier(statModifier);
            ApiEventManager.OnStatModified.Publish(this, statModifier);
        }

        /// <summary>
        /// Removes the given stat modifier instance from this unit.
        /// </summary>
        /// <param name="statModifier">Stat modifier instance to remove.</param>
        public void RemoveStatModifier(StatsModifier statModifier)
        {
            Stats.RemoveModifier(statModifier);
            ApiEventManager.OnStatModified.Publish(this, statModifier);
        }

        /// <summary>
        /// Gets the current primary ability resource value for this unit.
        /// </summary>
        /// <returns>Current PAR value.</returns>
        public virtual float GetPAR()
        {
            return Stats.CurrentMana;
        }

        /// <summary>
        /// Gets the maximum primary ability resource value for this unit.
        /// </summary>
        /// <returns>Maximum PAR value.</returns>
        public virtual float GetMaxPAR()
        {
            return Stats.ManaPoints.Total;
        }

        /// <summary>
        /// Gets this unit's primary ability resource as a ratio of current to max.
        /// </summary>
        /// <returns>PAR ratio in range 0.0 to 1.0.</returns>
        public virtual float GetPARPercent()
        {
            var maxPar = GetMaxPAR();
            if (maxPar <= 0.0f)
            {
                return 0.0f;
            }

            return GetPAR() / maxPar;
        }

        /// <summary>
        /// Checks whether this unit uses the specified primary ability resource type.
        /// </summary>
        /// <param name="parType">PAR type to compare against.</param>
        /// <returns>True if this unit's PAR type matches; otherwise false.</returns>
        public virtual bool HasPARType(PrimaryAbilityResourceType parType)
        {
            return Stats.ParType == parType;
        }

        /// <summary>
        /// Checks whether this unit can be treated as using the specified PAR type.
        /// </summary>
        /// <param name="parType">PAR type requirement.</param>
        /// <returns>True if the PAR type is compatible; otherwise false.</returns>
        public virtual bool HasCompatiblePARType(PrimaryAbilityResourceType parType)
        {
            if (parType == PrimaryAbilityResourceType.Other)
            {
                return Stats.ParType != PrimaryAbilityResourceType.None;
            }

            return HasPARType(parType);
        }

        /// <summary>
        /// Checks whether this unit has at least the specified PAR amount.
        /// </summary>
        /// <param name="amount">Required PAR amount.</param>
        /// <returns>True if this unit has enough PAR; otherwise false.</returns>
        public virtual bool HasEnoughPAR(float amount)
        {
            return amount <= 0.0f || GetPAR() >= amount;
        }

        /// <summary>
        /// Increases this unit's PAR by the given amount up to the maximum value.
        /// </summary>
        /// <param name="source">Unit credited as the source of the PAR gain.</param>
        /// <param name="amount">Requested PAR amount to add.</param>
        /// <returns>Actual PAR amount added after clamping.</returns>
        public virtual float IncreasePAR(AttackableUnit source, float amount)
        {
            if (amount <= 0.0f)
            {
                return 0.0f;
            }

            var maxPar = GetMaxPAR();
            var previousPar = GetPAR();
            if (maxPar <= 0.0f || previousPar >= maxPar)
            {
                return 0.0f;
            }

            Stats.CurrentMana = Math.Clamp(previousPar + amount, 0.0f, maxPar);
            var actualGain = Stats.CurrentMana - previousPar;
            if (actualGain > 0.0f)
            {
                ApiEventManager.OnAddPAR.Publish(this, source ?? this);
            }

            return actualGain;
        }

        /// <summary>
        /// Spends this unit's PAR by the given amount down to zero.
        /// </summary>
        /// <param name="amount">Requested PAR amount to spend.</param>
        /// <returns>Actual PAR amount spent after clamping.</returns>
        public virtual float SpendPAR(float amount)
        {
            if (amount <= 0.0f)
            {
                return 0.0f;
            }

            var previousPar = GetPAR();
            if (previousPar <= 0.0f)
            {
                return 0.0f;
            }

            Stats.CurrentMana = Math.Max(0.0f, previousPar - amount);
            return previousPar - Stats.CurrentMana;
        }

        /// <summary>
        /// Checks if healing can be received
        /// </summary>
        protected virtual bool CanReceiveHealing() {
            return this is not ObjBuilding and not BaseTurret;
        }

        /// <summary>
        /// Applies healing to this unit.
        /// </summary>
        /// <param name="caster">Unit that is casting to heal.</param>
        /// <param name="amount">The heal amount.</param>
        /// <param name="healType">Type of heal received. </param>
        public virtual void TakeHeal(AttackableUnit caster, float amount, HealType healType, IEventSource sourceScript = null) {
            if (amount <= 0.0f || IsDead || Stats.CurrentHealth <= 0.0f || !CanReceiveHealing()) return;

            var healer = caster ?? this;
            var data = new HealData {
                Healer                     = healer,
                OriginalHealAmount         = amount,
                HealAmount                 = amount,
                PostModificationHealAmount = 0.0f,
                HealType                   = healType,
                Target                     = this
            };

            ApiEventManager.OnCastHeal.Publish(healer, data);
            ApiEventManager.OnHeal.Publish(this, data);

            var amountToApply = Math.Max(0.0f, data.HealAmount);
            var previousHealth = Stats.CurrentHealth;
            Stats.CurrentHealth = Math.Clamp(previousHealth + amountToApply, 0.0f, Stats.HealthPoints.Total);
            data.PostModificationHealAmount = Stats.CurrentHealth - previousHealth;
        }

        /// <summary>
        /// Applies damage to this unit.
        /// </summary>
        /// <param name="attacker">Unit that is dealing the damage.</param>
        /// <param name="damage">Amount of damage to deal.</param>
        /// <param name="type">Whether the damage is physical, magical, or true.</param>
        /// <param name="source">What the damage came from: attack, spell, summoner spell, or passive.</param>
        /// <param name="damageText">Type of damage the damage text should be.</param>
        public DamageData TakeDamage(AttackableUnit attacker, float damage, DamageType type, DamageSource source, DamageResultType damageText, IEventSource sourceScript = null, AttackableUnit callForHelpAttacker = null)
        {
            // Return-shape note (resolves an old "make all TakeDamage return DamageData" TODO): the
            // two parameter-based overloads return the DamageData they construct (this one and the
            // isCrit wrapper) — that's where the caller doesn't have it yet. The DamageData-taking
            // overloads stay void ON PURPOSE: their caller already owns the instance, and the central
            // sink mutates it in place (PostMitigationDamage & co.), so a return would be redundant
            // and imply a fresh object.
            DamageData damageData = new DamageData
            {
                // BB CallForHelpAttackerVar (Riot cfhAttackerID) — split allied aggro credit from the
                // damage attacker for summoned-object damage. Null → falls back to attacker.
                CallForHelpAttacker = callForHelpAttacker,
                // IsAutoAttack means a GENUINE basic-attack swing, and that is set explicitly by
                // ObjAIBase.AutoAttackHit (the only real auto path). Script-dealt damage — including
                // on-hit SPELLS that use DAMAGE_SOURCE_ATTACK to proc on-hit effects (Alpha Strike,
                // Yasuo Q, Ezreal Q, ...) — is never a basic attack, so it must be false here.
                // Scripts that want "attack-source damage" should test DamageSource, not IsAutoAttack.
                IsAutoAttack = false,
                Attacker = attacker,
                Target = this,
                Damage = damage,
                // PostMitigationDamage is computed in the central TakeDamage sink (mitigation moved there).
                DamageSource = source,
                DamageType = type,
                DamageResultType = damageText
            };
            this.TakeDamage(damageData, damageText, sourceScript);
            return damageData;
        }

        DamageResultType Bool2Crit(bool isCrit)
        {
            if (isCrit)
            {
                return DamageResultType.RESULT_CRITICAL;
            }
            return DamageResultType.RESULT_NORMAL;
        }

        /// <summary>
        /// Applies damage to this unit.
        /// </summary>
        /// <param name="attacker">Unit that is dealing the damage.</param>
        /// <param name="damage">Amount of damage to deal.</param>
        /// <param name="type">Whether the damage is physical, magical, or true.</param>
        /// <param name="source">What the damage came from: attack, spell, summoner spell, or passive.</param>
        /// <param name="isCrit">Whether or not the damage text should be shown as a crit.</param>
        public DamageData TakeDamage(AttackableUnit attacker, float damage, DamageType type, DamageSource source, bool isCrit, IEventSource sourceScript = null, AttackableUnit callForHelpAttacker = null)
        {
            return TakeDamage(attacker, damage, type, source, Bool2Crit(isCrit), sourceScript, callForHelpAttacker);
        }

        public void TakeDamage(DamageData damageData, bool isCrit, IEventSource sourceScript = null)
        {
            this.TakeDamage(damageData, Bool2Crit(isCrit), sourceScript);
        }

        /// <summary>
        /// Applies damage to this unit.
        /// </summary>
        /// <param name="attacker">Unit that is dealing the damage.</param>
        /// <param name="damage">Amount of damage to deal.</param>
        /// <param name="type">Whether the damage is physical, magical, or true.</param>
        /// <param name="source">What the damage came from: attack, spell, summoner spell, or passive.</param>
        /// <param name="damageText">Type of damage the damage text should be.</param>
        /// <summary>Actor category for the dr_ attack-ratio lookup (Hero / Unit / Building).</summary>
        private enum ActorDamageClass { Hero, Unit, Building }

        /// <summary>
        /// Classifies a unit for the dr_ actor-type attack ratios: champions are Hero; turrets, inhibitors
        /// and the nexus are Building (mirrors AITurret / obj_Building -> Building in the decomp); everything
        /// else attackable (minions, monsters, pets, wards) is Unit (obj_AI_Minion -> Unit). Matches the
        /// existing structure test at <c>this is not ObjBuilding and not BaseTurret</c>.
        /// </summary>
        private static ActorDamageClass ClassifyForAttackRatio(AttackableUnit unit)
        {
            if (unit is Champion)
            {
                return ActorDamageClass.Hero;
            }
            if (unit is ObjBuilding or BaseTurret)
            {
                return ActorDamageClass.Building;
            }
            return ActorDamageClass.Unit;
        }

        /// <summary>
        /// The dr_ basic-attack damage multiplier for <paramref name="attacker"/> hitting
        /// <paramref name="target"/> (GlobalData.DamageRatios, loaded from Constants.var). The decomp's
        /// attacker getters collapse a Turret target into the Building ratio, so target types reduce to the
        /// same three categories as source types — a clean 3×3 lookup over the nine dr_ values.
        /// </summary>
        private static float GetActorTypeAttackRatio(AttackableUnit attacker, AttackableUnit target)
        {
            var ratios = GlobalData.DamageRatios;
            return (ClassifyForAttackRatio(attacker), ClassifyForAttackRatio(target)) switch
            {
                (ActorDamageClass.Hero, ActorDamageClass.Hero) => ratios.HeroToHero,
                (ActorDamageClass.Hero, ActorDamageClass.Unit) => ratios.HeroToUnit,
                (ActorDamageClass.Hero, ActorDamageClass.Building) => ratios.HeroToBuilding,
                (ActorDamageClass.Unit, ActorDamageClass.Hero) => ratios.UnitToHero,
                (ActorDamageClass.Unit, ActorDamageClass.Unit) => ratios.UnitToUnit,
                (ActorDamageClass.Unit, ActorDamageClass.Building) => ratios.UnitToBuilding,
                (ActorDamageClass.Building, ActorDamageClass.Hero) => ratios.BuildingToHero,
                (ActorDamageClass.Building, ActorDamageClass.Unit) => ratios.BuildingToUnit,
                (ActorDamageClass.Building, ActorDamageClass.Building) => ratios.BuildingToBuilding,
                _ => 1.0f
            };
        }

        public virtual void TakeDamage(DamageData damageData, DamageResultType damageText, IEventSource sourceScript = null)
        {
            // Riot OnPreDamage: fires on the RAW damage BEFORE mitigation. Scripts may modify
            // damageData.Damage here (the engine mitigates the modified value below). Published for both
            // attacker- and target-side buffs.
            ApiEventManager.OnPreDamage.Publish(damageData.Attacker, damageData);
            ApiEventManager.OnPreDamage.Publish(damageData.Target, damageData);

            // dr_ actor-type attack ratios (Constants.var): a basic attack's damage is scaled by a
            // source-type × target-type multiplier (dr_UnitToHero=0.6 — minions deal 60% to champions;
            // dr_UnitToBuilding=0.5; dr_BuildingToUnit=1.25 — turrets deal 125% to minions; the rest are 1.0).
            // Gated on IsAutoAttack — the decomp method family is obj_AI_*::GetAttackRatioWhenAttacking*
            // (mac 4.17), i.e. GENUINE basic-attack swings only; on-hit spells (DAMAGE_SOURCE_ATTACK but not
            // IsAutoAttack) and the turret execute are untouched. NOTE: the decomp exposes the source×target
            // getter structure but its multiply SITE was not recovered, so we apply it to the raw
            // pre-mitigation attack damage here (see docs/CONSTANTS_VAR_AUDIT.md).
            if (damageData.IsAutoAttack && damageData.Attacker != null)
            {
                damageData.Damage *= GetActorTypeAttackRatio(damageData.Attacker, damageData.Target);
            }

            // Armor/MR mitigation lives HERE in the central damage sink, not at DamageData construction.
            // Placed after OnPreDamage (pre-mitigation) but before the on-hit / OnPreDeal/Take hooks so
            // those observe the mitigated value (same as the old construction-time behaviour).
            damageData.PostMitigationDamage = Stats.GetPostMitigationDamage(damageData.Damage, damageData.DamageType, damageData.Attacker);

            var targetIsWard = damageData.Target is Minion { IsWard: true };
            if (damageData.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK)
            {
                if (damageData.DamageResultType == DamageResultType.RESULT_MISS)
                {
                    // Missed (e.g. Blind): the attack never connected — fire only the miss signals,
                    // NOT OnBeingHit / OnHitUnit (no on-hit procs on a missed attack). Both sides fire,
                    // mirroring the dodge pair: OnMiss on the attacker ("I missed"), OnBeingMissed on the
                    // defender ("an attack against me missed").
                    ApiEventManager.OnMiss.Publish(damageData.Attacker, damageData.Target);
                    ApiEventManager.OnBeingMissed.Publish(damageData.Target, damageData.Attacker);
                }
                else if (damageData.DamageResultType == DamageResultType.RESULT_DODGE)
                {
                    // Dodged (e.g. Jax E): same — only the dodge signals, no on-hit procs.
                    ApiEventManager.OnDodge.Publish(damageData.Target, damageData.Attacker);
                    ApiEventManager.OnBeingDodged.Publish(damageData.Attacker, damageData.Target);
                }
                else
                {
                    // Attack connected: fire on-hit reactions + the on-hit proc pipeline.
                    ApiEventManager.OnBeingHit.Publish(damageData.Target, damageData.Attacker);

                    // Wards should not trigger on-hit proc pipelines; therefore each basic attack consumes only one ward hit.
                    if (!targetIsWard)
                        ApiEventManager.OnHitUnit.Publish(damageData.Attacker as ObjAIBase, damageData);
                }
            }

            float healRatio = 0.0f;
            var attacker = damageData.Attacker;
            var attackerStats = damageData.Attacker.Stats;
            var type = damageData.DamageType;
            var source = damageData.DamageSource;

            ApiEventManager.OnPreDealDamage.Publish(damageData.Attacker, damageData);
            ApiEventManager.OnPreTakeDamage.Publish(damageData.Target, damageData);
            var postMitigationDamage = damageData.PostMitigationDamage;
            if (GlobalData.SpellVampVariables.SpellVampRatios.TryGetValue(source, out float ratio) || source == DamageSource.DAMAGE_SOURCE_ATTACK)
            {
                switch (source)
                {
                    case DamageSource.DAMAGE_SOURCE_SPELL:
                    case DamageSource.DAMAGE_SOURCE_SPELLAOE:
                    case DamageSource.DAMAGE_SOURCE_SPELLPERSIST:
                    case DamageSource.DAMAGE_SOURCE_PERIODIC:
                    case DamageSource.DAMAGE_SOURCE_PROC:
                    case DamageSource.DAMAGE_SOURCE_REACTIVE:
                    case DamageSource.DAMAGE_SOURCE_ONDEATH:
                    case DamageSource.DAMAGE_SOURCE_PET:
                        healRatio = attackerStats.SpellVamp.Total * ratio;
                        break;
                    case DamageSource.DAMAGE_SOURCE_ATTACK:
                        // Riot IsLifestealImmune: a target flagged lifesteal-immune (turrets, wards/props)
                        // grants the attacker NO life steal. `this` is the target being hit.
                        healRatio = Stats.IsLifestealImmune ? 0.0f : attackerStats.LifeSteal.Total;
                        break;
                }
            }

            // Any attackable victim tracks champion damagers — replay-verified (4.20): the
            // OnTurretDie/OnDampenerDie announce events carry assist lists, so markers can't
            // be champion-victim-only. S1 AddAssistMarker only gates on the SOURCE being a hero.
            if (damageData.Attacker is Champion cAttacker)
            {
                AddAssistMarker(cAttacker, GlobalData.ChampionVariables.TimerForAssist, damageData);
            }

            if (!CanTakeDamage(type))
            {
                return;
            }
            if (HasShield())
            {
                ConsumeShields(damageData);
                postMitigationDamage = damageData.PostMitigationDamage;
            }
            damageData.PostMitigationDamage = postMitigationDamage;
            Stats.CurrentHealth = Math.Max(0.0f, Stats.CurrentHealth - postMitigationDamage);

            // Riot GameObject::mLastTookDamageTime: stamp the engine clock whenever real damage lands.
            // Drives the Lua GetLastTookDamageTime / out-of-combat "time since combat" pattern.
            if (postMitigationDamage > 0.0f)
            {
                LastTookDamageTime = _game.GameTime;
            }

            if (attacker != null && attacker.Team != Team && postMitigationDamage > 0.0f)
            {
                var revealRange = GlobalData.AttackFlags.RevealAttackerRange;
                if (Vector2.DistanceSquared(attacker.Position, Position) <= revealRange * revealRange)
                {
                    attacker.RevealSpecificUnit(GlobalData.AttackFlags.RevealAttackerTimeOut);
                }
            }

            ApiEventManager.OnDealDamage.Publish(damageData.Attacker, damageData);
            ApiEventManager.OnTakeDamage.Publish(damageData.Target, damageData);
            
            // A zombie sits at 0 HP but is NOT dead (Model B: IsDead=false while IsZombie). Guard
            // against re-triggering Die() when it takes further damage — its real death is driven by
            // EndZombie() (the keep-alive expiry), not by another health-cross-zero.
            if (!IsDead && !IsZombie && Stats.CurrentHealth <= 0)
            {
                IsDead = true;
                _death = new DeathData
                {
                    // Default false; a death-reactive buff arms this from its OnDeath handler during
                    // Die() (Riot: BecomeZombie var set in BuffOnDeath → bZombie). See the zombie
                    // branch in Die() / OnZombie.
                    BecomeZombie = false,
                    // Dead wire field: the client never consumes the packet's DieType — DoNPCDie
                    // ignores it, and the NPC_Die_Broadcast path even recomputes it locally from
                    // the unit type (minion/pet=MINION_DIE 0, else NETURAL_DIE 1; AIBase.cpp
                    // DoDieBroadcast). Replay-verified: all 743 death packets in the 4.20 capture
                    // carry 0. Riot's own server sends 0 unconditionally.
                    DieType = 0,
                    Unit = this,
                    Killer = attacker,
                    DamageType = type,
                    DamageSource = source,
                    // Only meaningful for hero deaths (client GetRespawnTimeRemaining → death
                    // timer UI); NotifyNPC_Hero_Die injects the real champ.RespawnTimer at the
                    // packet level. For non-heroes Riot's server sends uninitialized garbage
                    // (replay shows wild exponents and even ASCII "BUFF" fragments in the float),
                    // which the client stores but never uses — clean 0 is the faithful value.
                    DeathDuration = 0
                };
            }

            if (attacker.Team != Team)
            {
                _game.PacketNotifier.NotifyUnitApplyDamage(damageData, _game.Config.IsDamageTextGlobal);
            }

            // Get health from lifesteal/spellvamp
            if (healRatio > 0)
            {
                var healAmount = healRatio * postMitigationDamage;
                if (healAmount > 0.0f)
                {
                    attacker.TakeHeal(attacker, healAmount,
                        source == DamageSource.DAMAGE_SOURCE_ATTACK ? HealType.LifeSteal : HealType.SpellVamp,
                        sourceScript);
                }
            }
        }

        // (A stub "IsInDistress()" lived here — deleted: zero callers, no Riot counterpart in any
        // corpus (4.17 decomp, S1 server, S1 Lua). Riot models "ally in distress" event-driven, not
        // as unit state: HandleCallForHelp fires per damage event, and our port does the same —
        // the TakeDamage CFH broadcast + ClassifyTarget(target, victim)'s contextual
        // "X attacking Y" rows ARE the distress handling.)

        /// <summary>
        /// Function called when this unit's health drops to 0 or less.
        /// </summary>
        /// <param name="data">Data of the death.</param>
        public virtual void Die(DeathData data)
        {
            //_game.ObjectManager.RefreshUnitVision(this);
            _game.ObjectManager.StopTargeting(this);
            // Stop + clear the path on death (Riot DoDeath zeroes Velocity). Matters for zombies,
            // which stay in the world rather than being removed — otherwise they keep sliding to
            // their last waypoint.
            StopMovement(MoveStopReason.Death);

            // Riot obj_AI_Base::DoDeath fires HandleDeath (EVENT_ON_DIE + buff OnDeath hooks) BEFORE
            // the zombie-vs-death decision, so a death-reactive buff (e.g. Karthus DeathDefied) can
            // arm data.BecomeZombie from its OnDeath handler. The actual removal / zombie branch runs
            // further down, after the non-persist buff cull.
            ApiEventManager.OnDeath.Publish(data.Unit, data);
            PublishNearbyDeath(data);

            // PersistsThroughDeath (buff side): the holder's death removes every buff that is NOT
            // flagged to persist. Riot's scriptBaseBuff::PersistsThroughDeath checks only the buff's
            // own flag (no spell-data fallback). Runs AFTER OnDeath so death-reactive buffs (revives
            // like Guardian Angel) fire first — those set the flag, so they also survive this pass.
            foreach (var buff in new List<Buff>(BuffList))
            {
                if (buff.BuffScript?.BuffMetaData is { PersistsThroughDeath: false })
                {
                    RemoveBuff(buff);
                }
            }

            if (data.Unit is ObjAIBase obj)
            {
                if (!(obj is Monster))
                {
                    var champs = _game.ObjectManager.GetChampionsInRangeFromTeam(Position, obj.ExperienceGiveRadius, CustomConvert.GetEnemyTeam(Team), true);
                    if (champs.Count > 0)
                    {
                        var expPerChamp = obj.Stats.ExpGivenOnDeath.Total / champs.Count;
                        foreach (var c in champs)
                        {
                            c.AddExperience(expPerChamp);
                        }
                    }
                }
            }

            // Gold / kill-credit redirect (Riot GoldRedirectTarget): a unit that cannot hold gold —
            // an autonomous pet (Malzahar Voidling, etc.) — routes its kill credit to another unit,
            // normally its summoner, so the OWNER receives the gold / XP / CS count. Without this a
            // pet's last hit credits nobody (the killer isn't a Champion, so OnKill never fires).
            // No-op until something sets GoldRedirectTarget (P-C pets / gold-share items).
            var creditedKiller = data.Killer;
            if (creditedKiller is ObjAIBase redirector && redirector.GoldRedirectTarget != null)
            {
                creditedKiller = redirector.GoldRedirectTarget;
            }

            // Deny gate (Riot AIMinionEventManager: killer.team == victim.team -> EVENT_ON_MINION_DENIED,
            // no gold/CS/XP; only an enemy killer fires EVENT_ON_MINION_KILL). Monsters are NEUTRAL, so a
            // BLUE/PURPLE killer always clears this gate and still gets jungle gold/XP.
            if (creditedKiller != null && creditedKiller is Champion champion
                && creditedKiller.Team != data.Unit.Team)
            {
                // Monster (jungle) XP is shared among the KILLER's team within the monster's
                // ExperienceRadius (Riot GiveExpToNearHeroesFromNeutral: camps 400, epics 2000; falls
                // back to ai_ExpRadius2). Neutrals have no enemy team, so the share is keyed to the
                // killer's team — a laner near a Dragon/Baron kill gets shared XP, small camps (400)
                // effectively only reach the jungler.
                if (data.Unit is Monster monster)
                {
                    var expChamps = _game.ObjectManager.GetChampionsInRangeFromTeam(
                        Position, monster.ExperienceGiveRadius, champion.Team, true);
                    if (expChamps.Count > 0)
                    {
                        var expPerChamp = data.Unit.Stats.ExpGivenOnDeath.Total / expChamps.Count;
                        foreach (var c in expChamps)
                        {
                            c.AddExperience(expPerChamp);
                        }
                    }
                    else
                    {
                        champion.AddExperience(data.Unit.Stats.ExpGivenOnDeath.Total);
                    }
                }

                champion.OnKill(data);
            }

            // Zombie-vs-death decision (Riot DoDeath), AFTER the non-persist buff cull above so the
            // keep-alive buff added by an OnZombie handler survives. A zombie stays in the world
            // (no SetToRemove) and keeps acting until a script calls EndZombie().
            if (data.BecomeZombie)
            {
                // Model B (faithful to Riot DoDeath: bZombie set, dead flag NOT): a zombie is NOT
                // counted as dead — clear IsDead so it behaves like a live unit until EndZombie().
                // Only the SERVER-side removal is deferred; the death is still announced immediately
                // below (NotifyDeath carries the BecomeZombie bit, so the client keeps the actor).
                IsZombie = true;
                IsDead = false;
                _zombieDeath = data;
                ApiEventManager.OnZombie.Publish(data.Unit, data);
            }
            else
            {
                if (!IsToRemove())
                {
                    _game.PacketNotifier.NotifyS2C_NPC_Die_MapView(data);
                }
                SetToRemove();
            }

            _game.PacketNotifier.NotifyDeath(data);
        }

        /// <summary>
        /// Ends the zombie phase started by <see cref="Die"/> and finalizes the real death. Called by
        /// the script owning the zombie's keep-alive once it expires (e.g. Karthus DeathDefiedBuff
        /// OnDeactivate). Riot exposes no observable server-side bZombie=false — the zombie ends by
        /// re-death when the keep-alive lapses; this replays the deferred removal/notify.
        /// </summary>
        public virtual void EndZombie()
        {
            if (!IsZombie)
            {
                return;
            }

            // The zombie truly dies now: clear the zombie state and set IsDead (the unit was a live
            // zombie, IsDead=false). Death was already announced at Die() (NotifyDeath, with the
            // zombie bit); this performs the deferred server-side removal + map-view cleanup.
            IsZombie = false;
            IsDead = true;
            var data = _zombieDeath ?? _death;
            _zombieDeath = null;

            if (data != null && !IsToRemove())
            {
                _game.PacketNotifier.NotifyS2C_NPC_Die_MapView(data);
            }
            SetToRemove();
        }

        /// <summary>
        /// Sets this unit's current model to the specified internally named model. *NOTE*: If the model is not present in the client files, all connected players will crash.
        /// </summary>
        /// <param name="model">Internally named model to set.</param>
        /// <returns></returns>
        /// TODO: Implement model verification (perhaps by making a list of all models in Content) so that clients don't crash if a model which doesn't exist in client files is given.
        public bool ChangeModel(string model)
        {
            // Unified through the CharacterDataStack base layer (single source of truth).
            // SetBase emits S2C_ChangeCharacterData (and syncs Model via ApplyStackModel) only when
            // the resolved model actually changes — same observable result as the old overwrite.
            return CharacterDataStack.SetBase(model);
        }

        /// <summary>
        /// Applies the model/skin resolved by the <see cref="CharacterDataStack"/> onto this unit
        /// (server-side mirror only — the authoritative wire packet was already sent by the stack).
        /// </summary>
        internal void ApplyStackModel(string model, uint skinID)
        {
            Model = model;
            OnStackSkinResolved(skinID);
        }

        /// <summary>
        /// Hook for derived units that carry a skin index (ObjAIBase.SkinID) to sync it when the
        /// CharacterDataStack resolves a new top layer. Base AttackableUnit has no skin index.
        /// </summary>
        protected virtual void OnStackSkinResolved(uint skinID) { }

        /// <summary>
        /// Applies the spellbook resolved by the <see cref="CharacterDataStack"/> (the topmost
        /// overrideSpells layer, or the base character). Server-side spell-slot swap only; the client
        /// loads the matching spellbook itself from the ChangeCharacterData useSpells flag.
        /// </summary>
        internal void ApplyStackSpellSkin(string spellSkinCharacter)
        {
            OnStackSpellSkinResolved(spellSkinCharacter);
        }

        /// <summary>
        /// Hook for spell-casting units (ObjAIBase) to swap their Q/W/E/R slots to another character's
        /// spells on transform. Base AttackableUnit has no spellbook.
        /// </summary>
        protected virtual void OnStackSpellSkinResolved(string spellSkinCharacter) { }

        /// <summary>
        /// Gets the movement speed stat of this unit (units/sec).
        /// </summary>
        /// <returns>Float units/sec.</returns>
        public float GetMoveSpeed()
        {
            if (MovementParameters != null)
            {
                return MovementParameters.PathSpeedOverride;
            }

            return Stats.GetTrueMoveSpeed();
        }

        /// <summary>
        /// Enables or disables the given status on this unit.
        /// </summary>
        /// <param name="status">StatusFlag to enable/disable.</param>
        /// <param name="enabled">Whether or not to enable the flag.</param>
        public void SetStatus(StatusFlags status, bool enabled)
        {
            // Riot couples stealth engine-side (S1 server LuaSpellScriptHelper::SetStealthed — the
            // body behind BBSetStatus(SetStealthed); the Lua never sees these side effects). On a
            // REAL transition it (a) mirrors the new value into Ghosted — PROVEN for 4.20 on the
            // wire (Shaco own-perspective replay 3f6b5739: STEALTHED bit 4 and IS_GHOSTED bit 12
            // flip together in the SAME replication update at stealth start AND end) — and (b) on
            // ENTRY only calls ClearTarget. The clear is S1-BINARY evidence only (not directly
            // testable for 4.20 — server bodies are absent from the client decomp): that it means
            // the unit's OWN attack target is corroborated by S1's death-teardown calling the same
            // ClearTarget(&this->AIManagerOwnerI) beside AI_BaseTask=None + ResetSelectedSpell.
            // Riot's ghost mirror is unconditional, so a stealth-exit stomps ghosting from other
            // sources exactly like Riot's does.
            if (status.HasFlag(StatusFlags.Stealthed) && Status.HasFlag(StatusFlags.Stealthed) != enabled)
            {
                _characterState.Set(StatusFlags.Ghosted, enabled);
                if (enabled && this is ObjAIBase ai)
                {
                    ai.SetTargetUnit(null, true);
                }
            }
            // Capability bits (CanMove/Attack/Cast/MoveEver) are ref-counted disable-holds; all other bits
            // are plain set/clear. The CharacterState backing handles the per-bit dispatch + recompute; we
            // then re-derive the replicated ActionState. SetStatus(StatusFlags.None, true) sets zero bits →
            // a pure recompute trigger (used by UpdateBuffs / SetForceMovementState).
            _characterState.Set(status, enabled);
            UpdateActionState();
        }

        void UpdateActionState()
        {
            // CallForHelpSuppressor
            Stats.SetActionState(ActionState.CAN_ATTACK, Status.HasFlag(StatusFlags.CanAttack));
            // M2 Phase 3: cast-disabling CC clears the CanCast capability (BuffType.ToCapabilityDisable),
            // so CAN_CAST mirrors the capability directly — Riot's wire representation (replay-verified).
            Stats.SetActionState(ActionState.CAN_CAST, Status.HasFlag(StatusFlags.CanCast));
            Stats.SetActionState(ActionState.CAN_MOVE, Status.HasFlag(StatusFlags.CanMove));
            Stats.SetActionState(ActionState.CAN_NOT_MOVE, IsDisplacementImmune);
            // Charm and taunt share wire bit 6 (both replay-verified: Ahri Seduce + PuncturingTaunt).
            // Drive it from the OR so a charmed-not-taunted (or vice-versa) unit keeps the bit set —
            // the two SetActionState calls both target bit 6, so each must write the combined value.
            bool controlledForcedAction = Status.HasFlag(StatusFlags.Charmed) || Status.HasFlag(StatusFlags.Taunted);
            Stats.SetActionState(ActionState.CHARMED, controlledForcedAction);
            Stats.SetActionState(ActionState.DISABLE_AMBIENT_GOLD, Status.HasFlag(StatusFlags.DisableAmbientGold));
            Stats.SetActionState(ActionState.DISABLE_AMBIENT_XP, Status.HasFlag(StatusFlags.DisableAmbientXP));

            // FEARED: DEAD in 4.20 — no buff/effect drives it (user-confirmed; 4.20 fears ship as
            // FLEE-type). Kept false. NOTE the enum's FEARED=bit7 is a misnomer: wire bit 7 is a
            // structural always-on bit, unrelated to fear.
            Stats.SetActionState(ActionState.FEARED, false);
            // IS_FLEEING (wire bit 9, REPLAY-VERIFIED via Shaco "Flee" across 4 champs): Riot DOES set
            // this on every fear, clearing CanMove alongside. We currently DON'T drive it (CC handled
            // purely as capability-disable + forced movement) — known faithfulness gap, see
            // reference_4_20_actionstate_wire_bits. The old "never set" claim measured the wrong
            // (4.17) bit 8, not the real bit 9.
            Stats.SetActionState(ActionState.IS_FLEEING, false);

            Stats.SetActionState(ActionState.FORCE_RENDER_PARTICLES, Status.HasFlag(StatusFlags.ForceRenderParticles));
            // GhostProof
            Stats.SetActionState(ActionState.IS_GHOSTED, Status.HasFlag(StatusFlags.Ghosted));
            // IgnoreCallForHelp
            // Immovable
            // Invulnerable
            // MagicImmune
            Stats.SetActionState(ActionState.IS_NEAR_SIGHTED, Status.HasFlag(StatusFlags.NearSighted));
            // Netted
            Stats.SetActionState(ActionState.NO_RENDER, Status.HasFlag(StatusFlags.NoRender));
            // PhysicalImmune
            Stats.SetActionState(ActionState.REVEAL_SPECIFIC_UNIT, Status.HasFlag(StatusFlags.RevealSpecificUnit));
            // Rooted
            // Silenced
            Stats.SetActionState(ActionState.IS_ASLEEP, Status.HasFlag(StatusFlags.Sleep));
            Stats.SetActionState(ActionState.STEALTHED, Status.HasFlag(StatusFlags.Stealthed));
            // DodgePiercing (wire bit 18): faithful to Riot's SetDodgePiercing, which set the bit and
            // replicated it. The 4.20 client never reads it (server resolves dodge into the wire
            // HitResult), so this is a cosmetic/state mirror — the functional gate lives in RollDodge.
            Stats.SetActionState(ActionState.DODGE_PIERCING, Status.HasFlag(StatusFlags.DodgePiercing));
            // SuppressCallForHelp

            bool targetable = Status.HasFlag(StatusFlags.Targetable);
            Stats.IsTargetable = targetable;
            // USEABLE units are deliberately excluded from this mirror. Bit 23 is Riot's mSelectable
            // (CharacterState.h CompressedStates — ActionState.SELECTABLE): it
            // gates MOUSE-PICKING, and the client's use-cursor branch checks IsUseable() on the
            // picked selection WITHOUT any IsTargetable test (HudCursorTargetLogic.cpp:908,
            // kCursorModeCaptureUse), unlike the attack/select branches. So click-to-use units
            // (ThreshLantern, Dominion capture points/OdinNeutralGuardian, KalistaAltar,
            // AzirTowerClicker — the only 6 IsUseable chardatas in 4.20) must keep the bit while
            // attack-untargetable, or the lantern/capture click dies. NON-useable touch pickups
            // (TT/HA health relics, IsUseable=false) correctly take the mirror: Targetable=false
            // clears their Selectable bit — they are not clickable, only walk-over. Client
            // IsUseable() itself is !dead && a REPLICATED useable value (AIAttackableUnit.cpp:1439,
            // Replicate&lt;int&gt;), consistent with ValidTargetCheck's team-blind useable escape.
            if (!CharData.IsUseable)
            {
                Stats.SetActionState(ActionState.SELECTABLE, targetable);
            }

            // Same shared bit 6 as CHARMED above — write the combined value (see controlledForcedAction).
            Stats.SetActionState(ActionState.TAUNTED, controlledForcedAction);

            // M2 Phase 2 (replay-verified): temporary "can't move/attack" CC is conveyed by CLEARING the
            // CAN_MOVE/CAN_ATTACK/CAN_CAST capability bits — Riot NEVER sets a dedicated CC bit (stun/sleep
            // = caps cleared). CAN_NOT_ATTACK has no source and is never set.
            // CAN_NOT_MOVE (bit 3) = Riot's IMMOVABLE, replay-verified DATA-DRIVEN from the chardata
            // "Imobile"/"Immobile" flag (IsDisplacementImmune): epic monsters like Baron set it
            // (rlp 312026bc: Baron 0x048C808B, bit3 set + CanMove clear). NOT driven by CanMoveEver —
            // turrets (CanMoveEver=false) do NOT set bit3 on Riot's wire (they keep CanMove); only
            // displacement-immune units do.
            Stats.SetActionState(ActionState.CAN_NOT_ATTACK, false);
        }

        // Fires OnNearbyDeath (Riot CharOnNearbyDeath) when any unit dies. The dispatcher range-gates
        // per-listener (each consumer registered its own radius — see NearbyDeathDispatcher), so this is
        // a single publish. Called from both death paths (AttackableUnit + Champion).
        protected void PublishNearbyDeath(DeathData data)
        {
            ApiEventManager.OnNearbyDeath.Publish(data);
        }

        void UpdateBuffs(float diff)
        {
            var tempBuffs = new List<Buff>(BuffList);
            foreach (Buff buff in tempBuffs)
            {
                if (buff.Elapsed())
                {
                    RemoveBuff(buff);
                }
                else
                {
                    buff.Update(diff);
                }
            }

            RecomputeBuffEffects();
        }

        /// <summary>
        /// Rebuilds the buff-effect status masks from the live buff set and re-applies them. Overlap-safe
        /// by re-aggregation: every active buff contributes its explicit SetStatusEffect masks PLUS its
        /// BuffType-derived CC state flag (<see cref="BuffTypeExtensions.ToStatusFlag"/>), so a CC state
        /// stays set while ANY buff of that type is active and clears only when the last (longest) expires
        /// — the union/longest-duration semantics Riot gets from BuffType-derived CharacterState.
        /// Called per tick from <see cref="UpdateBuffs"/> AND right after a buff activates
        /// (Buff.ActivateBuff) so newly-applied CC takes effect the same tick (no activation latency).
        /// </summary>
        // True iff an active buff imposes a MOVEMENT-disabling crowd control (stun / snare / sleep /
        // suppress — their BuffType.ToCapabilityDisable includes CanMove). Deliberately CC-only: it
        // EXCLUDES the imperative cast/channel/dash self-locks (which also clear the CanMove capability
        // but are NOT CC buffs) and charm/fear/taunt (AI-driven movement, no CanMove disable). Used by the
        // pathing chokepoint, which must gate move-broadcast under CC but keep combat re-pathing during
        // casts/windups. Epics (Baron/Dragon) reject CC, so it's false for them. M2 Phase 3 replacement for
        // the old MoveDisablingCC flag mask.
        private bool IsUnderMoveDisablingCC =>
            !IsCrowdControlImmune
            && BuffList.Exists(b => b.BuffType.ToCapabilityDisable().HasFlag(StatusFlags.CanMove));

        // True iff an active buff imposes a CAST-disabling crowd control (stun / silence / suppress /
        // charm / fear / taunt — their BuffType.ToCapabilityDisable includes CanCast). Deliberately
        // CC-only: it EXCLUDES the imperative cast/channel self-locks — Channel() clears the CanCast
        // capability as its OWN action-lock (Spell.Channel: SetStatus(CanCast,false)) but is NOT a CC
        // buff — so a channel never cancels on its own lock. CanCast-bit mirror of IsUnderMoveDisablingCC;
        // used by ChannelCancelCheck. Epics (Baron/Dragon) reject CC, so it's false for them.
        internal bool IsUnderCastDisablingCC =>
            !IsCrowdControlImmune
            && BuffList.Exists(b => b.BuffType.ToCapabilityDisable().HasFlag(StatusFlags.CanCast));

        internal void RecomputeBuffEffects()
        {
            StatusFlags before = Status;

            StatusFlags buffEnable = 0;
            StatusFlags buffDisable = 0;

            // Epic monsters (Baron/Dragon) reject the CC EFFECT while still receiving the buff OBJECT —
            // the model the wire forced us to: replay shows the generic stun/slow/silence BuffAdd2 DOES land
            // on SRU_Baron/SRU_Dragon netIDs at full duration (so Riot does NOT block at application), yet
            // they are never actually CC'd in-game. So Riot adds the buff (FX/DoT/stat-mods/duration tracking)
            // and rejects the control effect internally. We mirror that here: the buff stays in BuffList and
            // still sends NPC_BuffAdd2, but its BuffType-derived CC status flag (stun/root/charm/fear/taunt/
            // silence/suppress/sleep/disarm/nearsight) is never latched, so BaseAIScript's flag-poll never
            // raises OnFear/Charm/TauntBegin → no CC movement. SLOW is a MoveSpeed stat-mod (ToStatusFlag
            // == None), handled separately in Stats. See reference_epic_monster_cc_immunity.
            bool ccImmune = IsCrowdControlImmune;

            foreach (Buff buff in BuffList)
            {
                // Skip buffs already deactivated this tick (Remove==true) but not yet pulled from
                // BuffList by the next UpdateBuffs pass. DeactivateBuff removes the stat modifier
                // immediately, so without this guard a buff's BuffType-derived CC / capability-disable
                // flags would linger one extra tick (~33ms) past its duration while its stat effects
                // already ended — an asymmetry Riot doesn't have (it drops the CharacterState
                // contribution atomically with the buff). Guarding here ends CC on the same tick as stats.
                if (buff.Elapsed())
                {
                    continue;
                }

                buffEnable |= buff.StatusEffectsToEnable;
                if (!ccImmune)
                {
                    buffEnable |= buff.BuffType.ToStatusFlag();
                    // Faithful 4.20 (M2 Phase 2): CC clears the CAN_MOVE/CAN_ATTACK/CAN_CAST capability bits
                    // (replay-verified — Riot never sets CAN_NOT_MOVE/CAN_NOT_ATTACK). Aggregated here so
                    // overlapping CC stays union/longest-duration safe. ToStatusFlag still carries the real
                    // Riot state bits (Charmed/Feared/Taunted/Sleep/Suppressed/NearSighted) during migration.
                    buffDisable |= buff.BuffType.ToCapabilityDisable();
                }
                buffDisable |= buff.StatusEffectsToDisable;
            }

            // If the effect should be enabled, it overrides disable.
            buffDisable &= ~buffEnable;

            // Push the new buff layer into the CharacterState backing, then re-derive the replicated
            // ActionState (the old SetStatus(None, true) recompute trigger, now explicit).
            _characterState.SetBuffEffects(buffEnable, buffDisable);
            UpdateActionState();

            // Auto-stop when a movement-disabling CC becomes NEWLY active (Riot: getting stunned/rooted/
            // etc. halts you). CanMove() already gates the server-side Move(), but the CLIENT keeps
            // predicting along the last waypoints until told to stop — so clear the path + broadcast.
            // Fires once on the transition; skipped while dashing (forced movement owns the unit) so it
            // doesn't fight a dash, and skipped if already path-ended. Replaces the per-buff StopMovement.
            // M2 Phase 3: CC now clears the CanMove capability, so "newly move-disabled" = the CanMove bit
            // just went set→clear. (Also fires for cast/channel self-locks that clear CanMove, but those
            // already StopMovement, so the extra call is a harmless no-op.)
            bool newlyMoveDisabled = before.HasFlag(StatusFlags.CanMove) && !Status.HasFlag(StatusFlags.CanMove);
            if (newlyMoveDisabled && MovementParameters == null && !IsPathEnded())
            {
                StopMovement();
            }

            // A hard attack-disabling CC landing mid-windup cancels the basic attack (no damage) —
            // LoL's auto-attack windup-cancel. Fires on the CanAttack set→clear transition; the windup-state
            // check + uncancellable-swing guard live in ObjAIBase.CancelAutoAttackIfWindingUp.
            bool newlyAttackDisabled = before.HasFlag(StatusFlags.CanAttack) && !Status.HasFlag(StatusFlags.CanAttack);
            if (newlyAttackDisabled && this is ObjAIBase aiUnit)
            {
                aiUnit.CancelAutoAttackIfWindingUp();
            }
        }

        /// <summary>
        /// Teleports this unit to the given position, and optionally repaths from the new position.
        /// </summary>
        /// <param name="x">X coordinate to teleport to.</param>
        /// <param name="y">Y coordinate to teleport to.</param>
        /// <param name="repath">Whether or not to repath from the new position.</param>
        public void TeleportTo(float x, float y, bool repath = false)
        {
            TeleportTo(new Vector2(x, y), repath);
        }

        /// <summary>
        /// Routes the base GameObject.TeleportToPosition virtual into the real unit teleport. Without this
        /// override the 4-param sibling only SHADOWED the base — a GameObject-typed reference
        /// (e.g. the !tp-by-netID cheat resolving via GetObjectById) fell into the base's legacy
        /// enter-visibility resync instead of the proper TeleportID movement wire.
        /// </summary>
        public override void TeleportTo(float x, float y)
        {
            TeleportTo(x, y, repath: false);
        }

        /// <summary>
        /// Teleports this unit to the given position, and optionally repaths from the new position.
        /// The wire side is fully engine-driven (Riot BBTeleportToPosition shape): the teleport
        /// rides the tick's batched WaypointGroup as a teleport-flagged (HasTeleportID +
        /// incremented TeleportID) movement entry on CHL_LOW_PRIORITY, merging with any path set
        /// later this tick — replay-verified against Yi Alpha Strike (c7119e79, 59 casts). The
        /// former `silent` + manual NotifyTeleport script pattern is removed.
        /// </summary>
        public void TeleportTo(Vector2 position, bool repath = false)
        {
            TeleportID++;
            _movementUpdated = true;
            _teleportedDuringThisFrame = true;

            position = _game.Map.NavigationGrid.GetClosestTerrainExit(position, PathfindingRadius + 1.0f);

            if (repath)
            {
                SetPosition(position, true);
            }
            else
            {
                Position = position;
                StopMovement(networked: true);
            }
        }

        /// <summary>
        /// Consecutive-zero-movement tick counter for the active force move — Riot's
        /// NavigationPath::m_MoveBlockTimeOut (NavigationPath.cpp:150): AssembleWaypointList
        /// increments it whenever a non-overrideable (forced) path produced no movement this tick
        /// and calls Stop once it reaches 15 (Actor.cpp:2216-2223) — a wedged/zero-speed force
        /// move gives up after ~half a second instead of suppressing the unit forever. Lives on
        /// the path at Riot (fresh per force move); reset here on force-move begin.
        /// </summary>
        private int _forceMoveBlockedTicks;
        internal int ForceMoveBlockedTicks { get => _forceMoveBlockedTicks; set => _forceMoveBlockedTicks = value; }
        internal bool MovementUpdated { get => _movementUpdated; set => _movementUpdated = value; }

        // UpdateForceMovement (per-tick force-move integrator) moved to Actor (Stage 5b,
        // docs/ACTOR_CLASS_EXTRACTION_PLAN.md). Called from Update(); forwarding shim (no-op for non-AI).
        // FORCE_MOVE_BLOCK_TIMEOUT_TICKS moved with it; _forceMoveBlockedTicks stays (SetForceMovementState
        // resets it) and is reached via a bridge. SetForceMovementState stays here (virtual, override-coupled).
        private float UpdateForceMovement(float frameTime)
            => (this as ObjAIBase)?.AIManager?.Actor.UpdateForceMovement(frameTime) ?? frameTime;

        /// <summary>
        /// Moves this unit to its specified waypoints, updating its position along the way.
        /// Deliberately NO interpolation: Riot's 4.x model has none on
        /// either side — the server's Actor_Common steps paths discretely at speed × dt exactly
        /// like this method, and the client RE-SIMULATES the path locally (ClientFollowServerPath)
        /// instead of interpolating between server states (champion-pathing audit 2026-07: the
        /// client smooths nothing; corrections arrive as hard Waypoint[0] snaps folded into the
        /// periodic movement rebroadcasts). Sync is held by rebroadcast cadence, not interpolation —
        /// adding it would diverge from the wire-verified movement model.
        /// </summary>
        /// <param name="delta">The amount of milliseconds the unit is supposed to move</param>
        // Move() (per-tick movement integrator) moved to Actor (Stage 5a,
        // docs/ACTOR_CLASS_EXTRACTION_PLAN.md). Kept virtual so ObjAIBase.Move's order-gating override
        // still composes (its base.Move call lands here → Actor.Move). No-op for non-AI units (buildings
        // are gated out by CanMove() in Update()). _smoothedSeparationPush moved with it; the sync/drift
        // counters stay on the unit (shared with the replication flush) and are reached via bridges.
        public virtual bool Move(float delta) => (this as ObjAIBase)?.AIManager?.Actor.Move(delta) ?? false;

        /// <summary>
        /// Client group collision response for MOVING units (S4 Actor.cpp:309-470,
        /// HandleActorCollision hard-collision branch). The collider set is aggregated into ONE
        /// group circle (barycenter + enclosing radius) and the response direction is picked by
        /// the angle between the movement direction and the to-group normal:
        ///   * group behind (proj &lt;= 0)      -> boost FORWARD along the movement (no 5.1)
        ///   * head-on (proj &gt;= 0.707)        -> pure side-slide, tangent * pushDistance * 5.1
        ///   * glancing (0 &lt; proj &lt; 0.707) -> movement reflected across the group tangent * 5.1
        /// Returns the movement MODIFICATION (client: outMov = mov + response); the caller bounds
        /// the resulting movement via <see cref="ClampCollisionMovement"/> (the S4 length clamp)
        /// and terrain-gates it. The single coherent per-group direction (instead of N per-pair
        /// pushes that partially cancel) is the client's anti-jitter mechanism in dense crowds.
        ///
        /// Slide-sign note: the client slides TOWARD the side of the group center
        /// (signTable[sideDot &gt; 0] = +1, Actor.cpp:313/425) — ported literally even though it
        /// reads counter-intuitive; runtime fidelity over intuition.
        /// </summary>
        // Radius accessors MOVED to Actor (Stage 1, docs/ACTOR_CLASS_EXTRACTION_PLAN.md). These are
        // forwarding shims so the collision code still on this class (migrates in Stage 3) keeps its call
        // sites; the canonical logic + rationale now live on Actor.ActorRadius/GetHardRadius/GetSoftRadius.
        // Non-AI units (buildings) have no Actor → the fast-path fallback (r / 2r) reproduces the prior
        // `(this as ObjAIBase)?.UsesFastPath ?? true` fast branch exactly (behaviour-identical).
        private float ActorRadius => (this as ObjAIBase)?.AIManager.Actor.ActorRadius ?? PathfindingRadius;
        private float GetHardRadius() => (this as ObjAIBase)?.AIManager.Actor.GetHardRadius() ?? PathfindingRadius;
        private float GetSoftRadius() => (this as ObjAIBase)?.AIManager.Actor.GetSoftRadius() ?? PathfindingRadius * 2f;

        // Vision-radius scale (S4 obj_AI_Base::GetVisionScale: multiplier = pctBonus + 1, additive =
        // flatBonus; raw fields fed by vision-range buffs/items). Applied to the effective vision
        // radius via GameObject.GetEffectiveVisionRadius (= VisionRadius * mult + add). Default (1,0)
        // = no scaling, so this is inert until a buff/item calls AddVisionScale. NOTE: as of 4.20 we
        // ship no vision-scaling content, so this currently never changes behaviour — it's the
        // faithful hook so such effects work when added.
        private float _visionScalePctBonus;   // additive percent: 0.2 = +20% vision radius
        private float _visionScaleFlatBonus;   // flat world units added to vision radius
        public override float VisionScaleMultiplier => 1f + _visionScalePctBonus;
        public override float VisionScaleAdditive => _visionScaleFlatBonus;

        /// <summary>
        /// Adjusts this unit's vision-radius scale (S4 GetVisionScale source fields). <paramref
        /// name="pct"/> is additive percent (0.2 = +20% radius), <paramref name="flat"/> is in world
        /// units. Buffs/items apply on gain and pass negated values on expiry. Effective vision
        /// radius = VisionRadius * (1 + sum(pct)) + sum(flat).
        /// </summary>
        public void AddVisionScale(float pct, float flat)
        {
            _visionScalePctBonus += pct;
            _visionScaleFlatBonus += flat;
        }

        // ============================================================================================
        // COLLISION RESPONSE CLUSTER moved to Actor (Stage 3, docs/ACTOR_CLASS_EXTRACTION_PLAN.md):
        //   CollectHardColliders, SteerPathAroundColliders, ComputeGroupCollisionResponse,
        //   ShouldIgnoreCollisionDueToGhost, RunCollisionResponse, ApplyCollisionPush,
        //   HandleActorCollision, ClampCollisionMovement, ComputeAvoidanceResponse.
        // The only entry point from still-on-unit code is Move() -> RunCollisionResponse; kept here as a
        // forwarding shim (no-op for non-AI units, which never move). The moved cluster reaches unit/base
        // state (Position, the in-collision flags, temp-ghost counter, drift) through the internal bridges
        // below; those bridges collapse as _stuckGhostFrames (Stage 4) and drift + Position mirroring
        // (Stage 5) migrate onto the Actor.
        private bool RunCollisionResponse(List<GameObject> nearby, Vector2 originalPos, Vector2 originalDelta, float delta)
            => (this as ObjAIBase)?.AIManager?.Actor.RunCollisionResponse(nearby, originalPos, originalDelta, delta) ?? false;

        internal Vector2 UnreplicatedDrift { get => _unreplicatedDrift; set => _unreplicatedDrift = value; }
        internal float TraveledSinceLastSync { get => _traveledSinceLastSync; set => _traveledSinceLastSync = value; }
        internal float TimeSinceLastSync { get => _timeSinceLastSync; set => _timeSinceLastSync = value; }

        // Moved to Actor (Stage 2) — forwarding shim.
        public bool PathTrueEndIs(Vector2 location)
            => (this as ObjAIBase)?.AIManager?.Actor.PathTrueEndIs(location) ?? false;

        public bool SetPathTrueEnd(Vector2 location)
        {
            if (PathTrueEndIs(location))
            {
                return true;
            }

            PathHasTrueEnd = true;
            PathTrueEnd = location;

            if (CanChangeWaypoints())
            {
                var nav = _game.Map.NavigationGrid;
                bool useFast = (this as ObjAIBase)?.UsesFastPath ?? true;
                var path = nav.GetPath(Position, location, PathfindingRadius, useFast);
                if (path != null)
                {
                    SetWaypoints(path); // resets `PathHasTrueEnd`
                    PathHasTrueEnd = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Resets this unit's waypoints. Moved to Actor (Stage 2) — forwarding shim (no-op for non-AI
        /// units, whose path is already a permanent single stationary point).
        /// </summary>
        private void ResetWaypoints() => (this as ObjAIBase)?.AIManager?.Actor.ResetWaypoints();

        /// <summary>
        /// Returns whether this unit has reached the last waypoint in its path of waypoints. Moved to
        /// Actor (Stage 2) — forwarding shim; non-AI units have a single-point path (always ended).
        /// </summary>
        public bool IsPathEnded() => (this as ObjAIBase)?.AIManager?.Actor.IsPathEnded() ?? true;

        /// <summary>
        /// Sets this unit's movement path to the given waypoints. *NOTE*: Requires current position to be prepended.
        /// </summary>
        /// <param name="newWaypoints">New path of Vector2 coordinates that the unit will move to.</param>
        /// <param name="isForced">Bypass the <see cref="CanChangeWaypoints"/> guard (used by dashes / forced movement).</param>
        /// <param name="broadcastImmediately">
        /// When false, the new path is applied (the unit walks it server-side) but NOT broadcast
        /// this tick — the periodic streamer (champions: 96ms) carries the correction as a small
        /// Position+3 update instead of a full-route preview. Used to rate-limit move-order spam
        /// while the mouse is held: every mouse-move issues a fresh MoveTo, and broadcasting each
        /// as a full-path WaypointGroup makes the client hard-snap to Waypoint[0] 10-30×/s
        /// (visible jitter). Continuation orders fold into the stream instead. Defaults true.
        /// </param>
        public bool SetWaypoints(NavigationPath newWaypoints, bool isForced = false, bool broadcastImmediately = true,
            string pathReason = null)
        {
            // Waypoints should always have an origin at the current position.
            // Dashes are excluded as their paths should be set before being applied.
            // Setting waypoints during auto attacks is allowed (CanMove() permits a cancellable windup).
            //
            // CC chokepoint: NEVER accept/broadcast a MOVING path while the unit is under a movement-
            // disabling CC (Stun / Root-Snare / Sleep / Suppress / Net) and not forced. The server Move()
            // phase refuses to advance under those, so without this gate any move-issuing path (player
            // HandleMove, engine RefreshWaypoints, AI-script SetStateAndMove / ResumeAttackMove, BotAI, …)
            // would broadcast a path the client walks while the server holds — then snaps back ~CC-duration
            // later (the snare/root desync). Scoped to MoveDisablingCC ONLY, NOT the full CanMove(): casts,
            // attack windups and the capability flag must NOT gate pathing here — combat units re-path
            // constantly (collision separation, ranged repositioning) WHILE attacking, and CanMove()'s
            // non-CC clauses would wrongly reject those (minions clumping into a bulk / ranged minions
            // walking into melee). Fear/Charm/Taunt are not in the mask — the AI drives that movement.
            if (newWaypoints == null || newWaypoints.Count <= 1 || newWaypoints[0] != Position
                || (!isForced && !CanChangeWaypoints())
                || (!isForced && IsUnderMoveDisablingCC))
            {
                return false;
            }

            // Skip the per-tick WaypointGroup broadcast when the new path is identical to
            // the existing one. The unit's traversal state is unaffected; clients already
            // know this path. Reduces wire-format noise from periodic recomputes that
            // produce the same route. The new path is fresh (m_NextWaypoint=0); the current
            // path's cursor is CurrentWaypointKey (its progress so far), both threaded into the
            // faithful S4 IsPathTheSame so its near-unit prefix-skip works as the client's does.
            //
            // TWO-WAY CHECK (2026-07-05, tt123 "melees accelerate forward then get synced
            // back"): IsPathTheSame returns true iff the OTHER path is exhausted by the match,
            // and its phase-2 prefix-skip consumes every waypoint within a 50u box of the unit
            // — so a nearly-finished current path counts as "the same" against ANY new path.
            // One-way, that silently swapped in a fresh chase/forward path (server walks it,
            // client keeps the dry old path, no broadcast, no PATH_LOG) whenever a re-acquire
            // fired near the end of the previous path; the divergence surfaced only at the 5s
            // heartbeat as a forward yank (wire: 264u, unit 1073745601 @110.0s). Requiring the
            // REVERSE direction too means the NEW path must also be exhausted — i.e. it carries
            // nothing the client doesn't already know. Genuine recompute-duplicates still match
            // both ways and stay deduplicated; the broadcast-suppression itself is our own
            // wire-noise optimization (not Riot-evidenced), the S4 primitive is untouched.
            bool sameAsExisting = newWaypoints.IsPathTheSame(Waypoints, Position, 0, CurrentWaypointKey)
                && Waypoints.IsPathTheSame(newWaypoints, Position, CurrentWaypointKey, 0);

            // P6 instrumentation (2026-07-19, opt-in PATH_LOG): count/attribute every broadcast
            // the suppression swallows — the audit's precondition for deciding whether to drop
            // the server-side dedup entirely (candidate theory: Riot broadcasts every repath-
            // timer rebuild and that is where its ~16.5u anchor-snap p50 + high reanchor rate
            // come from; our dedup would be hiding exactly those). ev-reason prefix "dedup:".
            if (sameAsExisting && PathLogger.Enabled && this is Minion)
            {
                PathLogger.Log(_game.GameTime, NetId, "dedup:" + (pathReason ?? "?"), newWaypoints, null);
            }

            Waypoints = newWaypoints;
            CurrentWaypointKey = 1;

            PathHasTrueEnd = false;

            if (!sameAsExisting && broadcastImmediately)
            {
                _movementUpdated = true;
                FullPathBroadcastPending = true;

                // Diagnostic (opt-in PATH_LOG): record the geometry of the path that just went on the
                // wire so it can be diffed 1:1 against Riot via tools/minionroute.py. Only minions
                // (the snap complaint) and only when actually broadcasting (matches the wire).
                if (PathLogger.Enabled && this is Minion)
                {
                    var allyBodies = new List<Vector2>();
                    float chord = Vector2.Distance(newWaypoints[0], newWaypoints[newWaypoints.Count - 1]);
                    float queryR = chord * 0.5f + 250f;
                    Vector2 mid = (newWaypoints[0] + newWaypoints[newWaypoints.Count - 1]) * 0.5f;
                    foreach (var o in _game.Map.CollisionHandler.GetNearestObjects(
                                 new System.Activities.Presentation.View.Circle(mid, queryR)))
                    {
                        if (o is Minion om && om != this && om.Team == Team && !om.IsDead)
                        {
                            allyBodies.Add(om.Position);
                        }
                    }
                    PathLogger.Log(_game.GameTime, NetId, pathReason, newWaypoints, allyBodies);
                }
            }

            return true;
        }

        /// <summary>
        /// Backward-compat overload — wraps the supplied list in a fresh <see cref="NavigationPath"/>.
        /// Existing call sites that still build paths as <c>new List&lt;Vector2&gt; { ... }</c> keep
        /// compiling. Prefer the <see cref="NavigationPath"/> overload for new code.
        /// </summary>
        public bool SetWaypoints(List<Vector2> newWaypoints, bool isForced = false, bool broadcastImmediately = true,
            string pathReason = null)
        {
            if (newWaypoints == null) return false;
            return SetWaypoints(new NavigationPath(newWaypoints), isForced, broadcastImmediately, pathReason);
        }

        /// <summary>
        /// Marks this unit for re-broadcasting its movement state on the next sync. Used by the
        /// periodic full-sync heartbeat — without it, a unit that started a long path silently
        /// drifts on the client until something else triggers a packet.
        /// </summary>
        public void RequestMovementSync()
        {
            _movementUpdated = true;
        }

        /// <summary>
        /// Forces this unit to stop moving.
        /// </summary>
        public virtual void StopMovement(MoveStopReason reason = MoveStopReason.CrowdControl, bool networked = true)
        {
            if (Waypoints.Count == 1) return;
            if (MovementParameters != null)
            {
                SetForceMovementState(false, reason);
                return;
            }

            ResetWaypoints();

            // Riot OnStopMove: a stop COMMAND was just executed on a moving unit (we passed the
            // already-stopped / forced-movement early-returns above). Emit-only for now (E2,
            // docs/AI_EVENT_AUDIT.md) — no subscriber → 0 behaviour change.
            if (this is ObjAIBase aiStopUnit && aiStopUnit.AIScript is AI.Behavior.BaseAIScript stopScript)
            {
                stopScript.Emit(AI.Behavior.AIEvent.OnStopMove);
            }

            if (networked)
            {
                // Bug fix: Set only the flag; DO NOT make a direct Notify call.
                // The batching system (OnSync -> HoldMovementDataUntilWaypointGroup-
                // Notification -> ObjectManager.Update -> NotifyWaypointGroup()-Flush)
                // consolidates multiple movement updates from the same frame into ONE
                // packet per client containing all units in the movementData[] array. This
                // is the original S4 format.
                //
                // Previously: in addition to _movementUpdated=true,
                // NotifyWaypointGroup(this) was called directly. Result: per StopMovement
                // TWO packets to the client one from the direct call, one from the
                // batch flush. Race condition between the two -> inconsistent
                // waypoint state on the client -> OMW_HandlePing cannot draw a green
                // path line because the “current waypoints” snapshot
                // is not stable.
                //
                // With the fix: one packet per frame per client (S4-compliant),
                // OMW lines are drawn correctly as on the
                // wave-avoidance-progress branch.
                //
                // SetWaypoints and RequestMovementSync already use the correct
                // path (just set a flag). This was the only outlier
                // spot that bypassed the batching.
                _movementUpdated = true;
            }
        }

        /// <summary>
        /// Adds the given buff instance to this unit.
        /// </summary>
        /// <summary>
        /// Whether buff wire packets (BuffAdd2/Remove2/Replace/UpdateCount) go out for this buff.
        /// ALWAYS true, hidden buffs included. Evidence (4.17 mac decomp + replay):
        /// - Riot's wire carries hidden buffs with IsHidden=1 (replay: SionQ charge buff +
        ///   SionQSound* all arrive as BuffAdd2 hidden=1).
        /// - The client processes them fully: BuffManager_pimpl::OnNetworkPacket(PKT_NPC_BuffAdd2)
        ///   does ResizeClient + SetIsHidden(n.isHidden==1) (display-only flag) and fires the
        ///   kSoundEventTypeOnBuffActivate/OnBuffCast audio events — buff-driven spell sounds
        ///   NEED the add packet (BuffManagerClient.cpp:775+).
        /// - Unknown buff names (our server-internal markers) are handled gracefully:
        ///   buffsFindGlobalBuff fails -> Riot assert -> AssertCallback is LOG-ONLY
        ///   (GameAsserts.cpp: RiotLogMessage + ignore-set, no crash) and the packet is ignored.
        /// </summary>
        private static bool BuffIsReplicated(Buff b) => true;

        /// <param name="b">Buff instance to add.</param>
        public virtual bool AddBuff(Buff b)
        {
            if (ApiEventManager.OnAllowAddBuff.Publish(this, (b.SourceUnit, b)))
            {
                if (!ParentBuffs.TryGetValue(b.Name, out Buff parentBuff))
                {
                    if (HasBuff(b.Name))
                    {
                        var buff = GetBuffsWithName(b.Name)[0];
                        ParentBuffs.Add(b.Name, buff);
                    }
                    else
                    {
                        ParentBuffs.Add(b.Name, b);
                        BuffList.Add(b);
                        if (BuffIsReplicated(b))
                        {
                            _game.PacketNotifier.NotifyNPC_BuffAdd2(b);
                            // BuffAdd2 carries no counter field and the client's ResizeClient doesn't set
                            // mCounter, so a COUNTER-type buff would display 0 until its first increment.
                            // Send the initial counter explicitly (int32) right after the add.
                            if (b.BuffType == BuffType.COUNTER)
                            {
                                _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(b);
                            }
                        }
                        b.ActivateBuff();
                    }
                }
                else if (b.BuffAddType == BuffAddType.REPLACE_EXISTING)
                {
                    parentBuff.DeactivateBuff();
                    RemoveBuff(b.Name, false);
                    RemoveBuffSlot(b);

                    BuffSlots[parentBuff.Slot] = b;
                    b.SetSlot(parentBuff.Slot);

                    ParentBuffs.Add(b.Name, b);
                    BuffList.Add(b);

                    if (BuffIsReplicated(b))
                    {
                        _game.PacketNotifier.NotifyNPC_BuffReplace(b);
                        // BuffReplace (like BuffAdd2) doesn't carry/set the client mCounter — re-send the
                        // counter for COUNTER-type buffs so the replaced instance shows the right value.
                        if (b.BuffType == BuffType.COUNTER)
                        {
                            _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(b);
                        }
                    }
                    b.ActivateBuff();
                }
                else if (b.BuffAddType == BuffAddType.RENEW_EXISTING)
                {
                    if (b != parentBuff)
                    {
                        RemoveBuffSlot(b);
                    }
                    // Riot BuffScriptInstance::Renew (BuffScriptClient.cpp:354) re-attributes the buff's
                    // caster to the renewing source on every renewal (mCasterObjID = packet caster;
                    // RefreshSourceName if it changed). Mirror it: a renew by a DIFFERENT caster updates
                    // SourceUnit so the wire CasterNetID and script attribution follow the latest applier.
                    if (b.SourceUnit != null && b.SourceUnit != parentBuff.SourceUnit)
                    {
                        parentBuff.SourceUnit = b.SourceUnit;
                    }
                    parentBuff.Refresh();
                }
                else if (b.BuffAddType == BuffAddType.STACKS_AND_CONTINUE)
                {
                    if (parentBuff.StackCount >= parentBuff.MaxStacks)
                    {
                        RemoveBuffSlot(b);
                    }
                    else
                    {
                        var buffsWithName = GetBuffsWithName(b.Name);
                        var maxRemainingDuration = 0.0f;
                        for (var i = 0; i < buffsWithName.Count; i++)
                        {
                            var existingBuff = buffsWithName[i];
                            var remainingDuration = Math.Max(0.0f, existingBuff.Duration - existingBuff.TimeElapsed);
                            if (remainingDuration > maxRemainingDuration)
                            {
                                maxRemainingDuration = remainingDuration;
                            }
                        }

                        var durationToAdd = maxRemainingDuration + b.Duration;
                        if (durationToAdd <= Extensions.COMPARE_EPSILON)
                        {
                            durationToAdd = b.Duration;
                        }

                        // Recreate this stack with a queued duration so stack expirations continue sequentially.
                        // skipTenacity: durationToAdd derives from buffs already tenacity-reduced at their
                        // own creation; reducing again here would double-apply.
                        var continuingBuff = new Buff(_game, b.Name, durationToAdd, parentBuff.StackCount, b.OriginSpell,
                            b.TargetUnit, b.SourceUnit, b.IsBuffInfinite(), b.ParentScript, b.BuffVars?.Clone(), skipTenacity: true);

                        // Reuse the parent slot for this stack group.
                        RemoveBuffSlot(b);
                        RemoveBuffSlot(continuingBuff);
                        continuingBuff.SetSlot(parentBuff.Slot);

                        BuffList.Add(continuingBuff);

                        // Silent state mutations; one final BuffUpdateCount packet covers the whole logical operation.
                        parentBuff.IncrementStackCount(false);
                        buffsWithName.Add(continuingBuff);
                        for (var i = 0; i < buffsWithName.Count; i++)
                        {
                            buffsWithName[i].SetStacks(parentBuff.StackCount, false);
                        }

                        if (BuffIsReplicated(b))
                        {
                            if (parentBuff.BuffType == BuffType.COUNTER)
                            {
                                _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(parentBuff);
                            }
                            else
                            {
                                // REMAINING duration, not full — the active segment keeps counting down while a
                                // new stack is queued behind it. Replay-verified (FlaskOfCrystalWater, 2026-07-21):
                                // Riot's count=1→2 packet carries the active segment's remaining time (dur=13.085,
                                // rt=3.415), NOT the full duration; sending full would reset the client timer ring.
                                _game.PacketNotifier.NotifyNPC_BuffUpdateCount(parentBuff,
                                    Math.Max(0f, parentBuff.Duration - parentBuff.TimeElapsed), parentBuff.TimeElapsed);
                            }
                        }

                        // STACKS_AND_CONTINUE stacks are queued durations; only the current parent stack should stay active.
                    }
                }
                else if (b.BuffAddType == BuffAddType.STACKS_AND_OVERLAPS)
                {
                    if (parentBuff.StackCount >= parentBuff.MaxStacks)
                    {
                        var oldestbuff = parentBuff;
                        oldestbuff.DeactivateBuff();
                        RemoveBuff(b.Name, true);

                        var tempbuffs = GetBuffsWithName(b.Name);
                        BuffSlots[oldestbuff.Slot] = tempbuffs[0];
                        ParentBuffs.Add(oldestbuff.Name, tempbuffs[0]);
                        BuffList.Add(b);

                        if (BuffIsReplicated(b))
                        {
                            var currentParentBuff = ParentBuffs[b.Name];
                            if (currentParentBuff.BuffType == BuffType.COUNTER)
                            {
                                _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(currentParentBuff);
                            }
                            else
                            {
                                _game.PacketNotifier.NotifyNPC_BuffUpdateCount(b, b.Duration, b.TimeElapsed);
                            }
                        }

                        b.ActivateBuff();
                    }
                    else
                    {
                        BuffList.Add(b);

                        // Silent state mutations; one final BuffUpdateCount packet covers the whole logical operation.
                        parentBuff.IncrementStackCount(false);
                        var buffsWithName = GetBuffsWithName(b.Name);
                        for (var i = 0; i < buffsWithName.Count; i++)
                        {
                            buffsWithName[i].SetStacks(parentBuff.StackCount, false);
                        }

                        if (BuffIsReplicated(b))
                        {
                            if (b.BuffType == BuffType.COUNTER)
                            {
                                _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(parentBuff);
                            }
                            else
                            {
                                _game.PacketNotifier.NotifyNPC_BuffUpdateCount(b, b.Duration, b.TimeElapsed);
                            }
                        }

                        b.ActivateBuff();
                    }
                }
                else if (parentBuff.BuffAddType == BuffAddType.STACKS_AND_RENEWS)
                {
                    var existingBuffs = GetBuffsWithName(b.Name);
                    for (var i = 0; i < existingBuffs.Count; i++)
                    {
                        existingBuffs[i].ResetTimeElapsed();
                    }

                    // If max stacks reached, only renew existing stack timers.
                    if (parentBuff.StackCount >= parentBuff.MaxStacks)
                    {
                        RemoveBuffSlot(b);
                    }
                    else
                    {
                        // Reuse the parent slot for this stack group.
                        var parentSlot = parentBuff.Slot;
                        RemoveBuffSlot(b);
                        b.SetSlot(parentSlot);

                        BuffList.Add(b);
                        // Silent state mutations; one final BuffUpdateCount packet covers the whole logical operation.
                        parentBuff.IncrementStackCount(false);
                        existingBuffs.Add(b);
                        for (var i = 0; i < existingBuffs.Count; i++)
                        {
                            existingBuffs[i].SetStacks(parentBuff.StackCount, false);
                        }

                        b.ActivateBuff();
                    }

                    if (BuffIsReplicated(b))
                    {
                        if (parentBuff.BuffType == BuffType.COUNTER)
                        {
                            _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(parentBuff);
                        }
                        else
                        {
                            _game.PacketNotifier.NotifyNPC_BuffUpdateCount(parentBuff, parentBuff.Duration, parentBuff.TimeElapsed);
                        }
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Whether or not this unit has the given buff instance.
        /// </summary>
        /// <param name="buff">Buff instance to check.</param>
        /// <returns>True/False.</returns>
        public bool HasBuff(Buff buff)
        {
            return BuffList != null && BuffList.Contains(buff);
        }

        /// <summary>
        /// Whether or not this unit has a buff of the given name.
        /// </summary>
        /// <param name="buffName">Internal buff name to check for.</param>
        /// <returns>True/False.</returns>
        public bool HasBuff(string buffName)
        {
            return BuffList.Find(b => b.IsBuffSame(buffName)) != null;
        }

        /// <summary>
        /// Whether or not this unit has a buff of the given type.
        /// </summary>
        /// <param name="type">BuffType to check for.</param>
        /// <returns>True/False.</returns>
        public bool HasBuffType(BuffType type)
        {
            return BuffList != null && BuffList.Find(b => b.BuffType == type) != null;
        }

        /// <summary>
        /// Engine spell-shield consume for a non-ally spell execution hitting this unit.
        /// Replay-verified Riot behavior (project_spell_shield_system memory): the break is
        /// wire-INVISIBLE (no SpellShieldMarker packet exists in any replay) — the only wire trace
        /// is the shield buff's own BuffRemove2. Ally-cast executions never interact with spell
        /// shields (they always hit); enemy AND neutral (jungle-monster) executions break —
        /// policy decision 2026-07-05, covered by the same-team check below.
        /// The shield's buff script reacts via <see cref="ApiEventManager.OnSpellShieldBroken"/>
        /// (self-removal + on-block FX/mana); if it doesn't deactivate itself, we force-remove it.
        /// </summary>
        /// <param name="breaker">The spell whose execution is attempting to break the shield.</param>
        /// <returns>True if a spell shield consumed the execution (caller must skip ALL effects).</returns>
        public bool ConsumeSpellShield(Spell breaker)
        {
            if (breaker?.CastInfo?.Owner == null || breaker.CastInfo.Owner.Team == Team)
            {
                return false;
            }

            Buff shield = BuffList?.Find(b => b.BuffType == BuffType.SPELL_SHIELD && !b.Elapsed());
            if (shield == null)
            {
                return false;
            }

            ApiEventManager.OnSpellShieldBroken.Publish(shield, breaker);
            if (!shield.Elapsed())
            {
                shield.DeactivateBuff();
            }
            return true;
        }

        /// <summary>
        /// Gets a new buff slot for the given buff instance.
        /// </summary>
        /// <param name="b">Buff instance to add.</param>
        /// <returns>Byte buff slot of the given buff.</returns>
        public byte GetNewBuffSlot(Buff b)
        {
            var slot = GetBuffSlot();
            BuffSlots[slot] = b;
            return slot;
        }

        /// <summary>
        /// Gets the slot of the given buff instance, or an open slot if no buff is given.
        /// </summary>
        /// <param name="buffToLookFor">Buff to check. Leave empty to get an empty slot.</param>
        /// <returns>Slot of the given buff or an empty slot.</returns>
        private byte GetBuffSlot(Buff buffToLookFor = null)
        {
            // Slot 0 is a valid Riot-side buff slot (replay shows 1746 BuffAdd2/Remove2/UpdateCount packets at slot 0
            // for the same Katarina match). Start at 0 to match Riot's allocation convention.
            for (byte i = 0; i < BuffSlots.Length; i++) // Find the first open slot or the slot corresponding to buff
            {
                if (BuffSlots[i] == buffToLookFor)
                {
                    return i;
                }
            }

            throw new Exception("No slot found with requested value"); // If no open slot or no corresponding slot
        }

        /// <summary>
        /// Gets the list of parent buffs applied to this unit.
        /// </summary>
        /// <returns>List of parent buffs.</returns>
        public Dictionary<string, Buff> GetParentBuffs()
        {
            return ParentBuffs;
        }

        /// <summary>
        /// Gets the parent buff instance of the buffs of the given name. Parent buffs control stack count for buffs of the same name.
        /// </summary>
        /// <param name="name">Internal buff name to check.</param>
        /// <returns>Parent buff instance.</returns>
        public Buff GetBuffWithName(string name)
        {
            Buff buff;
            if (ParentBuffs.TryGetValue(name, out buff))
            {
                return buff;
            }
            return null;
        }

        /// <summary>
        /// Gets a list of all buffs applied to this unit (parent and children).
        /// </summary>
        /// <returns>List of buff instances.</returns>
        public List<Buff> GetBuffs()
        {
            return BuffList;
        }

        /// <summary>
        /// Gets the number of parent buffs applied to this unit.
        /// </summary>
        /// <returns>Number of parent buffs.</returns>
        public int GetBuffsCount()
        {
            return ParentBuffs.Count;
        }

        /// <summary>
        /// Gets a list of all buff instances of the given name (parent and children).
        /// </summary>
        /// <param name="buffName">Internal buff name to check.</param>
        /// <returns>List of buff instances.</returns>
        public List<Buff> GetBuffsWithName(string buffName)
        {
            return BuffList.FindAll(b => b.IsBuffSame(buffName));
        }

        /// <summary>
        /// Removes the given buff from this unit. Called automatically when buff timers have finished.
        /// Buffs with BuffAddType.STACKS_AND_OVERLAPS are removed incrementally, meaning one instance removed per RemoveBuff call.
        /// Other BuffAddTypes are removed entirely, regardless of stacks. DecrementStackCount can be used as an alternative.
        /// </summary>
        /// <param name="b">Buff to remove.</param>
        public void RemoveBuff(Buff b)
        {
            if (!HasBuff(b))
            {
                return;
            }

            if (!ParentBuffs.TryGetValue(b.Name, out Buff parentBuff))
            {
                if (!b.Elapsed()) b.DeactivateBuff();
                BuffList.Remove(b);
                RemoveBuffSlot(b);

                if (BuffIsReplicated(b)) _game.PacketNotifier.NotifyNPC_BuffRemove2(b);
                return;
            }

            // STACKS_AND_CONTINUE keeps queued buff instances and removes one segment at a time.
            if (b.BuffAddType == BuffAddType.STACKS_AND_CONTINUE && b.StackCount > 1)
            {
                if (b == parentBuff)
                {
                    var parentSlot = parentBuff.Slot;
                    if (!parentBuff.Elapsed())
                    {
                        parentBuff.DeactivateBuff();
                    }

                    // Silent state mutations; final BuffUpdateCount below covers the whole logical operation.
                    parentBuff.DecrementStackCount(false);
                    RemoveBuff(b.Name, false);

                    var tempBuffs = GetBuffsWithName(b.Name);
                    tempBuffs.ForEach(tempBuff => tempBuff.SetStacks(parentBuff.StackCount, false));

                    // Next oldest buff takes the parent slot.
                    BuffSlots[parentSlot] = tempBuffs[0];
                    tempBuffs[0].SetSlot(parentSlot);
                    ParentBuffs.Add(b.Name, tempBuffs[0]);

                    // Continue-style stacks apply one active segment at a time.
                    tempBuffs[0].ActivateBuff();
                }
                else
                {
                    if (!b.Elapsed())
                    {
                        b.DeactivateBuff();
                    }
                    BuffList.Remove(b);

                    parentBuff.DecrementStackCount(false);
                    GetBuffsWithName(b.Name).ForEach(tempBuff => tempBuff.SetStacks(parentBuff.StackCount, false));
                }

                if (BuffIsReplicated(b))
                {
                    if (b.BuffType == BuffType.COUNTER)
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(ParentBuffs[b.Name]);
                    }
                    else
                    {
                        // A queued segment just became the active one — the client should show ITS remaining,
                        // not the summed total our continuingBuff carries. Replay-verified (FlaskOfCrystalWater,
                        // 2026-07-21): Riot's count=2→1 boundary packet is (dur=next-segment-full, rt=0); our
                        // continuingBuff's remaining (Duration - TimeElapsed) equals that next segment's time,
                        // and rt=0 presents it as freshly started (matches Riot). Sending full/elapsed would
                        // show the summed duration and reset the ring wrong.
                        var promoted = ParentBuffs[b.Name];
                        _game.PacketNotifier.NotifyNPC_BuffUpdateCount(promoted,
                            Math.Max(0f, promoted.Duration - promoted.TimeElapsed), 0f);
                    }
                }
            }
            else if (b.BuffAddType == BuffAddType.STACKS_AND_RENEWS && b.StackCount > 1)
            {
                if (b == parentBuff)
                {
                    var parentSlot = parentBuff.Slot;
                    if (!parentBuff.Elapsed())
                    {
                        parentBuff.DeactivateBuff();
                    }

                    // Silent state mutations; final BuffUpdateCount below covers the whole logical operation.
                    parentBuff.DecrementStackCount(false);
                    RemoveBuff(b.Name, false);

                    var tempBuffs = GetBuffsWithName(b.Name);
                    tempBuffs.ForEach(tempBuff => tempBuff.SetStacks(parentBuff.StackCount, false));

                    // Next oldest buff takes the parent slot.
                    BuffSlots[parentSlot] = tempBuffs[0];
                    tempBuffs[0].SetSlot(parentSlot);
                    ParentBuffs.Add(b.Name, tempBuffs[0]);
                }
                else
                {
                    if (!b.Elapsed())
                    {
                        b.DeactivateBuff();
                    }
                    BuffList.Remove(b);

                    parentBuff.DecrementStackCount(false);
                    GetBuffsWithName(b.Name).ForEach(tempBuff => tempBuff.SetStacks(parentBuff.StackCount, false));
                }

                // Keep visual timer from the newest active instance.
                var tempBuffsAfterRemoval = GetBuffsWithName(b.Name);
                var newestBuff = tempBuffsAfterRemoval[tempBuffsAfterRemoval.Count - 1];

                if (BuffIsReplicated(b))
                {
                    if (b.BuffType == BuffType.COUNTER)
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(ParentBuffs[b.Name]);
                    }
                    else if (parentBuff.StackCount == 1)
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateCount(newestBuff, b.Duration - newestBuff.TimeElapsed,
                            newestBuff.TimeElapsed);
                    }
                    else
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateCountGroup(this, tempBuffsAfterRemoval,
                            b.Duration - newestBuff.TimeElapsed, newestBuff.TimeElapsed);
                    }
                }
            }
            // STACKS_AND_OVERLAPS maintains multiple active Buff objects in BuffList
            else if (b.BuffAddType == BuffAddType.STACKS_AND_OVERLAPS && b.StackCount > 1)
            {
                if (b == parentBuff)
                {
                    var parentSlot = parentBuff.Slot;
                    if (!parentBuff.Elapsed())
                    {
                        parentBuff.DeactivateBuff();
                    }

                    // Silent state mutations; final BuffUpdateCount below covers the whole logical operation.
                    parentBuff.DecrementStackCount(false);
                    RemoveBuff(b.Name, false);

                    var tempBuffs = GetBuffsWithName(b.Name);
                    tempBuffs.ForEach(tempBuff => tempBuff.SetStacks(parentBuff.StackCount, false));

                    // Next oldest buff takes the parent slot.
                    BuffSlots[parentSlot] = tempBuffs[0];
                    tempBuffs[0].SetSlot(parentSlot);
                    ParentBuffs.Add(b.Name, tempBuffs[0]);
                }
                else
                {
                    if (!b.Elapsed())
                    {
                        b.DeactivateBuff();
                    }
                    BuffList.Remove(b);

                    parentBuff.DecrementStackCount(false);
                    GetBuffsWithName(b.Name).ForEach(tempBuff => tempBuff.SetStacks(parentBuff.StackCount, false));
                }

                // Used in packets to maintain the visual buff icon's timer, as removing a stack from the icon can reset the timer.
                var tempBuffsAfterRemoval = GetBuffsWithName(b.Name);
                var newestBuff = tempBuffsAfterRemoval[tempBuffsAfterRemoval.Count - 1];

                if (BuffIsReplicated(b))
                {
                    if (b.BuffType == BuffType.COUNTER)
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(ParentBuffs[b.Name]);
                    }
                    else if (parentBuff.StackCount == 1)
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateCount(newestBuff, b.Duration - newestBuff.TimeElapsed,
                            newestBuff.TimeElapsed);
                    }
                    else
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateCountGroup(this, tempBuffsAfterRemoval,
                            b.Duration - newestBuff.TimeElapsed, newestBuff.TimeElapsed);
                    }
                }
            }
            else
            {
                // For STACKS_AND_RENEWS, STACKS_AND_CONTINUE, REPLACE_EXISTING, etc.
                // Or STACKS_AND_OVERLAPS when it's the last stack.
                if (!b.Elapsed())
                {
                    b.DeactivateBuff();
                }

                RemoveBuff(b.Name, true);

                if (BuffList.Contains(b))
                {
                    BuffList.Remove(b);
                    RemoveBuffSlot(b);
                }

                if (BuffIsReplicated(b))
                {
                    _game.PacketNotifier.NotifyNPC_BuffRemove2(b);
                }
            }
        }

        /// <summary>
        /// Removes the given buff instance from the buff slots of this unit.
        /// Called automatically by RemoveBuff().
        /// </summary>
        /// <param name="b">Buff instance to check for.</param>
        private void RemoveBuffSlot(Buff b)
        {
            try
            {
                var slot = GetBuffSlot(b);
                BuffSlots[slot] = null;
            }
            catch
            {

            }
        }

        /// <summary>
        /// Removes the parent buff of the given internal name from this unit.
        /// </summary>
        /// <param name="b">Internal buff name to remove.</param>
        private void RemoveBuff(string b, bool removeSlot)
        {
            if (ParentBuffs.TryGetValue(b, out Buff parentBuff))
            {
                if (removeSlot && parentBuff != null)
                {
                    RemoveBuffSlot(parentBuff);
                }
                BuffList.Remove(parentBuff);
                ParentBuffs.Remove(b);
            }
        }

        /// <summary>
        /// Removes all buffs of the given internal name from this unit regardless of stack count.
        /// Intended mainly for buffs with BuffAddType.STACKS_AND_OVERLAPS.
        /// </summary>
        /// <param name="buffName">Internal buff name to remove.</param>
        public void RemoveBuffsWithName(string buffName)
        {
            foreach (var b in BuffList.ToArray())
            {
                if (b.IsBuffSame(buffName))
                {
                    RemoveBuff(b);
                }
            }
        }
        /// <summary>
        /// Deactivates all buffs of the given type.
        /// </summary>
        /// <param name="type">The BuffType to remove.</param>
        public void RemoveBuffsByType(BuffType type)
        {
            var buffsToRemove = BuffList.FindAll(b => b.BuffType == type);
            foreach (var buff in buffsToRemove)
            {
                RemoveBuff(buff);
            }
        }

        public virtual void AddShield(Shield shield)
        {
            if (shield == null || Shields.Contains(shield))
            {
                return;
            }

            Shields.AddLast(shield);
            // StopShieldFade=false: replay-verified Riot animates the bar on a shield GAIN
            // (1437/1437 adds in the Morgana replay carry StopShieldFade=0), never snaps it.
            _game.PacketNotifier.NotifyModifyShield(this, shield.Amount, shield.Physical, shield.Magical, false);
            ApiEventManager.OnShieldAdded.Publish(this, shield);
        }

        public virtual void RemoveShield(Shield shield)
        {
            if (shield == null)
            {
                return;
            }

            if (Shields.Remove(shield))
            {
                if (shield.Amount != 0)
                {
                    // Removed while still holding charge (buff expiry / manual dispel) — shrink
                    // the bar, but this is NOT a break, so OnShieldBreak must not fire.
                    _game.PacketNotifier.NotifyModifyShield(this, -shield.Amount, shield.Physical, shield.Magical, true);
                }
                else
                {
                    // Fully drained (ConsumeShields by damage, or ReduceShield) — a genuine break.
                    ApiEventManager.OnShieldBreak.Publish(shield);
                }
            }
        }

        public virtual bool HasShield(Shield shield = null)
        {
            return shield == null ? Shields.Count > 0 : Shields.Contains(shield);
        }

        /// <summary>
        /// Consume object-based shields and reduce post-mitigation damage.
        /// </summary>
        protected bool ConsumeShields(DamageData damageData)
        {
            LinkedList<Shield> toRemove = new LinkedList<Shield>();
            // Riot consumption order: OnPreDamagePriority descending (higher shields absorb first);
            // among equal priority, shields flagged DoOnPreDamageInExpirationOrder are spent in
            // soonest-expiry-first order so a shield about to vanish is used before a longer-lived
            // one. OrderBy is stable, so shields with no expiration preference keep insertion order.
            var ordered = Shields
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.ConsumeInExpirationOrder ? s.RemainingTime : float.MaxValue);
            foreach (var shield in ordered)
            {
                var consumed = shield.Consume(damageData);
                if (consumed != 0)
                {
                    _game.PacketNotifier.NotifyModifyShield(this, -consumed, shield.Physical, shield.Magical, false);
                    ApiEventManager.OnShieldReduced.Publish(shield, consumed);
                }

                if (shield.IsConsumed())
                {
                    toRemove.AddFirst(shield);
                }

                if (damageData.PostMitigationDamage <= 0)
                {
                    break;
                }
            }

            foreach (var shield in toRemove)
            {
                RemoveShield(shield);
            }

            return damageData.PostMitigationDamage <= 0;
        }

        /// <summary>
        /// Forces this unit to perform a line-path dash which ends at the given position. This is the
        /// engine line-path force-move primitive — Riot's <c>Actor_Common::ServerForceLinePath</c> (the
        /// follow variant is <see cref="AI.ObjAIBase.ServerForceFollowUnitPath"/>). Script-facing callers
        /// use the ForceMove / ForceMoveAway verbs in ApiFunctionManager, not this directly.
        /// NOTE: in Riot the dash params live on the NavigationPath; here they're on MovementParameters.
        /// </summary>
        /// <param name="endPos">Position to end the dash at. With <paramref name="idealDistance"/> &gt; 0 this is
        /// treated as the AIM point (direction only); the actual travel length comes from idealDistance.</param>
        /// <param name="speed">Amount of units the dash should travel in a second (movespeed).</param>
        /// <param name="gravity">Optionally how much gravity the unit will experience when above the ground while dashing.</param>
        /// <param name="keepFacingLastDirection">Whether or not the AI unit should face the direction they were facing before the dash.</param>
        /// <param name="lockActions">Whether or not to prevent movement, casting, or attacking during the duration of the movement.</param>
        /// <param name="idealDistance">Riot BBMove/BBMoveAway <c>IdealDistance</c>: when &gt; 0, the dash travels exactly
        /// this many units along the direction to <paramref name="endPos"/> instead of the geometric distance to it
        /// (decouples aim direction from travel length). 0 = use the full distance to endPos.</param>
        /// <param name="moveBackBy">Riot BBMove/BBMoveToUnit <c>MoveBackBy</c>: pull the endpoint back toward the start
        /// by this many units (positive = stop short of the target, negative = overshoot past it). Applied after
        /// idealDistance and before terrain resolution.</param>
        /// <param name="innerDistance">Riot BBMoveAway <c>DistanceInner</c>: minimum displacement floor. If terrain
        /// resolution pulls the endpoint closer than this, push it back out to innerDistance along the dash
        /// direction — but only to a walkable point, never into terrain. Guarantees a minimum travel (e.g.
        /// SweepingBlow's [550, 600] band). 0 = no floor (fully clampable, e.g. Headbutt).</param>
        /// (Three pre-rewrite TODOs resolved: dash values do NOT come from spell data — Riot passes
        /// them as BBMove/BBMoveAway/BBMoveToUnit SCRIPT parameters (e.g. Pulverize's literal
        /// Speed=10/Gravity=20), which is why the params above are named after those BB fields and
        /// scripts supply them; dashing is NOT crowd control — the displacement convention gives
        /// knockup/knockback their own CC buff while dashes carry none; and no "Dash class" —
        /// the forced-movement rewrite settled the shape as ForceMovementParameters, mirroring
        /// Riot's params-on-the-NavigationPath.)

        /// <summary>
        /// How far a FIRST_WALL_HIT / GET_NEAREST_* endpoint may be snapped out of terrain before
        /// the movement is refused instead. Replay-bracketed on Vayne Q / Riven E (4.20 corpus):
        /// endpoints up to 66u from the nearest walkable cell center still moved (snapped),
        /// endpoints 74u+ away collapsed to a zero-length dash — the cap lies in (66, 74).
        /// </summary>
        // ServerForceLinePath moved to Actor (Stage 5c, docs/ACTOR_CLASS_EXTRACTION_PLAN.md — Riot
        // Actor_Common method). Public forwarding shim (no-op for non-AI units, which never dash).
        public void ServerForceLinePath(Vector2 endPos, float speed, float gravity = 0.0f, bool keepFacingLastDirection = true, bool lockActions = true, string movementName = "", AttackableUnit caster = null, bool ignoreTerrain = false, Vector2 parabolicStartPoint = default, ForceMovementType movementType = ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersType movementOrdersType = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER, float idealDistance = 0.0f, float moveBackBy = 0.0f, float innerDistance = 0.0f)
            => (this as ObjAIBase)?.AIManager?.Actor.ServerForceLinePath(endPos, speed, gravity, keepFacingLastDirection, lockActions, movementName, caster, ignoreTerrain, parabolicStartPoint, movementType, movementOrdersType, idealDistance, moveBackBy, innerDistance);

        /// <summary>
        /// Sets this unit's current dash state to the given state.
        /// </summary>
        /// <param name="state">State to set. True = dashing, false = not dashing.</param>
        /// <param name="reason">Why the forced movement ended (drives OnMoveSuccess vs OnMoveFailure).</param>
        public virtual void SetForceMovementState(bool state, MoveStopReason reason = MoveStopReason.Finished)
        {
            // Forced-movement BEGIN. MovementParameters is already set by the caller
            // (ServerForceLinePath/ServerForceFollowUnitPath) before SetForceMovementState(true).
            // The action-lock (if any) is applied through the normal ref-counted SetStatus path — the
            // SAME mechanism Riot uses: a BBMove followed by separate BBSetStatus blocks (e.g.
            // RenektonUppercut locks SetCanAttack/SetCanCast/SetCanMove; ShyvanaTransformLeap only
            // SetCanCast). The force-move itself controls position only; movement EXECUTION is already
            // suppressed intrinsically while MovementParameters != null. Ref-counting means a concurrent
            // stun/root hold on the same capability survives when this dash releases its own hold.
            if (state && MovementParameters != null)
            {
                // Fresh force move = fresh block counter (Riot: m_MoveBlockTimeOut lives on the
                // newly constructed force path).
                _forceMoveBlockedTicks = 0;
                if (MovementParameters.SetStatus != StatusFlags.None)
                {
                    SetStatus(MovementParameters.SetStatus, false);
                }

                // CLIENT-AUTONOMOUS ATTACKERS (sr130 "minion teleports instead of knockback",
                // 2026-07-19): a Minion's client swing loop has no self-cancel and ignores
                // movement while active (AIMinionClient.cpp:134-187) — it only breaks on a
                // FORCED NPC_InstantStop_Attack. Our knockback flow emitted the dash (0x64,
                // end-of-tick batch) one tick BEFORE any attack cancel reached the wire, so the
                // client unit stayed pinned in its swing loop through the whole dash and was
                // teleported by the post-dash re-anchor. Riot's displacement CC hard-cancels the
                // attack at apply time (TurnOffAutoAttack(STOPREASON_IMMEDIATELY)) — mirror that
                // HERE, synchronously at force-move begin, so the forced ISA is a direct send
                // that precedes the batched 0x64 on the wire.
                if (this is Minion aiMinion)
                {
                    aiMinion.TurnOffAutoAttack(AutoAttackStopReason.OtherImmediately);
                }

                ApiEventManager.OnMoveBegin.Publish(this, MovementParameters);
            }

            if (MovementParameters != null && state == false)
            {
                var movementParams = MovementParameters;
                MovementParameters = null;

                // End-of-force-move terrain validation (Riot AssembleWaypointList end-of-path,
                // Actor.cpp:2126-2200): forced paths skip per-step passability (they may cross
                // walls), so a dash CANCELLED mid-wall (stun/death) can strand the unit inside
                // terrain. Riot validates the final position and snaps via
                // SnapToNearestPassableCellCenter; we mirror it with the minimal center-out exit
                // (radius 1 — same choice as the OnCollision terrain handler: the full-body-
                // clearance exit caused 50-90u jumps). Done BEFORE the end events so scripts
                // (wall-stuns etc.) read the final position. Normally-ending dashes never trigger
                // this: their endpoint was terrain-resolved at setup.
                if (!_game.Map.PathingHandler.IsWalkable(Position, 0f))
                {
                    Position = _game.Map.NavigationGrid.GetClosestTerrainExit(Position, 1.0f);
                }

                if (movementParams.SetStatus != StatusFlags.None)
                {
                    SetStatus(movementParams.SetStatus, true);
                }

                ApiEventManager.OnMoveEnd.Publish(this, movementParams);

                if (reason == MoveStopReason.Finished)
                {
                    ApiEventManager.OnMoveSuccess.Publish(this, movementParams);
                }
                else if (reason != MoveStopReason.Finished)
                {
                    ApiEventManager.OnMoveFailure.Publish(this, movementParams);
                }

                ResetWaypoints();
            }
        }

        /// <summary>
        /// Sets this unit's animation states to the given set of states.
        /// Given state pairs are expected to follow a specific structure:
        /// First string is the animation to override, second string is the animation to play in place of the first.
        /// <param name="animPairs">Dictionary of animations to set.</param>
        /// <param name="asBaseLayer">Insert at the BOTTOM of the override stack instead of
        /// the top: any script/buff override (Aatrox R RUN_ULT, form swaps) keeps winning no
        /// matter when it was added. Used by the speed-state run-animation watcher, whose
        /// state can flip mid-buff (Ghost cast during an active ult must not replace the
        /// ult's run animation).</param>
        public void SetAnimStates(Dictionary<string, string> animPairs, object source = null, bool asBaseLayer = false)
        {
            if (animPairs == null || animPairs.Count == 0) return;
            if (source == null) source = this;

            var changesToSend = new Dictionary<string, string>();

            foreach (var pair in animPairs)
            {
                string key = pair.Key;
                string newValue = pair.Value;

                if (!_animOverrideStack.ContainsKey(key))
                {
                    _animOverrideStack[key] = new List<AnimOverrideInfo>();
                }

                var list = _animOverrideStack[key];

                list.RemoveAll(x => x.Source == source);
                if (!string.IsNullOrEmpty(newValue))
                {
                    var info = new AnimOverrideInfo { OverrideValue = newValue, Source = source };
                    if (asBaseLayer)
                    {
                        list.Insert(0, info);
                    }
                    else
                    {
                        list.Add(info);
                    }
                }

                string activeVal = list.Count > 0 ? list.Last().OverrideValue : "";

                string currentActive = animOverrides.ContainsKey(key) ? animOverrides[key] : "";
                if (currentActive != activeVal)
                {
                    if (string.IsNullOrEmpty(activeVal))
                    {
                        animOverrides.Remove(key);
                    }
                    else
                    {
                        animOverrides[key] = activeVal;
                    }
                    changesToSend[key] = activeVal;
                }
            }

            if (changesToSend.Count > 0)
            {
                _game.PacketNotifier.NotifyS2C_SetAnimStates(this, animOverrides);
            }
        }
        /// <summary>
        /// Removes all animation overrides applied by a specific source.
        /// </summary>
        /// <param name="source">The object that applied the overrides (e.g. MovementParameters)</param>
        public void RemoveAnimStates(object source)
        {
            if (source == null) return;

            Dictionary<string, string> changesToSend = null;

            foreach (var kvp in _animOverrideStack)
            {
                string key = kvp.Key;
                List<AnimOverrideInfo> list = kvp.Value;

                if (list.Count == 0) continue;

                int itemsRemoved = list.RemoveAll(x => x.Source == source);

                if (itemsRemoved > 0)
                {
                    string newActiveVal = list.Count > 0 ? list.Last().OverrideValue : "";
                    string currentActive = "";
                    if (animOverrides.TryGetValue(key, out string val))
                    {
                        currentActive = val;
                    }

                    if (newActiveVal != currentActive)
                    {
                        if (string.IsNullOrEmpty(newActiveVal))
                        {
                            animOverrides.Remove(key);
                        }
                        else
                        {
                            animOverrides[key] = newActiveVal;
                        }

                        if (changesToSend == null)
                        {
                            changesToSend = new Dictionary<string, string>();
                        }
                        changesToSend[key] = newActiveVal;
                    }
                }
            }
            if (changesToSend != null && changesToSend.Count > 0)
            {
                _game.PacketNotifier.NotifyS2C_SetAnimStates(this, animOverrides);
            }
        }
        /// <summary>
        /// Registers a GameScriptTimer to be updated by this unit's game loop.
        /// The timer will be automatically removed once it is finished.
        /// </summary>
        /// <param name="timer">The GameScriptTimer instance to register.</param>
        public void RegisterTimer(GameScriptTimer timer)
        {
            _scriptTimers.Add(timer);
        }
        /// <summary>
        /// Updates all registered script timers and removes any that have completed.
        /// </summary>
        private void UpdateTimers(float diff)
        {
            for (int i = _scriptTimers.Count - 1; i >= 0; i--)
            {
                var timer = _scriptTimers[i];
                timer.Update(diff);
                if (timer.IsDead())
                {
                    _scriptTimers.RemoveAt(i);
                }
            }
        }

        public ShieldValues GetCombinedShieldValues()
        {
            var combined = new ShieldValues();

            foreach (var shield in Shields)
            {
                if (shield.Magical && shield.Physical)
                {
                    combined.MagicalAndPhysical += shield.Amount;
                }
                else if (shield.Magical)
                {
                    combined.Magical += shield.Amount;
                }
                else if (shield.Physical)
                {
                    combined.Physical += shield.Amount;
                }
            }

            return combined;
        }
        protected void UpdateGrassState()
        {
            var navGrid = _game.Map.NavigationGrid;

            bool currentlyInGrass = navGrid.HasFlag(Position, NavigationGridCellFlags.HAS_GRASS);

            if (currentlyInGrass != _isInGrass)
            {
                _isInGrass = currentlyInGrass;

                if (_isInGrass)
                {
                    ApiEventManager.OnEnterGrass.Publish(this);
                }
                else
                {
                    ApiEventManager.OnLeaveGrass.Publish(this);
                }
            }
        }
    }
}
