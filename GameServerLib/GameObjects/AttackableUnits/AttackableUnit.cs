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
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
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

        //TODO: Find out where this variable came from and if it can be unhardcoded
        internal const float DETECT_RANGE = 475.0f;

        /// <summary>
        /// Variable containing all data about the this unit's current character such as base health, base mana, whether or not they are melee, base movespeed, per level stats, etc.
        /// </summary>
        public CharData CharData { get; }
        /// <summary>
        /// Whether or not this Unit is dead. Refer to TakeDamage() and Die().
        /// </summary>
        public bool IsDead { get; protected set; }
        /// <summary>
        /// Number of minions this Unit has killed. Unused besides in replication which is used for packets, refer to NotifyOnReplication in PacketNotifier.
        /// </summary>
        /// TODO: Verify if we want to move this to ObjAIBase since AttackableUnits cannot attack or kill anything.
        public int MinionCounter { get; protected set; }
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
        /// List of all buffs applied to this AI. Used for easier indexing of buffs.
        /// </summary>
        /// TODO: Verify if we can remove this in favor of BuffSlots while keeping the functions which allow for easy accessing of individual buff instances.
        private List<Buff> BuffList { get; }

        /// <summary>
        /// Waypoints that make up the path a game object is walking in.
        /// </summary>
        public NavigationPath Waypoints { get; protected set; }
        /// <summary>
        /// Index of the waypoint in the list of waypoints that the object is currently on.
        /// </summary>
        public int CurrentWaypointKey { get; protected set; }
        public Vector2 CurrentWaypoint
        {
            get { return Waypoints[CurrentWaypointKey]; }
        }
        private Vector2 OldPoint = new Vector2(0, 0);
        private Vector2 _smoothedSeparationPush = Vector2.Zero;
        private Vector2 _unreplicatedDrift = Vector2.Zero;
        // Distance walked since the last movement broadcast — drives the Riot-style keepalive
        // cadence for non-champions (replay: walking minions get a WaypointGroup every ~167u
        // ≈ 0.5s at 325 movespeed, REGARDLESS of drift — each update carries Waypoint[0] =
        // current position, so collision drift is folded into small periodic corrections
        // instead of accumulating into one visible snap).
        private float _traveledSinceLastSync;
        // True when the waypoint LIST itself changed since the last broadcast (new order/path)
        // — the next WaypointGroup then carries the full route (client path-preview needs it;
        // Riot's occasional long lists, max 20, are these). Keepalives/drift corrections with
        // an unchanged route are capped to Position + 3 lookahead for ALL units (Riot hero
        // wire: median 2, p90 3 waypoints across 25k updates).
        public bool FullPathBroadcastPending { get; private set; }

        // Temp-ghost stuck recovery (S4 mGettingOutOfCollisionGhosted, consumed in
        // Actor_Common::GetCollisionState, Actor.cpp:2325-2334): a per-tick stuck-with-repulse
        // counter; past the threshold the unit's collision state flips mIgnoreCollisions and it
        // PHASES through other actors until it gets free. Pure server-sim state — never
        // broadcast, distinct from the player-facing StatusFlags.Ghosted buff. Threshold is
        // mUseSlowerButMoreAccurateSearch-dependent: 45 fast (champions) / 15 slow.
        // Lifecycle (client Actor.cpp:1473-1477): ++ per stuck tick, reset when in collision
        // but NOT stuck; our additions: reset when stationary / dashing (client equivalent is
        // its Stop/path lifecycle); frozen while ghosted (collision processing is skipped, so
        // the ghost ends when the unit stops or arrives).
        private int _stuckGhostFrames;
        private int TempGhostThreshold => (this as ObjAIBase)?.UsesFastPath == true ? 45 : 15;
        public bool IsTemporarilyGhosted => _stuckGhostFrames > TempGhostThreshold;

        // Sticky side-pick for the collision/avoidance slide responses. The client gets
        // direction hysteresis for free: its response writes back into m_Movement (Actor.cpp
        // `done:` label) so the NEXT frame's side computation uses the already-slid heading.
        // Our Move() re-aims at CurrentWaypoint every tick, so a head-on approach onto a
        // stationary unit has dot(toCenter, side) ≈ ±noise and the slide side flips per tick
        // (in-game: zigzag in place / walk animation without progress when clicking through an
        // enemy champion). Once a side is picked it stays until the unit leaves contact.
        // 0 = no committed side.
        private float _collisionSlideSign;

        // Last GameTime (ms) a move order was broadcast for this unit. Drives the move-order
        // rate-limiter in HandleMove: held-mouse issues a MoveTo every mouse-move frame; we
        // broadcast at most once per streaming window and fold the rest into the 96ms stream.
        public float LastMoveOrderBroadcastTime { get; set; }

        private float ResolveSlideSign(float computedSign)
        {
            if (_collisionSlideSign == 0f)
            {
                _collisionSlideSign = computedSign;
            }
            return _collisionSlideSign;
        }

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

        // Stuck recovery state, mirrors client `Actor_Common::m_StuckTimer` + `m_RepathedCount`
        // (S1 actor_client.cpp:5040-5078). Detects "actor wants to move but isn't making
        // progress" (e.g., dynamic-blocker overlap on Inhibitor/Nexus respawn, force move into
        // terrain, post collision wedge into walls) and triggers escalating repath attempts.
        // Without this, a unit stuck inside a building footprint silently consumes Move Orders
        // without progress
        private float _stuckTimerMs = 0f;
        private int _stuckRepathCount = 0;
        private Vector2 _stuckLastCheckPos = Vector2.Zero;
        public bool PathHasTrueEnd { get; private set; } = false;
        public Vector2 PathTrueEnd { get; private set; }
        private bool _isInGrass = false;
        /// <summary>
        /// Status effects enabled on this unit. Refer to StatusFlags enum.
        /// </summary>
        public StatusFlags Status { get; private set; }
        // Base layer of the action-state. Non-capability flags keep plain set/clear bitfield
        // semantics; the default-ON capability flags (mirrors Riot's CharacterState::RefCountedState)
        // are ref-counted DISABLE-holds instead, so overlapping imperative SetStatus(cap, false)
        // holds compose correctly — one source releasing its hold must not re-enable the capability
        // while another source still holds it disabled (the Xerath-Q-lockout overlap class of bug).
        // Immovable is intentionally NOT ref-counted here: it is default-OFF (enable-hold polarity,
        // opposite to CanX) and has no callers; it stays a plain flag.
        private StatusFlags _nonCapabilityBase = 0;
        private int _disableCanMove;
        private int _disableCanAttack;
        private int _disableCanCast;
        private int _disableCanMoveEver;
        private const StatusFlags CapabilityMask =
            StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMoveEver;

        /// <summary>
        /// Speed scale Riot's server applies while traversing a force-move (the "reduceSpeedSlightly"
        /// factor in obj_AI_Base::MoveForwardAtMaxSpeed, AIBase.cpp:1920). Single source of truth — a
        /// dash covers its distance in distance / (speed * ForceMoveSpeedScale) seconds. Scripts that
        /// time an effect to a force-move's landing use ApiFunctionManager.GetForceMoveTravelTime.
        /// </summary>
        public const float ForceMoveSpeedScale = 0.99f;
        private StatusFlags _buffEffectsToEnable = 0;
        private StatusFlags _buffEffectsToDisable = 0;

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
        /// Information about this object's icon on the minimap.
        /// </summary>
        /// TODO: Move this to GameObject.
        public IconInfo IconInfo { get; protected set; }
        public override bool IsAffectedByFoW => true;
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

            Waypoints = NavigationPath.OfSingle(Position);
            CurrentWaypointKey = 1;
            SetStatus(
                StatusFlags.CanAttack | StatusFlags.CanCast |
                StatusFlags.CanMove | StatusFlags.CanMoveEver |
                StatusFlags.Targetable, true
            );
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
            var gobj = "[Character]" + Model;

            // TODO: Account for any other units that have skins (requires skins to be implemented for those units)
            if (this is Champion c)
            {
                var szSkin = "";
                if (c.SkinID < 10)
                {
                    szSkin = "0" + c.SkinID;
                }
                else
                {
                    szSkin = c.SkinID.ToString();
                }
                gobj += szSkin;
            }

            return HashFunctions.HashStringNorm(gobj);
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
            // TODO: Verify how dashes are affected by teleports.
            //       Typically follow dashes are unaffected, but there may be edge cases e.g. LeeSin
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

        /// <summary>
        /// Per tick stuck detection + escalating repath. Mirrors client
        /// <c>Actor_Common::m_StuckTimer</c> + <c>m_RepathedCount</c> logic at S1 actor_client.cpp:5040-5078.
        /// "Stuck" = actual per tick distance is less than <c>MinSpeedRatioBeforeStuck</c> of
        /// expected (movespeed * diff). Constants from playable_client_420 NSEAI.cfg defaults.
        ///
        /// On trigger: snap position to nearest walkable cell (handles dynamic blocker overlap),
        /// then re-issue path to the existing goal. Each repath escalates the next trigger threshold by
        /// <c>TimeBetweenRepathsInMS</c>, capped at 15 (= S1:5034). The ghost fallback layer
        /// (S1:5044 <c>++mGettingOutOfCollisionGhosted</c> → temporary <c>mIgnoreCollisions</c>
        /// after 15 45 stuck ticks) is intentionally not ported it would conflict with the
        /// player facing <c>StatusFlags.Ghosted</c>
        /// </summary>
        private void UpdateStuckRecovery(float diff)
        {
            // New per-tick-per-unit work added by the pathing port. Cheap on the early-out path,
            // but on trigger it calls TryUnstuckRepath -> full A*. Scoped separately from
            // AttackableUnit.Move so the trace attributes its cost (and its A* fan-out) directly.
            using var _scope = Profiler.Scope("AttackableUnit.StuckRecovery", "pathing");
            // Skip cases where stuck detection isn't meaningful.
            // Use !IsPathEnded() (NOT Waypoints.Count > 1): a unit that ARRIVED at its goal keeps
            // its full waypoint list (arrival only advances CurrentWaypointKey to Count, never
            // clears Waypoints — see Move() line ~1502), so Count stays >1 while IsPathEnded() is
            // true. The old Count>1 check treated an arrived-at-goal unit as still wanting to move:
            // actualDist≈0 vs expected>0 → "stuck" → TryUnstuckRepath does GetPath(pos→goal) where
            // goal==pos → degenerate null → escalates repathCount 0→15 over ~33s, eventually
            // ResetWaypoints (the "stopped then teleported far" bug, runtime-confirmed:
            // goal==pos, newPathCount=0, posChanged=False every event). Triggered by very short
            // paths (pathLen~26) where the unit reaches the goal. The degenerate in-blocker case
            // the old comment cared about (HandleMove [pos,pos] on a NOT_PASSABLE cell) is still
            // recovered via the !IsWalkable branch.
            bool wantsToMove = !IsPathEnded() || !_game.Map.PathingHandler.IsWalkable(Position, 0f);
            if (!CanMove() || MovementParameters != null || !wantsToMove)
            {
                _stuckTimerMs = 0f;
                _stuckRepathCount = 0;
                _stuckLastCheckPos = Position;
                return;
            }

            float dx = Position.X - _stuckLastCheckPos.X;
            float dy = Position.Y - _stuckLastCheckPos.Y;
            float actualDist = MathF.Sqrt(dx * dx + dy * dy);
            float expectedDist = GetMoveSpeed() * (diff / 1000f);
            _stuckLastCheckPos = Position;

            // S1 NSEAI.cfg `MinSpeedRatioBeforeStuck = 0.25` actual < 25% of expected = stuck.
            // Naturally handles slow effects since GetMoveSpeed already accounts for them.
            // Special case `expectedDist <= 0` only when the unit ALSO has no path; otherwise
            // a path ended unit on a blocked cell would never get unstuck.
            const float MIN_SPEED_RATIO = 0.25f;
            if (expectedDist <= 0.001f && Waypoints.Count <= 1)
            {
                _stuckTimerMs = 0f;
                _stuckRepathCount = 0;
                return;
            }
            if (expectedDist > 0.001f && actualDist >= expectedDist * MIN_SPEED_RATIO)
            {
                _stuckTimerMs = 0f;
                _stuckRepathCount = 0;
                return;
            }

            _stuckTimerMs += diff;

            // S1 NSEAI.cfg defaults: StuckDelayInMS=200, TimeBetweenRepathsInMS=250, max-cap=15
            // (S1:5034). Threshold escalates so a unit stuck and repath loop doesn't spam.
            const float STUCK_DELAY_MS = 200f;
            const float STUCK_REPATH_INTERVAL_MS = 250f;
            const int STUCK_MAX_REPATHS = 15;

            int countCapped = Math.Min(_stuckRepathCount, STUCK_MAX_REPATHS);
            float threshold = STUCK_DELAY_MS + countCapped * STUCK_REPATH_INTERVAL_MS;

            if (_stuckTimerMs > threshold)
            {
                bool unstuck = TryUnstuckRepath();
                _stuckTimerMs = 0f;
                if (unstuck)
                {
                    // Reset escalation and treat next stuck-event as fresh.
                    _stuckRepathCount = 0;
                }
                else
                {
                    // Repath made no change then escalate so we don't spam, and after the cap give
                    // up entirely (clear waypoints) so subsequent player orders aren't shadowed.
                    _stuckRepathCount++;
                    if (_stuckRepathCount > STUCK_MAX_REPATHS)
                    {
                        ResetWaypoints();
                        _stuckRepathCount = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Stuck recovery action meaning we snap position to nearest walkable cell (escapes dynamic blocker
        /// overlap, e.g. Inhibitor/Nexus respawn or knockback into terrain), then issue a fresh
        /// actor aware path to the existing goal. <see cref="SetPosition"/> with <c>repath: false</c>
        /// avoids recursing through the SafePath logic. If repath fails, position is at least
        /// snapped to walkable so the next tick's path-following starts from a clean state.
        /// </summary>
        private bool TryUnstuckRepath()
        {
            // Stuck-recovery action: GetClosestTerrainExit + a full actor-aware A*. Scoped so the
            // trace shows how often stuck units force an unplanned repath.
            using var _scope = Profiler.Scope("AttackableUnit.TryUnstuckRepath", "pathing");
            // Snap to nearest walkable cell. Use radius=0 (= cell walkable check, ignore
            // PathfindingRadius clearance) this is for stuck recovery we just need to escape the
            // blocking cell, even if the destination has tighter clearance than usual. This is
            // what makes the stuck fix work for cases where the unit is wedged in narrow gaps
            // (Inhibitor edges, lane wall corners) where no PathfindingRadius clear position
            // exists nearby.
            Vector2 snappedFrom = _game.Map.NavigationGrid.GetClosestTerrainExit(Position, 0f);
            bool positionChanged = snappedFrom != Position;
            if (positionChanged)
            {
                SetPosition(snappedFrom, repath: false);
            }

            // No goal to repath to (degenerate waypoints) at least the position snap counts as
            // progress if it happened.
            if (Waypoints == null || Waypoints.Count <= 1)
            {
                return positionChanged;
            }
            Vector2 goal = Waypoints[Waypoints.Count - 1];

            var newPath = _game.Map.PathingHandler.GetPath(this, goal);
            if (newPath != null && newPath.Count >= 2)
            {
                // Only count this as genuine "unstuck" progress if the recompute actually
                // rerouted (or we snapped off a blocked cell). When the unit is wedged against
                // OTHER UNITS (not terrain), the actor-aware A* still routes straight through
                // allies (BuildActorBlockedPredicate skips same-team) and lands on enemies via
                // the near-goal exemption — so GetPath returns the SAME blocked path. Reporting
                // that as success resets _stuckRepathCount every 200ms, pinning the watchdog in a
                // tight 200ms loop that re-runs a full A* forever and never lets Riot's escalating
                // backoff (NSEAI TimeBetweenRepathsInMS=250) engage. Treat a same-path recompute
                // as NO progress so the watchdog backs off; the real escape for a unit-wedge is the
                // temp-ghost counter (Move(): _stuckGhostFrames → IsTemporarilyGhosted at 45/15),
                // which phases the unit through and resets the stuck state once it moves freely.
                bool reroutedOrSnapped = positionChanged
                    || !newPath.IsPathTheSame(Waypoints, Position, 0, CurrentWaypointKey);
                SetWaypoints(newPath);
                return reroutedOrSnapped;
            }

            return positionChanged;
        }

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

            // TODO: Rework stat management.
            _statUpdateTimer += diff;
            while (_statUpdateTimer >= 500)
            {
                using var _statsScope = Profiler.Scope("AttackableUnit.StatsTick");
                // update Stats (hpregen, manaregen) every 0.5 seconds
                Stats.Update(this, _statUpdateTimer);
                _statUpdateTimer -= 500;
                API.ApiEventManager.OnUpdateStats.Publish(this, diff);
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

            if (collider is SpellMissile || collider is SpellSector || collider is ObjBuilding || (collider is Region region && region.CollisionUnit == this))
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
            ApiEventManager.OnReceiveHeal.Publish(this, data);

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
        public DamageData TakeDamage(AttackableUnit attacker, float damage, DamageType type, DamageSource source, DamageResultType damageText, IEventSource sourceScript = null)
        {
            //TODO: Make all TakeDamage functions return DamageData
            DamageData damageData = new DamageData
            {
                IsAutoAttack = source == DamageSource.DAMAGE_SOURCE_ATTACK,
                Attacker = attacker,
                Target = this,
                Damage = damage,
                PostMitigationDamage = Stats.GetPostMitigationDamage(damage, type, attacker),
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
        public DamageData TakeDamage(AttackableUnit attacker, float damage, DamageType type, DamageSource source, bool isCrit, IEventSource sourceScript = null)
        {
            return TakeDamage(attacker, damage, type, source, Bool2Crit(isCrit), sourceScript);
        }

        public void TakeDamage(DamageData damageData, bool isCrit, IEventSource sourceScript = null)
        {
            this.TakeDamage(damageData, Bool2Crit(isCrit));
        }

        /// <summary>
        /// Applies damage to this unit.
        /// </summary>
        /// <param name="attacker">Unit that is dealing the damage.</param>
        /// <param name="damage">Amount of damage to deal.</param>
        /// <param name="type">Whether the damage is physical, magical, or true.</param>
        /// <param name="source">What the damage came from: attack, spell, summoner spell, or passive.</param>
        /// <param name="damageText">Type of damage the damage text should be.</param>
        public virtual void TakeDamage(DamageData damageData, DamageResultType damageText, IEventSource sourceScript = null)
        {
            var targetIsWard = damageData.Target is Minion { IsWard: true };
            if (damageData.DamageSource == DamageSource.DAMAGE_SOURCE_ATTACK)
            {
                if (damageData.DamageResultType == DamageResultType.RESULT_MISS)
                {
                    // Missed (e.g. Blind): the attack never connected — fire only the miss signal,
                    // NOT OnBeingHit / OnHitUnit (no on-hit procs on a missed attack).
                    ApiEventManager.OnMiss.Publish(damageData.Attacker, damageData.Target);
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
                        healRatio = attackerStats.LifeSteal.Total;
                        break;
                }
            }

            if (this is Champion c && damageData.Attacker is Champion cAttacker)
            {
                c.AddAssistMarker(cAttacker, GlobalData.ChampionVariables.TimerForAssist, damageData);
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
            
            if (!IsDead && Stats.CurrentHealth <= 0)
            {
                IsDead = true;
                _death = new DeathData
                {
                    BecomeZombie = false, // TODO: Unhardcode
                    DieType = 0, // TODO: Unhardcode
                    Unit = this,
                    Killer = attacker,
                    DamageType = type,
                    DamageSource = source,
                    DeathDuration = 0 // TODO: Unhardcode
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

        /// <summary>
        /// Whether or not this unit is currently calling for help. Unimplemented.
        /// </summary>
        /// <returns>True/False.</returns>
        /// TODO: Implement this.
        public virtual bool IsInDistress()
        {
            return false; //return DistressCause;
        }

        /// <summary>
        /// Function called when this unit's health drops to 0 or less.
        /// </summary>
        /// <param name="data">Data of the death.</param>
        public virtual void Die(DeathData data)
        {
            ExitStealth();
            //_game.ObjectManager.RefreshUnitVision(this);
            _game.ObjectManager.StopTargeting(this);

            if (!IsToRemove())
            {
                _game.PacketNotifier.NotifyS2C_NPC_Die_MapView(data);
            }

            SetToRemove();

            ApiEventManager.OnDeath.Publish(data.Unit, data);

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
                    var champs = _game.ObjectManager.GetChampionsInRangeFromTeam(Position, GlobalData.ObjAIBaseVariables.ExpRadius2, CustomConvert.GetEnemyTeam(Team), true);
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

            if (creditedKiller != null && creditedKiller is Champion champion)
            {
                //Monsters give XP exclusively to the killer
                if (data.Unit is Monster)
                {
                    champion.AddExperience(data.Unit.Stats.ExpGivenOnDeath.Total);
                }

                champion.OnKill(data);
            }

            _game.PacketNotifier.NotifyDeath(data);
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
            // Capability bits are ref-counted disable-holds (default ON); all other bits are plain
            // set/clear. Dispatch per-bit because callers pass multi-bit masks mixing the two (e.g.
            // the constructor enables CanAttack|CanCast|CanMove|CanMoveEver|Targetable in one call).
            // SetStatus(StatusFlags.None, true) sets zero bits → pure recompute trigger (used by
            // UpdateBuffs / SetForceMovementState); the per-flag checks below are simply skipped.
            if ((status & StatusFlags.CanMove) != 0) _disableCanMove = RefHold(_disableCanMove, enabled);
            if ((status & StatusFlags.CanAttack) != 0) _disableCanAttack = RefHold(_disableCanAttack, enabled);
            if ((status & StatusFlags.CanCast) != 0) _disableCanCast = RefHold(_disableCanCast, enabled);
            if ((status & StatusFlags.CanMoveEver) != 0) _disableCanMoveEver = RefHold(_disableCanMoveEver, enabled);

            StatusFlags otherBits = status & ~CapabilityMask;
            if (otherBits != 0)
            {
                if (enabled)
                {
                    _nonCapabilityBase |= otherBits;
                }
                else
                {
                    _nonCapabilityBase &= ~otherBits;
                }
            }

            StatusFlags effectiveBase = ComputeEffectiveBase();
            Status = (
                effectiveBase
                & ~_buffEffectsToDisable
            )
            | _buffEffectsToEnable;

            UpdateActionState();
        }

        // Adjust a capability disable-hold counter. enable=true releases one hold (clamped at 0 —
        // this clamp is LOAD-BEARING: the constructor and PlayerManager enable capabilities before
        // any disable exists, so an over-release must stay at 0 and leave the capability enabled).
        // enable=false adds a hold. The capability is enabled iff its counter is 0.
        private static int RefHold(int count, bool enable)
        {
            if (enable)
            {
                return count > 0 ? count - 1 : 0;
            }
            return count + 1;
        }

        // The base-layer status: non-capability bits as-is, plus each default-ON capability iff it
        // currently has no active disable-hold (counter == 0). Replaces the old single
        // _statusBeforeApplyingBuffEfects bitfield, invisibly to the buff/dash layers and all readers.
        private StatusFlags ComputeEffectiveBase()
        {
            StatusFlags b = _nonCapabilityBase;
            if (_disableCanMove == 0) b |= StatusFlags.CanMove;
            if (_disableCanAttack == 0) b |= StatusFlags.CanAttack;
            if (_disableCanCast == 0) b |= StatusFlags.CanCast;
            if (_disableCanMoveEver == 0) b |= StatusFlags.CanMoveEver;
            return b;
        }

        void UpdateActionState()
        {
            // CallForHelpSuppressor
            Stats.SetActionState(ActionState.CAN_ATTACK, Status.HasFlag(StatusFlags.CanAttack));
            bool canCast = Status.HasFlag(StatusFlags.CanCast)
                && !Status.HasFlag(StatusFlags.Charmed)
                && !Status.HasFlag(StatusFlags.Feared)
                && !Status.HasFlag(StatusFlags.Silenced)
                && !Status.HasFlag(StatusFlags.Sleep)
                && !Status.HasFlag(StatusFlags.Stunned)
                && !Status.HasFlag(StatusFlags.Suppressed)
                && !Status.HasFlag(StatusFlags.Taunted);
            Stats.SetActionState(ActionState.CAN_CAST, canCast);
            Stats.SetActionState(ActionState.CAN_MOVE, Status.HasFlag(StatusFlags.CanMove));
            Stats.SetActionState(ActionState.CAN_NOT_MOVE, !Status.HasFlag(StatusFlags.CanMoveEver));
            Stats.SetActionState(ActionState.CHARMED, Status.HasFlag(StatusFlags.Charmed));
            // DisableAmbientGold

            bool feared = Status.HasFlag(StatusFlags.Feared);
            Stats.SetActionState(ActionState.FEARED, feared);
            // TODO: Verify
            Stats.SetActionState(ActionState.IS_FLEEING, feared);

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
            // SuppressCallForHelp

            bool targetable = Status.HasFlag(StatusFlags.Targetable);
            Stats.IsTargetable = targetable;
            // TODO: Refactor this.
            if (!CharData.IsUseable)
            {
                Stats.SetActionState(ActionState.TARGETABLE, targetable);
            }

            Stats.SetActionState(ActionState.TAUNTED, Status.HasFlag(StatusFlags.Taunted));

            Stats.SetActionState(
                ActionState.CAN_NOT_MOVE,
                !Status.HasFlag(StatusFlags.CanMove)
                || Status.HasFlag(StatusFlags.Charmed)
                || Status.HasFlag(StatusFlags.Feared)
                || Status.HasFlag(StatusFlags.Immovable)
                || Status.HasFlag(StatusFlags.Netted)
                || Status.HasFlag(StatusFlags.Rooted)
                || Status.HasFlag(StatusFlags.Sleep)
                || Status.HasFlag(StatusFlags.Stunned)
                || Status.HasFlag(StatusFlags.Suppressed)
                || Status.HasFlag(StatusFlags.Taunted)
            );

            Stats.SetActionState(
                ActionState.CAN_NOT_ATTACK,
                !Status.HasFlag(StatusFlags.CanAttack)
                || Status.HasFlag(StatusFlags.Charmed)
                || Status.HasFlag(StatusFlags.Disarmed)
                || Status.HasFlag(StatusFlags.Feared)
                // TODO: Verify
                || Status.HasFlag(StatusFlags.Pacified)
                || Status.HasFlag(StatusFlags.Sleep)
                || Status.HasFlag(StatusFlags.Stunned)
                || Status.HasFlag(StatusFlags.Suppressed)
            );
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
        // Movement-DISABLING crowd control (the unit cannot move at all — these are the CC flags
        // CanMove() blocks on). Fear/Charm/Taunt are intentionally excluded: the AI DRIVES movement
        // for those (flee/pull/walk-to), so they must NOT be auto-stopped.
        private const StatusFlags MoveDisablingCC =
            StatusFlags.Stunned | StatusFlags.Rooted | StatusFlags.Sleep
            | StatusFlags.Suppressed | StatusFlags.Netted;

        // Attack-DISABLING crowd control (the unit cannot auto-attack — mirrors the CC set CanAttack()
        // blocks on). A hard CC from this set landing mid-windup cancels the basic attack (no damage).
        // Silence/Snare are NOT here: a silenced/rooted unit can still auto-attack, so a swing continues.
        private const StatusFlags AttackDisablingCC =
            StatusFlags.Charmed | StatusFlags.Disarmed | StatusFlags.Feared
            | StatusFlags.Pacified | StatusFlags.Sleep | StatusFlags.Stunned | StatusFlags.Suppressed;

        internal void RecomputeBuffEffects()
        {
            StatusFlags before = Status;

            _buffEffectsToEnable = 0;
            _buffEffectsToDisable = 0;

            foreach (Buff buff in BuffList)
            {
                _buffEffectsToEnable |= buff.StatusEffectsToEnable;
                _buffEffectsToEnable |= buff.BuffType.ToStatusFlag();
                _buffEffectsToDisable |= buff.StatusEffectsToDisable;
            }

            // If the effect should be enabled, it overrides disable.
            _buffEffectsToDisable &= ~_buffEffectsToEnable;

            // Recompute Status from the new masks (StatusFlags.None sets no base bits).
            SetStatus(StatusFlags.None, true);

            // Auto-stop when a movement-disabling CC becomes NEWLY active (Riot: getting stunned/rooted/
            // etc. halts you). CanMove() already gates the server-side Move(), but the CLIENT keeps
            // predicting along the last waypoints until told to stop — so clear the path + broadcast.
            // Fires once on the transition; skipped while dashing (forced movement owns the unit) so it
            // doesn't fight a dash, and skipped if already path-ended. Replaces the per-buff StopMovement.
            StatusFlags newlyDisabling = Status & MoveDisablingCC & ~before;
            if (newlyDisabling != 0 && MovementParameters == null && !IsPathEnded())
            {
                StopMovement();
            }

            // A hard attack-disabling CC landing mid-windup cancels the basic attack (no damage) —
            // LoL's auto-attack windup-cancel. Fires on the transition; the windup-state check +
            // uncancellable-swing guard live in ObjAIBase.CancelAutoAttackIfWindingUp.
            StatusFlags newlyAttackDisabling = Status & AttackDisablingCC & ~before;
            if (newlyAttackDisabling != 0 && this is ObjAIBase aiUnit)
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
        /// <param name="silent">If true, position changes silently e.g. no `_movementUpdated`
        /// flag, no networked StopMovement. Use with `PacketNotifier.NotifyTeleport` for blink
        /// spells that need an immediate same-tick position-sync without batching.</param>
        public void TeleportTo(float x, float y, bool repath = false, bool silent = false)
        {
            TeleportTo(new Vector2(x, y), repath, silent);
        }

        /// <summary>
        /// Teleports this unit to the given position, and optionally repaths from the new position.
        /// </summary>
        public void TeleportTo(Vector2 position, bool repath = false, bool silent = false)
        {
            TeleportID++;
            if (!silent)
            {
                _movementUpdated = true;
                _teleportedDuringThisFrame = true;
            }

            position = _game.Map.NavigationGrid.GetClosestTerrainExit(position, PathfindingRadius + 1.0f);

            if (repath)
            {
                SetPosition(position, true);
            }
            else
            {
                Position = position;
                StopMovement(networked: !silent);
            }
        }

        private float UpdateForceMovement(float frameTime)
        {
            var MP = MovementParameters;
            Vector2 dir;
            float distToDest;
            float distRemaining = float.PositiveInfinity;
            float timeRemaining = float.PositiveInfinity;
            if (MP.FollowNetID > 0)
            {
                GameObject unitToFollow = _game.ObjectManager.GetObjectById(MP.FollowNetID);
                if (unitToFollow == null)
                {
                    SetForceMovementState(false, MoveStopReason.LostTarget);
                    return frameTime;
                }
                dir = unitToFollow.Position - Position;
                distToDest = Math.Max(0, dir.Length() - MP.FollowBackDistance);
                if (MP.FollowDistance > 0)
                {
                    distRemaining = MP.FollowDistance - MP.PassedDistance;
                }
                if (MP.FollowTravelTime > 0)
                {
                    // FollowTravelTime is seconds (script param); ElapsedTime accumulates ms.
                    timeRemaining = MP.FollowTravelTime * 1000f - MP.ElapsedTime;
                }
            }
            else
            {
                if (Waypoints == null || Waypoints.Count <= 1)
                {
                    SetForceMovementState(false, MoveStopReason.ForceMovement);
                    return frameTime;
                }

                dir = Waypoints[1] - Position;
                if (float.IsNaN(dir.X) || float.IsNaN(dir.Y) || float.IsInfinity(dir.X) || float.IsInfinity(dir.Y))
                {
                    SetForceMovementState(false, MoveStopReason.ForceMovement);
                    return frameTime;
                }
                distToDest = dir.Length();
            }
            distRemaining = Math.Min(distToDest, distRemaining);

            float time = Math.Min(frameTime, timeRemaining);
            // Force-moves traverse at ForceMoveSpeedScale (Riot's "reduceSpeedSlightly", AIBase.cpp:1920);
            // the parabolic arc HEIGHT is client-only, the server moves purely horizontally and ends on
            // reaching the goal, so a dash covers its distance in distance / (speed * ForceMoveSpeedScale).
            float speed;
            if (MP.FollowNetID > 0 && MP.FollowTravelTime > 0)
            {
                // Fixed-travel-time follow: re-scale speed every tick so the unit reaches the (moving)
                // target exactly when the travel time elapses, regardless of how the target moves —
                // Riot's Actor_Common::TrackTargetUnit (Actor.cpp:2256): travelVelocity = remainDist /
                // remainTime, set as the path speed override (PathSpeedOverride is ignored in this mode).
                // distToDest is world-units, timeRemaining is ms → units/ms, matching the else branch.
                speed = distToDest / Math.Max(timeRemaining, 0.0001f) * ForceMoveSpeedScale;
            }
            else
            {
                speed = MP.PathSpeedOverride * 0.001f * ForceMoveSpeedScale;
            }
            float distPerFrame = speed * time;
            float dist = Math.Min(distPerFrame, distRemaining);
            if (dir != Vector2.Zero)
            {
                Position += Vector2.Normalize(dir) * dist;
            }

            if (distRemaining <= distPerFrame)
            {
                SetForceMovementState(false);
                return (distPerFrame - distRemaining) / speed;
            }
            if (timeRemaining <= frameTime)
            {
                SetForceMovementState(false);
                return frameTime - timeRemaining;
            }
            MP.PassedDistance += dist;
            MP.ElapsedTime += time;

            return 0;
        }

        /// <summary>
        /// Moves this unit to its specified waypoints, updating its position along the way.
        /// </summary>
        /// <param name="diff">The amount of milliseconds the unit is supposed to move</param>
        /// TODO: Implement interpolation (assuming all other desync related issues are already fixed).
        public virtual bool Move(float delta)
        {
            Vector2 originalPos = Position;
            bool walked = false;

            if (CurrentWaypointKey < Waypoints.Count)
            {
                float speed = GetMoveSpeed() * 0.001f;
                var maxDist = speed * delta;

                while (true)
                {
                    var dir = CurrentWaypoint - Position;
                    var dist = dir.Length();

                    if (maxDist < dist)
                    {
                        Position += dir / dist * maxDist;
                        break;
                    }
                    else
                    {
                        Position = CurrentWaypoint;
                        maxDist -= dist;

                        CurrentWaypointKey++;
                        if (CurrentWaypointKey == Waypoints.Count || maxDist == 0)
                        {
                            break;
                        }
                    }
                }
                walked = true;

                // No terrain check on the natural waypoint walk because the path was produced by the
                // actor aware A* (PathingHandler.GetPath, A1) which only emits walkable cells, so
                // a check here would only fire on precision artifacts at blocker edges. The S1 client's terrain check at
                // actor_client.cpp:2241-2259 fires inside HandleActorCollision after the
                // collision response push i.e., the bodyblocking equivalent below but not on the
                // natural walk.
            }

            // (bodyblocking) Separation also occurs without an active waypoint walk
            // otherwise, units that reach their waypoint within an overlap get stuck.
            // Dashes completely bypass separation so as not to disrupt dash trajectories.
            bool pushed = false;
            if (MovementParameters == null)
            {
                // Broadphase query radius = 4·CollisionRadius. The QuadTree match is radius-sum
                // (dist < query.r + other.r, QuadTree.IntersectsWith), so GetNearestObjects(this)
                // — query.r = self.r — only reached self.r + other.r (~130u for a champion). That
                // is SHORTER than the largest per-pair consumer threshold: soft-avoidance needs
                // out to other.r + GetSoftRadius() = other.r + 2·self.r (~195u), and hard needs
                // selfHard + other.r + buffer. Neighbours in that band were never returned, so the
                // pre-contact veer and the group barycenter were computed from a TRUNCATED set
                // (late/incomplete avoidance, wrong group centre near the edge). Riot collects
                // collision neighbours at 2·GetRadius() (hard pass) then 4·GetRadius() (avoidance
                // pass) — Actor.cpp:1543-1606 — so 4·CollisionRadius matches its wider pass and
                // covers every per-pair threshold with margin. Each helper re-filters by its own
                // precise threshold, so the extra candidates are harmless (just a wider broadphase).
                float queryRadius = 4f * MathF.Max(0.5f, CollisionRadius);
                var nearby = _game.Map.CollisionHandler.GetNearestObjects(
                    new System.Activities.Presentation.View.Circle(Position, queryRadius));

                Vector2 originalDelta = Position - originalPos;
                bool moving = originalDelta.LengthSquared() > 0.0001f;

                if (moving && IsTemporarilyGhosted)
                {
                    // Temp-ghosted (S4 mIgnoreCollisions): the unit phases — its own collision
                    // processing is skipped entirely and it keeps walking unimpeded (others also
                    // ignore it via the neighbor filters). The counter is intentionally frozen
                    // here; it resets when the unit goes stationary/arrives (below) — mirroring
                    // the client where a ghosted actor's collision handling produces no
                    // stuck/in-collision events and the state clears with the path lifecycle.
                    _smoothedSeparationPush = Vector2.Zero;
                    _collisionSlideSign = 0f;
                }
                else if (moving)
                {
                    // MOVING units: client group collision response (S4 Actor.cpp:309-470).
                    // The response modifies THIS tick's movement directly — no exponential
                    // smoothing (the client has none; the single coherent group direction is
                    // the anti-jitter mechanism, per-pair sums were the jitter source). Bounded
                    // by the S4 movement-length clamp, then terrain-gated like before
                    // (S1 actor_client.cpp:2241-2259 drop-on-unwalkable; the client's cell-border
                    // slide handling is approximated by dropping the push).
                    _smoothedSeparationPush = Vector2.Zero;

                    // Hard collision first; the avoidance (soft-radius pre-contact veer) layer
                    // only runs when there are NO hard colliders — exact client control flow
                    // (the hard branch exits via `goto done`, S4 Actor.cpp:338-700 vs 705-870).
                    // Each layer has its own length-clamp parameter set.
                    Vector2 response = ComputeGroupCollisionResponse(nearby, originalPos, originalDelta,
                        out bool hadHardColliders);
                    float clampTrigger = 0.75f, clampLo = 0.625f, clampHi = 1.375f;
                    if (!hadHardColliders)
                    {
                        response = ComputeAvoidanceResponse(nearby, originalPos, originalDelta);
                        clampTrigger = 0.25f; clampLo = 0.875f; clampHi = 1.125f;
                    }

                    // Contact-free tick: release the committed slide side so the next contact
                    // picks fresh.
                    if (response.LengthSquared() <= 0.0001f)
                    {
                        _collisionSlideSign = 0f;
                    }

                    if (response.LengthSquared() > 0.0001f)
                    {
                        Vector2 newMovement = ClampCollisionMovement(originalDelta + response, originalDelta,
                            clampTrigger, clampLo, clampHi);
                        // Position already includes originalDelta (the walk above), so the
                        // remaining push is the clamped movement minus what was already walked.
                        Vector2 push = newMovement - originalDelta;
                        if (push.LengthSquared() > 0.0001f)
                        {
                            Vector2 candidate = Position + push;
                            bool canApply = _game.Map.NavigationGrid.IsWalkable(candidate, 0f);
                            if (canApply)
                            {
                                Position = candidate;
                                _unreplicatedDrift += push;
                                pushed = true;
                            }
                        }
                    }

                    // Stuck-with-repulse + temp-ghost counter (S4 Actor.cpp:467-491 + 1473-1477):
                    // the client's stuck branch lives INSIDE the hard-collider response — no hard
                    // colliders means no stuck evaluation (and the ghost counter stays untouched,
                    // matching the client where no isStuck event fires that tick). Counter: ++ per
                    // stuck tick (capped 2x threshold), reset when in collision but NOT stuck.
                    if (hadHardColliders)
                    {
                        Vector2 newDelta = Position - originalPos;

                        // Temp-ghost escalation tracks GENUINE stuck (post-collision movement
                        // collapsed below 25% of intent) — a separate, stricter condition than the
                        // 1.5 separation gate below. Decoupled deliberately: the separation push
                        // fires on nearly every contact, so driving the ghost counter off it would
                        // ramp _stuckGhostFrames every tick and units would phase through each other
                        // within ~0.5s. Only a true wedge (movement ≈ cancelled) should escalate.
                        const float GenuineStuckRatio = 0.25f;
                        float genuineStuckSq = originalDelta.LengthSquared()
                                             * (GenuineStuckRatio * GenuineStuckRatio);
                        if (originalDelta.LengthSquared() > 0.01f
                            && newDelta.LengthSquared() < genuineStuckSq)
                        {
                            _stuckGhostFrames = Math.Min(_stuckGhostFrames + 1, TempGhostThreshold * 2);
                        }
                        else
                        {
                            _stuckGhostFrames = 0;
                        }

                        // Per-contact separation nudge (S4 isStuckWithRepulse, gate
                        // MinionMaxCollisionAvoidanceRatio = 1.5). Terrain-gated; on rejection the
                        // escalating-repath watchdog (UpdateStuckRecovery) still snaps a genuine wedge.
                        Vector2 stuckPush = ComputeStuckExtraPush(nearby, newDelta, originalDelta, delta);
                        if (stuckPush.LengthSquared() > 0.0001f)
                        {
                            Vector2 candidate = Position + stuckPush;
                            if (_game.Map.NavigationGrid.IsWalkable(candidate, 0f))
                            {
                                Position = candidate;
                                _unreplicatedDrift += stuckPush;
                                pushed = true;
                            }
                        }
                    }
                }
                else
                {
                    // STATIONARY units: per-pair radial separation (server-authority necessity —
                    // spawn/post-dash overlap; the client's collision callback never runs for
                    // non-moving actors). Keeps the per-tick budget clamp + smoothing.
                    // A stationary unit is trivially un-stuck: clear the temp-ghost counter
                    // (covers arrival/stop — the client clears via its Stop/path lifecycle).
                    _stuckGhostFrames = 0;
                    _collisionSlideSign = 0f;

                    Vector2 rawPush = ComputeSeparationPush(nearby);

                    // Clamp the push against a per tick movement budget (walk-speed fallback so
                    // stationary units are clamped instead of teleported by the full overlap push).
                    rawPush = ClampSeparationPush(rawPush, originalDelta, delta);

                    // Frametime correct smoothing. alpha = 1 - exp(-delta/tau). Keeps the response
                    // consistent at different tick rates and shortens the tail after the overlap clears.
                    const float PushSmoothingTauMs = 60f;
                    float alpha = 1f - (float)Math.Exp(-delta / PushSmoothingTauMs);
                    _smoothedSeparationPush = _smoothedSeparationPush * (1f - alpha)
                                            + rawPush * alpha;

                    // Body blocking push, terrain gated. Mirrors S1 actor_client.cpp:2241-2259: if
                    // the collision response would land Position in a NOT_PASSABLE / dynamic blocker
                    // cell, drop the push instead of applying it. Drained smoothed state on rejection
                    // so the next tick starts with a fresh push attempt.
                    if (_smoothedSeparationPush.LengthSquared() > 0.0001f)
                    {
                        Vector2 candidate = Position + _smoothedSeparationPush;
                        bool canApply = _game.Map.NavigationGrid.IsWalkable(candidate, 0f);
                        if (canApply)
                        {
                            Position = candidate;
                            _unreplicatedDrift += _smoothedSeparationPush;
                            pushed = true;
                        }
                        else
                        {
                            _smoothedSeparationPush = Vector2.Zero;
                        }
                    }
                }

                // (Stuck detection + extra push lives inside the moving branch above — it both
                // applies the centroid escape push and drives the temp-ghost counter.)

                // Force a movement data resync once the unreplicated drift gets large enough
                // that the client would otherwise see a visible snap on the next SetWaypoints.
                // Skip stopped units (Waypoints.Count == 1) -> GetCenteredWaypoints can't build a
                // valid packet for them.
                //
                // Threshold sized to match Riot's observed cadence: replay shows walking minions
                // resync every ~167u (≈ 0.5s at 325u/s movespeed). At the previous 25u threshold
                // we were emitting ~6× more keepalive WaypointGroups than Riot for steady-state
                // walking so a bandwidth waste with no visible benefit (client interpolates fine over
                // the longer interval).
                //
                // CHAMPIONS use a much tighter threshold (2026-06-07, replay-measured): the
                // client HARD-SETS m_Position to Waypoint[0] on every WaypointGroup receive
                // (ClientFollowServerPath, ActorClient.cpp:169 `m_Position = m_Path.GetStartPoint()`)
                // — the snap IS the sync mechanism. Riot therefore corrects walking heroes with
                // FREQUENT TINY updates: replay 343e3502, 471 mid-walk same-destination 0x61 for
                // the hero, median gap 96ms, wp0 correction median 19u / p90 86u — invisible.
                // Our old 175u threshold (sized off the MINION cadence) accumulated half a
                // second of divergence and released it as one visible forward teleport.
                //
                // WHY WE NOW GO BELOW RIOT'S 19u MEDIAN (2026-06-17): Riot's server and the
                // 4.x client ran the SAME collision code, so their positions agreed and the 19u
                // correction was just float/cadence noise. WE reimplement collision server-side,
                // SEPARATELY from the client's local sim, so the two genuinely diverge — and the
                // client's local push is pushDistance-based, NOT dt-scaled, so the divergence
                // grows with frame rate (uncapped FPS = larger per-second drift). At a 20u cap
                // that divergence is released as one ~20u snap, which IS visible on the player's
                // own champion in crowds/teamfights (lateral separation pushes snap sideways).
                // The cap = the max single-snap magnitude, so we set it well under the
                // perceptual floor for a champion-sized model. This is purely drift-gated, so a
                // clean straight walk (drift ≈ 0) still emits NOTHING — the extra packets only
                // appear while there is real divergence to correct, and each correction is
                // smaller. Tune CHAMPION_DRIFT_RESYNC up if teamfight bandwidth becomes an issue,
                // down if snaps are still visible. Minions/others stay at 175u (replay-verified
                // minion cadence; their snaps aren't player-focused).
                const float CHAMPION_DRIFT_RESYNC = 8f;
                float driftResyncThreshold = this is Champion ? CHAMPION_DRIFT_RESYNC : 175f;
                if (Waypoints.Count > 1
                    && _unreplicatedDrift.LengthSquared() > driftResyncThreshold * driftResyncThreshold)
                {
                    _movementUpdated = true;
                }

                // Travel-cadence keepalive = a periodic Waypoint[0] re-anchor while the unit
                // walks. Each WaypointGroup the client receives hard-sets m_Position to wp0
                // (ActorClient.cpp:169), so re-anchoring frequently to the unit's TRUE position
                // keeps the client from interpolating a stale path for long — accumulated
                // FP/speed/collision divergence is then corrected in many tiny (invisible)
                // steps instead of released as one visible snap on the next path change. The
                // resend carries the trimmed route (champions get their full remaining route via
                // GetCenteredWaypoints, re-seeded at the current Position), so it is purely a
                // re-anchor, not a path change.
                //
                // CHAMPION cadence reinstated 2026-06-17 (fresh replay measurement —
                // tools/wpan.py over 343e3502 + a6db3774, champion = 0x46 sender — SUPERSEDES the
                // earlier "Riot goes SILENT for seconds on a fixed path" claim, which was WRONG):
                // Riot resends a MOVING champion's 0x61 CONTINUOUSLY — gap histogram mode at the
                // 150-200ms bucket (2046 / 2640 hits), of which 1659 / 1771 are SAME-GOAL resends
                // (goal within 40u of the prior = a genuine periodic streamer, not new orders);
                // true silence (gap > 1000ms) is rare (167 / 352 vs thousands). 57u ≈ 167ms at a
                // 340u/s champion movespeed, putting us at Riot's measured mode. Distance-gated,
                // so a stopped champion (Waypoints.Count == 1) emits nothing and a slowed one
                // resends less often — matching Riot's idle behaviour.
                // CAUTION: the OLD streamer was per-tick (~96ms) AND re-sent the full multi-
                // waypoint route, which caused arc jitter (network-latency snap-back: a resend
                // whose wp0 lags the client's in-flight interpolated position pulls it back
                // ~latency*speed). 57u (~167ms) is far less frequent; IN-GAME VERIFY that arc
                // jitter has not returned, and raise CHAMPION_KEEPALIVE if it has.
                //
                // Non-champions stay at 100u (~3 updates/s at minion speed): denser than Riot's
                // ~167u minion cadence because our server collision sim diverges from the old
                // client's local sim — smaller intervals = smaller hard-snap corrections.
                // Affordable since GetCenteredWaypoints caps non-champion lists at 4 entries.
                const float CHAMPION_KEEPALIVE = 57f;
                _traveledSinceLastSync += originalDelta.Length();
                float keepaliveDist = this is Champion ? CHAMPION_KEEPALIVE : 100f;
                if (Waypoints.Count > 1 && _traveledSinceLastSync >= keepaliveDist)
                {
                    _movementUpdated = true;
                }
            }
            else
            {
                // Dash / forced movement: no body collision, and forced movement clears any
                // accumulated stuck state.
                _smoothedSeparationPush = Vector2.Zero;
                _stuckGhostFrames = 0;
                _collisionSlideSign = 0f;
            }

            return walked || pushed;
        }

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
        // S4 Actor_Common::GetHardRadius/GetSoftRadius (Actor.cpp:2384-2398). The collision
        // response is ASYMMETRIC: the SELF term uses these type-scaled radii, the NEIGHBOR term
        // always uses full GetRadius (= CollisionRadius). The gating flag mUseSlowerButMoreAccurate-
        // Search == !UsesFastPath (champions = fast A*, minions = slow-accurate — same flag drives
        // the NavGrid travelFactor branch). Champions (fast): hard = r, soft = 2r — IDENTICAL to
        // the old full-radius behaviour, so this changes nothing for them. Minions (slow): hard =
        // 0.2r, soft = 0.3r → waves pack to Riot's tighter threshold (~69.6u vs our old ~108u for
        // two r=48 minions: neighbor-full 48 + self-hard 9.6 + buffer 12). Used for SELF only.
        private float GetHardRadius() => ((this as ObjAIBase)?.UsesFastPath ?? false) ? CollisionRadius : CollisionRadius * 0.2f;
        private float GetSoftRadius() => ((this as ObjAIBase)?.UsesFastPath ?? false) ? CollisionRadius * 2f : CollisionRadius * 0.3f;

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

        private Vector2 ComputeGroupCollisionResponse(List<GameObject> nearby, Vector2 originalPos, Vector2 movementDelta, out bool hadHardColliders)
        {
            const float ReflectionIndex = 5.1f;   // S4 Actor.cpp:314
            const float AngleThreshold = 0.707f;  // S4 Actor.cpp:315
            const float Epsilon = 1e-6f;          // Riot::Vector3f::kfThreshold class
            const float MinColliderDistSq = 100f; // S4 Actor.cpp:276 — deep overlaps (<10u) are
                                                  // NOT colliders; the stuck layer handles them.

            hadHardColliders = false;
            if (nearby.Count == 0) return Vector2.Zero;

            float movementMag = movementDelta.Length();
            if (movementMag <= 0.001f) return Vector2.Zero;
            Vector2 objFwd = movementDelta / movementMag;

            bool skipAllyLaneMinions = this is LaneMinion firstWaveSelf
                                       && firstWaveSelf.IsFirstWave
                                       && !firstWaveSelf.HasPassedFirstTurret;

            // Collider collection — same classification as the client callback (Actor.cpp:240-285):
            // dead/ghosted filter, direction gate, min-distance buffer, 10u deep-overlap floor.
            float selfHard = GetHardRadius();
            var colliders = new List<AttackableUnit>(4);
            Vector2 barycenter = Vector2.Zero;
            foreach (var other in nearby)
            {
                if (other == this || other.IsToRemove()) continue;
                if (!(other is AttackableUnit otherUnit)) continue;
                if (otherUnit.IsDead || otherUnit.Status.HasFlag(StatusFlags.Ghosted) || otherUnit.IsTemporarilyGhosted) continue;
                if (skipAllyLaneMinions && otherUnit is LaneMinion && otherUnit.Team == Team) continue;
                if (Vector2.Dot(objFwd, other.Position - Position) <= 0f) continue;

                float distSq = Vector2.DistanceSquared(Position, other.Position);
                if (distSq < MinColliderDistSq) continue;

                float pairBuffer = Math.Clamp(
                    Math.Min(selfHard, other.CollisionRadius) * 0.25f, 12f, 20f);
                float pairRadius = selfHard + other.CollisionRadius + pairBuffer;
                if (distSq >= pairRadius * pairRadius) continue;

                colliders.Add(otherUnit);
                barycenter += other.Position;
            }
            if (colliders.Count == 0) return Vector2.Zero;
            hadHardColliders = true;
            barycenter /= colliders.Count;

            // Enclosing group radius: max over colliders of (dist to barycenter + their radius)
            // (S4 Actor.cpp:356-370).
            float groupRadius = 0f;
            foreach (var c in colliders)
            {
                float r = Vector2.Distance(c.Position, barycenter) + c.CollisionRadius;
                if (r > groupRadius) groupRadius = r;
            }

            float minDistanceBuffer = Math.Clamp(
                Math.Min(groupRadius, selfHard) * 0.25f, 12f, 20f);
            float totalThreshold = groupRadius + selfHard + minDistanceBuffer;

            // collisionNormal from the POST-move position (client: info.NextPosition);
            // toCenter from the PRE-move position (client: m_Position).
            Vector2 rel = barycenter - Position;
            float relLenSq = rel.LengthSquared();
            Vector2 collisionNormal = relLenSq <= Epsilon ? objFwd : rel / MathF.Sqrt(relLenSq);
            float pushDistance = Math.Max(totalThreshold - MathF.Sqrt(relLenSq), 0f);
            if (pushDistance <= 0f) return Vector2.Zero;

            Vector2 toCenter = barycenter - originalPos;
            Vector2 side = new Vector2(objFwd.Y, -objFwd.X); // client's (fwd.z, -fwd.x) perpendicular

            float proj = Vector2.Dot(objFwd, collisionNormal);
            float projAbs = Math.Abs(proj);

            if (projAbs <= Epsilon)
            {
                // Perpendicular degenerate cases (S4 Actor.cpp:407-432).
                float sideDotAbs = Math.Abs(Vector2.Dot(side, collisionNormal));
                if (sideDotAbs <= Epsilon)
                {
                    return collisionNormal * (-ReflectionIndex * pushDistance);
                }
                float slide = pushDistance / sideDotAbs;
                float slideFactor = slide <= totalThreshold ? Math.Max(0.01f, slide) : totalThreshold;
                float slideSign = ResolveSlideSign(Vector2.Dot(toCenter, side) > 0f ? 1f : -1f);
                return side * (slideFactor * ReflectionIndex * slideSign);
            }

            float factor = pushDistance / projAbs;
            float clampedFactor = factor <= totalThreshold ? Math.Max(0.01f, factor) : totalThreshold;

            if (proj <= 0f)
            {
                // Group behind the movement: accelerate forward, away from it (no 5.1).
                return objFwd * clampedFactor;
            }
            if (proj >= AngleThreshold)
            {
                // Head-on: pure side-slide with RAW pushDistance (S4 Actor.cpp:440-447).
                float sign = ResolveSlideSign(Vector2.Dot(toCenter, side) > 0f ? 1f : -1f);
                return side * (pushDistance * ReflectionIndex * sign);
            }
            // Glancing: reflect the movement across the group tangent (S4 Actor.cpp:448-462):
            // 2*(fwd - n*proj) - fwd, scaled by 5.1 * clampedFactor.
            Vector2 tangential = objFwd - collisionNormal * proj;
            Vector2 reflected = tangential * 2f - objFwd;
            return reflected * (ReflectionIndex * clampedFactor);
        }

        /// <summary>
        /// S4 movement-length clamp after a collision/avoidance response: when the modified
        /// movement deviates by more than <paramref name="trigger"/> (in squared length) from
        /// the original, rescale it into [lo, hi] x |original|². Bounds the responses to a
        /// modest per-tick magnitude while keeping their DIRECTION — the response is a steering
        /// change, not a teleport. For a zero original movement this zeroes the response (the
        /// client's responses only exist for moving actors). Two parameter sets in the client:
        /// hard collision (Actor.cpp:498-510) trigger 0.75, [0.625, 1.375]; avoidance
        /// (Actor.cpp:850-859) trigger 0.25, [0.875, 1.125].
        /// </summary>
        private static Vector2 ClampCollisionMovement(Vector2 newMovement, Vector2 originalMovement,
            float trigger, float lo, float hi)
        {
            float origLenSq = originalMovement.LengthSquared();
            float newLenSq = newMovement.LengthSquared();
            if (Math.Abs(newLenSq - origLenSq) > trigger * origLenSq && newLenSq > 1e-6f)
            {
                float targetLenSq = newLenSq > hi * origLenSq
                    ? hi * origLenSq
                    : Math.Max(lo * origLenSq, newLenSq);
                return newMovement * MathF.Sqrt(targetLenSq / newLenSq);
            }
            return newMovement;
        }

        /// <summary>
        /// Client soft-radius avoidance for MOVING units (S4 Actor.cpp:705-870) — the
        /// pre-contact layer. Runs ONLY when there are no hard colliders. Actors inside the
        /// soft band (otherR + 2*selfR; GetSoftRadius fast-mode — slow-mode actors unverified,
        /// see audit memory) with relative movement form a group (barycenter + enclosing radius
        /// + average velocity), and the unit veers SIDEWAYS before contact:
        ///   * group behind the movement -> nothing
        ///   * same-direction traffic (parallelness &gt; 0.707) and group is as fast -> nothing
        ///     (follow, don't overtake); if we're faster -> overtake side by the group heading
        ///   * head-on / crossing -> side picked by which side the group center is on
        /// Magnitude = min(0.4 x |movement|, pushDistance / |dot(normal, side)|) — a gentle veer,
        /// further bounded by the tight avoidance length clamp (0.25 / [0.875, 1.125]).
        /// </summary>
        private Vector2 ComputeAvoidanceResponse(List<GameObject> nearby, Vector2 originalPos, Vector2 movementDelta)
        {
            const float AngleThreshold = 0.707f;  // S4 Actor.cpp:315
            const float Epsilon = 1e-6f;

            if (nearby.Count == 0) return Vector2.Zero;

            float movementMag = movementDelta.Length();
            if (movementMag <= 0.001f) return Vector2.Zero;
            Vector2 objFwd = movementDelta / movementMag;

            bool skipAllyLaneMinions = this is LaneMinion firstWaveSelf
                                       && firstWaveSelf.IsFirstWave
                                       && !firstWaveSelf.HasPassedFirstTurret;

            // Our per-second velocity for the lockstep gate / speed comparison. The client uses
            // m_Movement on both sides; we derive the neighbor's from its waypoint state.
            Vector2 myVelocity = objFwd * GetMoveSpeed();

            // Soft-band collection (S4 Actor.cpp:274-285): inside (.., otherR + softRadius),
            // moving relative to us (lockstep formations don't trigger), in front of the
            // movement (the direction gate precedes both classifications).
            var members = new List<AttackableUnit>(4);
            Vector2 barycenter = Vector2.Zero;
            Vector2 groupVelocity = Vector2.Zero;
            float softRadius = GetSoftRadius(); // SELF soft term (S4 Actor.cpp:766): champion 2r, minion 0.3r
            foreach (var other in nearby)
            {
                if (other == this || other.IsToRemove()) continue;
                if (!(other is AttackableUnit otherUnit)) continue;
                if (otherUnit.IsDead || otherUnit.Status.HasFlag(StatusFlags.Ghosted) || otherUnit.IsTemporarilyGhosted) continue;
                if (skipAllyLaneMinions && otherUnit is LaneMinion && otherUnit.Team == Team) continue;
                if (Vector2.Dot(objFwd, other.Position - Position) <= 0f) continue;

                float distSq = Vector2.DistanceSquared(Position, other.Position);
                if (distSq <= Epsilon) continue;
                float softThreshold = other.CollisionRadius + softRadius;
                if (distSq >= softThreshold * softThreshold) continue;

                Vector2 otherVelocity = Vector2.Zero;
                if (!otherUnit.IsPathEnded())
                {
                    Vector2 otherDir = otherUnit.CurrentWaypoint - otherUnit.Position;
                    float otherDirLenSq = otherDir.LengthSquared();
                    if (otherDirLenSq > Epsilon)
                    {
                        otherVelocity = otherDir * (otherUnit.GetMoveSpeed() / MathF.Sqrt(otherDirLenSq));
                    }
                }
                // Relative-movement gate (S4 Actor.cpp:281-284): identical vectors = lockstep
                // formation, no avoidance between its members. The client threshold is a per-axis
                // 1e-5 on m_Movement (essentially exact equality) — pairs with ANY relative drift
                // are collected and then filtered by the parallelness/speed branch in the
                // response. Mirror that: near-exact equality only.
                if ((myVelocity - otherVelocity).LengthSquared() <= 0.0001f) continue;

                members.Add(otherUnit);
                barycenter += other.Position;
                groupVelocity += otherVelocity;
            }
            if (members.Count == 0) return Vector2.Zero;
            barycenter /= members.Count;
            groupVelocity /= members.Count;

            // Group behind the movement: nothing to avoid (S4 Actor.cpp:755-758).
            Vector2 toCenter = barycenter - originalPos;
            if (Vector2.Dot(objFwd, toCenter) < 0f) return Vector2.Zero;

            float groupRadius = 0f;
            foreach (var m in members)
            {
                float r = Vector2.Distance(m.Position, barycenter) + m.CollisionRadius;
                if (r > groupRadius) groupRadius = r;
            }

            // Avoidance buffer caps at 15, not 20 (S4 Actor.cpp:760-764).
            float minDistanceBuffer = Math.Clamp(
                Math.Min(groupRadius, GetHardRadius()) * 0.25f, 12f, 15f);

            Vector2 rel = barycenter - Position;
            float relLenSq = rel.LengthSquared();
            Vector2 collisionNormal = relLenSq <= Epsilon ? objFwd : rel / MathF.Sqrt(relLenSq);
            float pushDistance = Math.Max(
                groupRadius + softRadius + minDistanceBuffer - MathF.Sqrt(relLenSq), 0f);
            if (pushDistance <= 0f) return Vector2.Zero;

            float groupVelLenSq = groupVelocity.LengthSquared();
            Vector2 inObjFwd = groupVelLenSq <= Epsilon
                ? (toCenter.LengthSquared() > Epsilon ? Vector2.Normalize(toCenter) : objFwd)
                : groupVelocity / MathF.Sqrt(groupVelLenSq);

            Vector2 side = new Vector2(objFwd.Y, -objFwd.X);
            float parallelness = Vector2.Dot(objFwd, inObjFwd);

            float sign;
            if (parallelness > AngleThreshold)
            {
                // Same-direction traffic (S4 Actor.cpp:807-817): if the group is as fast as us,
                // follow instead of overtaking — no response. Otherwise pick the overtake side
                // from the group's heading relative to our axis.
                if (myVelocity.LengthSquared() <= groupVelLenSq)
                {
                    return Vector2.Zero;
                }
                sign = ResolveSlideSign(Vector2.Dot(inObjFwd, side) > 0f ? 1f : -1f);
            }
            else
            {
                // Head-on or crossing: side picked by which side the group center is on
                // (S4 Actor.cpp:800-805 / 819-823 — same literal sign convention as the hard
                // branch).
                sign = ResolveSlideSign(Vector2.Dot(toCenter, side) > 0f ? 1f : -1f);
            }

            float sideDotAbs = Math.Abs(Vector2.Dot(collisionNormal, side));
            float slideRatio = sideDotAbs <= Epsilon ? pushDistance : pushDistance / sideDotAbs;
            float magnitude = movementMag * 0.4f; // S4 Actor.cpp:839
            if (slideRatio <= magnitude)
            {
                magnitude = Math.Max(0.01f, slideRatio);
            }

            return side * (sign * magnitude);
        }

        /// <summary>
        /// Body blocking for STATIONARY units -> Calculates a push vector that pushes this unit
        /// out of any overlap with AttackableUnit neighbors. Half of the overlap is applied per
        /// tick; the opposing unit pushes itself by the remainder in its own tick.
        ///
        /// Server-authority necessity, NOT a client port: the client's collision callback only
        /// runs for moving actors, but the server must resolve spawn/post-dash overlap for units
        /// that aren't walking (moving units use <see cref="ComputeGroupCollisionResponse"/>).
        /// </summary>
        private Vector2 ComputeSeparationPush(List<GameObject> nearby)
        {
            if (nearby.Count == 0) return Vector2.Zero;

            // FirstWave -> Ignore ally lane minion collision until reaching your own outer turret,
            // so that champions cannot manipulate the initial wave by body-blocking.
            bool skipAllyLaneMinions = this is LaneMinion firstWaveSelf
                                       && firstWaveSelf.IsFirstWave
                                       && !firstWaveSelf.HasPassedFirstTurret;

            Vector2 totalPush = Vector2.Zero;
            foreach (var other in nearby)
            {
                if (other == this || other.IsToRemove()) continue;
                if (!(other is AttackableUnit otherUnit)) continue;
                // Dead champions don't SetToRemove (they sit at death position until respawn,
                // ~30s) so without this filter they keep body-pushing live units. Ghosted units
                // pass through everything client-side too this matches PathingHandler's predicate.
                if (otherUnit.IsDead || otherUnit.Status.HasFlag(StatusFlags.Ghosted) || otherUnit.IsTemporarilyGhosted) continue;
                if (skipAllyLaneMinions && otherUnit is LaneMinion && otherUnit.Team == Team) continue;

                var diff = Position - other.Position;
                float distSq = diff.LengthSquared();
                // Min-distance buffer (S4 Actor.cpp:251-256): the collision trigger distance is
                // the radius sum PLUS clamp(min(selfR, otherR) * 0.25, 12, 20) — units separate
                // to slightly beyond raw contact. ASYMMETRIC: SELF uses GetHardRadius (champion r,
                // minion 0.2r — Actor.cpp:2394), NEIGHBOR uses full GetRadius (= CollisionRadius).
                float minDistanceBuffer = Math.Clamp(
                    Math.Min(GetHardRadius(), other.CollisionRadius) * 0.25f, 12f, 20f);
                float combinedRadius = GetHardRadius() + other.CollisionRadius + minDistanceBuffer;
                if (distSq >= combinedRadius * combinedRadius) continue; // no overlap

                float distance = (float)Math.Sqrt(distSq);
                Vector2 radialDir;
                float overlap;

                if (distance < 0.01f)
                {
                    //TODO: this needs tuning
                    // Perfect overlap -> deterministic fallback via NetId, so that the same pair
                    // always chooses the same direction -> should be no visual jitter.
                    float angle = (NetId * 2.39996f) % 6.28318f;
                    radialDir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    overlap = combinedRadius;
                }
                else
                {
                    radialDir = diff / distance;
                    overlap = combinedRadius - distance;
                }

                // Pure radial separation. (The old 0.3-radial/0.7-tangential mix was an invented
                // approximation of the client's slide behavior for WALKING units — superseded by
                // ComputeGroupCollisionResponse; this method only ever runs with zero movement
                // now, where radial is the only meaningful direction.)
                totalPush += radialDir * (overlap * 0.5f);
            }

            return totalPush;
        }

        /// <summary>
        /// Stuck Branch -> If, after the other checks above dont work, the effective movement for this tick is less
        /// than a certain fraction of the originally intended movement, the unit
        /// is considered stuck -> should happen typically in dense crowds where push vectors cancel each other out or
        /// are completely clamped out by the above clamp.
        ///
        /// Formula based on actor_client.cpp L6620-6681 thanks to Feta:
        ///   pushSpeed = ExtraSeparationSpeed × CollisionRadius  (units/sec)
        ///   pushSpeed = min(pushSpeed, maxSpeed × 1.5)
        ///   pushSpeed = min(pushSpeed, 95 units/sec)
        ///   push = (Position - centroid_of_neighbors).normalized × pushSpeed × deltaSeconds
        ///
        /// This push bypasses the clamp (in S1: (so this could be wrong) bypasses ±37.5% clamp) and is the only
        /// way for units to break free from stuck crowds when their normal
        /// movement has been reduced to zero by push cancellation.
        ///
        /// GATE (S4-verified 2026-06-12 vs Actor.cpp:469-470 + NSEAI.cfg [Collision]):
        ///   isStuckWithRepulse fires when |outMov| <= s_MinionMaxCollisionAvoidanceRatio * speed,
        ///   with s_MinionMaxCollisionAvoidanceRatio = 1.5 (NOT MinSpeedRatioBeforeStuck=0.25 — that
        ///   constant is a movement-scale in the path-active branch at Actor.cpp:1217, a different
        ///   mechanism). A 1.5x gate is met in essentially every contact, so this is a CONTINUOUS
        ///   gentle separation, not a "severely stuck" signal. The constant lives in Actor_Common
        ///   (base class) with no actor-type branch → applies to heroes AND minions identically;
        ///   "Minion" in the name is legacy. The genuinely-stuck escalation (temp-ghost counter)
        ///   uses a separate, stricter 0.25 gate in Move() so this frequent nudge can't ramp it.
        ///
        /// Magnitude (S4-verified): min(95, speed*1.5, ExtraSeparationSpeed * stuckRatio² * dt)
        /// with stuckRatio = stuckTimerSec / s_timeBetweenPathCorrections + 1 — but in patch 4.17
        /// `s_timeBetweenPathCorrections` is a never-assigned static (zero) → the ratio is +inf and
        /// the third term never binds: the push saturates INSTANTLY to min(95, speed*1.5). The
        /// gradual quadratic escalation only existed in S1. We mirror S4: no escalation term.
        /// </summary>
        private Vector2 ComputeStuckExtraPush(List<GameObject> nearby, Vector2 newDelta, Vector2 originalDelta, float deltaMs)
        {
            const float MinionMaxCollisionAvoidanceRatio = 1.5f; // s_MinionMaxCollisionAvoidanceRatio, Actor.cpp:469
            const float HardCapPerSec = 95.0f;
            const float MaxSpeedMultiplier = 1.5f;

            // Reference = the intended per-tick movement (originalDelta = this tick's walk before push).
            // Stationary / path-ended units (≈0) are handled by the separate stationary branch.
            float originalMagSq = originalDelta.LengthSquared();
            if (originalMagSq <= 0.01f) return Vector2.Zero;

            // Separation gate: fire unless the post-collision movement already exceeds 1.5x the
            // intended movement (i.e. the reflection/slide alone pushed hard enough).
            float gateSq = originalMagSq * (MinionMaxCollisionAvoidanceRatio * MinionMaxCollisionAvoidanceRatio);
            if (newDelta.LengthSquared() >= gateSq) return Vector2.Zero;

            // FirstWave -> ignore ally lane minion collisions until reaching your own outer turret
            // (similar to ComputeSeparationPush — otherwise the stuck push between ally minions
            // would trigger again even though Phase 1 correctly skips it).
            bool skipAllyLaneMinions = this is LaneMinion firstWaveSelf
                                       && firstWaveSelf.IsFirstWave
                                       && !firstWaveSelf.HasPassedFirstTurret;

            // this is the center of all overlapping neighbors. (Centoid)
            int count = 0;
            Vector2 centroid = Vector2.Zero;
            foreach (var other in nearby)
            {
                if (other == this || other.IsToRemove()) continue;
                if (!(other is AttackableUnit otherUnit)) continue;
                if (otherUnit.IsDead || otherUnit.Status.HasFlag(StatusFlags.Ghosted) || otherUnit.IsTemporarilyGhosted) continue;
                if (skipAllyLaneMinions && otherUnit is LaneMinion && otherUnit.Team == Team) continue;

                // Same collider classification as ComputeSeparationPush: the client's stuck push
                // takes the barycenter of the collider list, which was collected WITH the
                // direction gate and the min-distance buffer (S4 Actor.cpp:251-264) — mirror both
                // here so the centroid matches the set that actually blocks the movement.
                // originalDelta is guaranteed non-zero by the early-out above.
                if (Vector2.Dot(originalDelta, other.Position - Position) <= 0f) continue;

                var diff = Position - other.Position;
                float minDistanceBuffer = Math.Clamp(
                    Math.Min(GetHardRadius(), other.CollisionRadius) * 0.25f, 12f, 20f);
                float combinedRadius = GetHardRadius() + other.CollisionRadius + minDistanceBuffer;
                if (diff.LengthSquared() >= combinedRadius * combinedRadius) continue;

                centroid += other.Position;
                count++;
            }
            if (count == 0) return Vector2.Zero;
            centroid /= count;

            Vector2 awayFromCentroid = Position - centroid;
            Vector2 pushDir;
            if (awayFromCentroid.LengthSquared() < 0.01f)
            {
                // The centroid lies on the unit (perfectly symmetrical crowd). So a deterministic
                // fallback via NetId, to prevent the same setup from shifting between frames.
                float angle = (NetId * 2.39996f) % 6.28318f;
                pushDir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            }
            else
            {
                pushDir = Vector2.Normalize(awayFromCentroid);
            }

            // S4 4.17 runtime: min(95, speed*1.5) — the escalation term is dead, see doc above.
            float maxSpeed = GetMoveSpeed();
            float pushSpeedPerSec = Math.Min(maxSpeed * MaxSpeedMultiplier, HardCapPerSec);

            float deltaSec = deltaMs * 0.001f;
            return pushDir * (pushSpeedPerSec * deltaSec);
        }

        /// <summary>
        /// Clamp: Limits the magnitude of the separation push to a fraction of the unit's
        /// per-tick movement budget (max of actual walk delta and what the unit could walk
        /// at full move speed). This prevents stationary units from being shoved out
        /// instantly and keeps walking-unit body blocking smooth without oscillation.
        /// </summary>
        private Vector2 ClampSeparationPush(Vector2 push, Vector2 originalDelta, float deltaMs)
        {
            const float ClampRatio = 0.375f;

            float walkBudget = GetMoveSpeed() * 0.001f * deltaMs;
            float originalMag = originalDelta.Length();
            float budget = Math.Max(originalMag, walkBudget);
            if (budget <= 0.0001f) return push;

            float maxPushMag = budget * ClampRatio;
            float pushMagSq = push.LengthSquared();
            if (pushMagSq <= maxPushMag * maxPushMag) return push;

            return push * (maxPushMag / (float)Math.Sqrt(pushMagSq));
        }

        public bool PathTrueEndIs(Vector2 location)
        {
            return PathHasTrueEnd && PathTrueEnd == location;
        }

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
                bool useFast = (this as ObjAIBase)?.UsesFastPath ?? false;
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
        /// Resets this unit's waypoints.
        /// </summary>
        private void ResetWaypoints()
        {
            Waypoints = NavigationPath.OfSingle(Position);
            CurrentWaypointKey = 1;

            PathHasTrueEnd = false;
        }

        /// <summary>
        /// Returns whether this unit has reached the last waypoint in its path of waypoints.
        /// </summary>
        public bool IsPathEnded()
        {
            return CurrentWaypointKey >= Waypoints.Count;
        }

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
        public bool SetWaypoints(NavigationPath newWaypoints, bool isForced = false, bool broadcastImmediately = true)
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
                || (!isForced && (Status & MoveDisablingCC) != 0))
            {
                return false;
            }

            // Skip the per-tick WaypointGroup broadcast when the new path is identical to
            // the existing one. The unit's traversal state is unaffected; clients already
            // know this path. Reduces wire-format noise from periodic recomputes that
            // produce the same route. The new path is fresh (m_NextWaypoint=0); the current
            // path's cursor is CurrentWaypointKey (its progress so far), both threaded into the
            // faithful S4 IsPathTheSame so its near-unit prefix-skip works as the client's does.
            bool sameAsExisting = newWaypoints.IsPathTheSame(Waypoints, Position, 0, CurrentWaypointKey);

            Waypoints = newWaypoints;
            CurrentWaypointKey = 1;

            PathHasTrueEnd = false;

            if (!sameAsExisting && broadcastImmediately)
            {
                _movementUpdated = true;
                FullPathBroadcastPending = true;
            }

            return true;
        }

        /// <summary>
        /// Backward-compat overload — wraps the supplied list in a fresh <see cref="NavigationPath"/>.
        /// Existing call sites that still build paths as <c>new List&lt;Vector2&gt; { ... }</c> keep
        /// compiling. Prefer the <see cref="NavigationPath"/> overload for new code.
        /// </summary>
        public bool SetWaypoints(List<Vector2> newWaypoints, bool isForced = false, bool broadcastImmediately = true)
        {
            if (newWaypoints == null) return false;
            return SetWaypoints(new NavigationPath(newWaypoints), isForced, broadcastImmediately);
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
        /// <param name="b">Buff instance to add.</param>
        /// TODO: Probably needs a refactor to lessen thread usage. Make sure to stick very closely to the current method; just optimize it.
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
                        if (!b.IsHidden)
                        {
                            _game.PacketNotifier.NotifyNPC_BuffAdd2(b);
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

                    if (!b.IsHidden)
                    {
                        _game.PacketNotifier.NotifyNPC_BuffReplace(b);
                    }
                    b.ActivateBuff();
                }
                else if (b.BuffAddType == BuffAddType.RENEW_EXISTING)
                {
                    if (b != parentBuff)
                    {
                        RemoveBuffSlot(b);
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
                        var continuingBuff = new Buff(_game, b.Name, durationToAdd, parentBuff.StackCount, b.OriginSpell,
                            b.TargetUnit, b.SourceUnit, b.IsBuffInfinite(), b.ParentScript, b.Variables?.Clone());

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

                        if (!b.IsHidden)
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

                        if (!b.IsHidden)
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

                        if (!b.IsHidden)
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

                    if (!b.IsHidden)
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

                if (!b.IsHidden) _game.PacketNotifier.NotifyNPC_BuffRemove2(b);
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

                if (!b.IsHidden)
                {
                    if (b.BuffType == BuffType.COUNTER)
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(ParentBuffs[b.Name]);
                    }
                    else
                    {
                        _game.PacketNotifier.NotifyNPC_BuffUpdateCount(ParentBuffs[b.Name], ParentBuffs[b.Name].Duration,
                            ParentBuffs[b.Name].TimeElapsed);
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

                if (!b.IsHidden)
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

                if (!b.IsHidden)
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

                if (!b.IsHidden)
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
            foreach (var shield in Shields)
            {
                var consumed = shield.Consume(damageData);
                if (consumed != 0)
                {
                    _game.PacketNotifier.NotifyModifyShield(this, -consumed, shield.Physical, shield.Magical, false);
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
        /// <param name="endPos">Position to end the dash at.</param>
        /// <param name="speed">Amount of units the dash should travel in a second (movespeed).</param>
        /// <param name="gravity">Optionally how much gravity the unit will experience when above the ground while dashing.</param>
        /// <param name="keepFacingLastDirection">Whether or not the AI unit should face the direction they were facing before the dash.</param>
        /// <param name="lockActions">Whether or not to prevent movement, casting, or attacking during the duration of the movement.</param>
        /// TODO: Find a good way to grab these variables from spell data.
        /// TODO: Verify if we should count Dashing as a form of Crowd Control.
        /// TODO: Implement Dash class which houses these parameters, then have that as the only parameter to this function (and other Dash-based functions).
        public void ServerForceLinePath(Vector2 endPos, float speed, float gravity = 0.0f, bool keepFacingLastDirection = true, bool lockActions = true, string movementName = "", AttackableUnit caster = null, bool ignoreTerrain = false, Vector2 parabolicStartPoint = default, ForceMovementType movementType = ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersType movementOrdersType = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER)
        {
            if (MovementParameters != null)
            {
                SetForceMovementState(false, MoveStopReason.ForceMovement);
            }

            // FIRST_WALL_HIT: clamp the destination to the last walkable cell along the ray so the
            // unit stops at the wall instead of being teleported through it by GetClosestTerrainExit
            // when the requested endPos lies inside terrain. Track whether a wall was actually hit
            // so we can publish OnCollisionTerrain — clamping means the unit never physically
            // collides with terrain, but scripts (e.g. Vayne E wall-stun) rely on that signal.
            bool wallHit = false;
            if (!ignoreTerrain && movementType == ForceMovementType.FIRST_WALL_HIT)
            {
                var clampedEnd = _game.Map.NavigationGrid.GetFirstWallHitPoint(Position, endPos);
                if (clampedEnd != endPos)
                {
                    wallHit = true;
                    endPos = clampedEnd;
                }
            }

            var newCoords = ignoreTerrain ? endPos : _game.Map.NavigationGrid.GetClosestTerrainExit(endPos, PathfindingRadius + 1.0f);

            // POSTPONE_CURRENT_ORDER + an active plain MoveTo: snapshot the move destination (last
            // waypoint) NOW, before SetWaypoints below clears it. Re-issued at dash-end (ObjAIBase
            // .SetForceMovementState) so the unit resumes walking to it — AttackTo resumes on its own (the
            // TargetUnit survives), but a MoveTo's destination only lives in Waypoints. See P1b / the
            // forced-movement plan.
            Vector2 postponedMoveDest = Vector2.Zero;
            if (movementOrdersType == ForceMovementOrdersType.POSTPONE_CURRENT_ORDER
                && this is ObjAIBase moverSelf && moverSelf.MoveOrder == OrderType.MoveTo
                && Waypoints != null && Waypoints.Count > 1 && !IsPathEnded())
            {
                postponedMoveDest = Waypoints[Waypoints.Count - 1];
            }

            // False because we don't want this to be networked as a normal movement.
            SetWaypoints(new List<Vector2> { Position, newCoords }, true);

            // TODO: Take into account the rest of the arguments
            MovementParameters = new ForceMovementParameters
            {
                PostponedMoveDestination = postponedMoveDest,
                SetStatus = StatusFlags.None,
                ElapsedTime = 0,
                PathSpeedOverride = speed,
                ParabolicGravity = gravity,
                ParabolicStartPoint = parabolicStartPoint == default ? Position : parabolicStartPoint,
                KeepFacingDirection = keepFacingLastDirection,
                FollowNetID = 0,
                FollowDistance = 0,
                FollowBackDistance = 0,
                FollowTravelTime = 0,
                MovementName = movementName,
                MovementOrdersType = movementOrdersType,
                Caster = caster ?? this
            };

            if (lockActions)
            {
                MovementParameters.SetStatus = StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove;
            }

            SetForceMovementState(true, MoveStopReason.ForceMovement);

            // Movement is networked this way instead.
            // TODO: Verify if we want to use NotifyWaypointListWithSpeed instead as it does not require conversions.
            //_game.PacketNotifier.NotifyWaypointListWithSpeed(this, speed, gravity, keepFacingLastDirection, null, 0, 0, 20000.0f);
            _game.PacketNotifier.NotifyWaypointGroupWithSpeed(this);
            _movementUpdated = false;

            if (wallHit)
            {
                ApiEventManager.OnCollisionTerrain.Publish(this);
            }
        }

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
                if (MovementParameters.SetStatus != StatusFlags.None)
                {
                    SetStatus(MovementParameters.SetStatus, false);
                }
                ApiEventManager.OnMoveBegin.Publish(this, MovementParameters);
            }

            if (MovementParameters != null && state == false)
            {
                var movementParams = MovementParameters;
                MovementParameters = null;

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
        public void EnterStealth(float fadeTime = 0f, float value = 0.3f, bool IsFadingIn = true, float FadeTime = 0.0f, float MaxWeight = 0.2f)
        {
            if (fadeTime == 0f)
            {
                SetStatus(StatusFlags.Stealthed, true);
            }
            else
            {
                RegisterTimer(new GameScriptTimer(fadeTime, () =>
                {
                    SetStatus(StatusFlags.Stealthed, true);
                }));
            }

            _game.PacketNotifier.NotifyUnitInvis(fadeTime, value, this);
            if (this is Champion champ)
            {
                var tilt = new LeaguePackets.Game.Common.Color { Red = 255, Green = 128, Blue = 64, Alpha = 200 };
                _game.PacketNotifier.ColorRemapFx(champ, IsFadingIn, FadeTime, tilt, MaxWeight, false);
            }
        }
        public void ExitStealth()
        {
            SetStatus(StatusFlags.Stealthed, false);
            _game.PacketNotifier.NotifyUnitInvis(0, 1f, this);
            if (this is Champion champ)
            {
                var tilt = new LeaguePackets.Game.Common.Color { Red = 0, Green = 0, Blue = 0, Alpha = 0 };
                _game.PacketNotifier.ColorRemapFx(champ, false, 0f, tilt, 0f);
            }
            _game.ObjectManager.RefreshUnitVision(this);
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
