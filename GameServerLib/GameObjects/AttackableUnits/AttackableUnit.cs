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
        /// Stats used purely in networking the accompishments or status of units and their gameplay affecting stats.
        /// </summary>
        public Replication Replication { get; protected set; }
        /// <summary>
        /// Variable housing all of this Unit's stats such as health, mana, armor, magic resist, ActionState, etc.
        /// Currently these are only initialized manually by ObjAIBase and ObjBuilding.
        /// </summary>
        public Stats Stats { get; protected set; }
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

        // Mirrors NSEAI.cfg [Collision] `CanActorsSlideIntoOccupiedGridSquares = 1` (patch 4.20).
        // S4 Actor_Common.cpp:1265+ has 12 call sites gating the per-step movement-collision
        // hard-stop on this flag; loaded from CFG at S4:8042-8044, default false in code but
        // CFG sets it true in patch 4.20.
        //
        // 2026-05-04 attempt set this to true → stuck regression at multiple positions around
        // the nexus → reverted under hypothesis "needs Phase-1 inner-loop terrain check first".
        // 2026-05-05: that regression may have been amplified or fully caused by the unrelated
        // StopMovement double-Notify race-condition (commit 24bba97) which was fixed afterward.
        // RE-TEST: with StopMovement now batched cleanly, this flag may work without the
        // Phase-1 inner-loop port. If regression returns, full Phase-1 port (S1 actor_client.cpp
        // :2241-2259) is genuinely needed. Either way, this flag changes ONLY whether body-push
        // applied lands the unit in NOT_PASSABLE; the unit slides into the cell instead of being
        // hard-stopped at the boundary. Subsequent ticks' OnCollision-isTerrain extracts via
        // `SetPosition(exit, repath:true)` basically the same recovery flow as before.
        private const bool ENABLE_ACTORS_SLIDE_INTO_OCCUPIED = true;

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
        private StatusFlags _statusBeforeApplyingBuffEfects = 0;
        private StatusFlags _buffEffectsToEnable = 0;
        private StatusFlags _buffEffectsToDisable = 0;
        private StatusFlags _dashEffectsToDisable = 0;

        /// <summary>
        /// Parameters of any forced movements (dashes) this unit is performing.
        /// </summary>
        public ForceMovementParameters MovementParameters { get; protected set; }
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
                SetDashingState(false, MoveStopReason.ForceMovement);
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
            // NOTE: deliberately do NOT bail on IsPathEnded() if HandleMove's null fallback
            // produced degenerate `[currentPos, currentPos]` waypoints, the path appears "ended"
            // immediately even though the unit is in a stuck cell. We still want recovery in that
            // case if the unit is actually on a non-walkable cell.
            bool wantsToMove = Waypoints.Count > 1 || !_game.Map.PathingHandler.IsWalkable(Position, 0f);
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
                SetWaypoints(newPath);
                return true;
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
                    remainingFrameTime = DashMove(diff);
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

                Vector2 exit = _game.Map.NavigationGrid.GetClosestTerrainExit(Position, PathfindingRadius + 1.0f);
                // repath:true triggers SetPosition's SafePath branch which re-routes the existing
                // waypoints around whatever blocker we just collided with. With repath:false the
                // goal-side of the path stays unchanged — when the unit collides with a building
                // dynamic-blocker (Inhibitor/Nexus footprint), Waypoints[1..] still point INTO the
                // blocker, so the next tick walks the unit straight back in, OnCollision fires
                // again, and the unit drifts deeper with each oscillation. Symptom: clicking
                // beyond an inhibitor pushes the unit further INTO it instead of around. The
                // historical reason for repath:false was fear of a safe/unsafe path oscillation
                // loop (per the older `SetPosition` TODO), which A1 actor-aware A* + the
                // UpdateStuckRecovery watchdog now mitigate.
                SetPosition(exit, repath: true);
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
                ApiEventManager.OnBeingHit.Publish(damageData.Target, damageData.Attacker);

                // Wards should not trigger on-hit proc pipelines; Therfore each basic attack consumes only one ward hit.
                if (!targetIsWard)
                    ApiEventManager.OnHitUnit.Publish(damageData.Attacker as ObjAIBase, damageData);
                
                //TODO: find a use case for these OnDodge OnBeingDodged and OnMiss
                if (damageData.DamageResultType == DamageResultType.RESULT_DODGE)
                {
                    ApiEventManager.OnDodge.Publish(damageData.Target, damageData.Attacker);
                    ApiEventManager.OnBeingDodged.Publish(damageData.Attacker, damageData.Target);
                }
                else if (damageData.DamageResultType == DamageResultType.RESULT_MISS)
                {
                    ApiEventManager.OnMiss.Publish(damageData.Attacker, damageData.Target);
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
                c.AddAssistMarker(cAttacker, 10.0f, damageData);
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

            if (data.Killer != null && data.Killer is Champion champion)
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
            if (Model.Equals(model))
            {
                return false;
            }
            Model = model;
            _game.PacketNotifier.NotifyS2C_ChangeCharacterData(this);
            return true;
        }

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
            if (enabled)
            {
                _statusBeforeApplyingBuffEfects |= status;
            }
            else
            {
                _statusBeforeApplyingBuffEfects &= ~status;
            }
            Status = (
                (
                    _statusBeforeApplyingBuffEfects
                    & ~_buffEffectsToDisable
                )
                | _buffEffectsToEnable
            )
            & ~_dashEffectsToDisable;

            UpdateActionState();
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
            // Combine the status effects of all the buffs
            _buffEffectsToEnable = 0;
            _buffEffectsToDisable = 0;

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

                    _buffEffectsToEnable |= buff.StatusEffectsToEnable;
                    _buffEffectsToDisable |= buff.StatusEffectsToDisable;
                }
            }

            // If the effect should be enabled, it overrides disable.
            _buffEffectsToDisable &= ~_buffEffectsToEnable;

            // Set the status effects of this unit.
            SetStatus(StatusFlags.None, true);
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

        private float DashMove(float frameTime)
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
                    SetDashingState(false, MoveStopReason.LostTarget);
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
                    timeRemaining = MP.FollowTravelTime - MP.ElapsedTime;
                }
            }
            else
            {
                if (Waypoints == null || Waypoints.Count <= 1)
                {
                    SetDashingState(false, MoveStopReason.ForceMovement);
                    return frameTime;
                }

                dir = Waypoints[1] - Position;
                if (float.IsNaN(dir.X) || float.IsNaN(dir.Y) || float.IsInfinity(dir.X) || float.IsInfinity(dir.Y))
                {
                    SetDashingState(false, MoveStopReason.ForceMovement);
                    return frameTime;
                }
                distToDest = dir.Length();
            }
            distRemaining = Math.Min(distToDest, distRemaining);

            float time = Math.Min(frameTime, timeRemaining);
            float speed = MP.PathSpeedOverride * 0.001f;
            float distPerFrame = speed * time;
            float dist = Math.Min(distPerFrame, distRemaining);
            if (dir != Vector2.Zero)
            {
                Position += Vector2.Normalize(dir) * dist;
            }

            if (distRemaining <= distPerFrame)
            {
                SetDashingState(false);
                return (distPerFrame - distRemaining) / speed;
            }
            if (timeRemaining <= frameTime)
            {
                SetDashingState(false);
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
                var nearby = _game.Map.CollisionHandler.GetNearestObjects(this);

                Vector2 originalDelta = Position - originalPos;

                Vector2 rawPush = ComputeSeparationPush(nearby, originalDelta);

                // Clamp the push against a per tick movement budget. Includes a walk-speed
                // fallback so stationary units (e.g. auto-attacking) are also clamped instead
                // of being teleported by the full overlap push.
                rawPush = ClampSeparationPush(rawPush, originalDelta, delta);

                // Frametime correct smoothing. alpha = 1 - exp(-delta/tau). Keeps the response
                // consistent at different tick rates and shortens the tail after the overlap clears.
                const float PushSmoothingTauMs = 60f;
                float alpha = 1f - (float)Math.Exp(-delta / PushSmoothingTauMs);
                _smoothedSeparationPush = _smoothedSeparationPush * (1f - alpha)
                                        + rawPush * alpha;

                // Body blocking push, terrain gated. Mirrors S1 actor_client.cpp:2241-2259: if
                // the collision response would land Position in a NOT_PASSABLE / dynamic blocker
                // cell, drop the push instead of applying it. Without this, units crowded against
                // a wall (champion attacking Inhibitor/Nexus surrounded by minions) get body-shoved
                // into the building's dynamic blocker. Drained smoothed state on rejection so the
                // next tick starts with a fresh push attempt rather than carrying the bad push.
                if (_smoothedSeparationPush.LengthSquared() > 0.0001f)
                {
                    Vector2 candidate = Position + _smoothedSeparationPush;
                    bool canApply = ENABLE_ACTORS_SLIDE_INTO_OCCUPIED
                        || _game.Map.NavigationGrid.IsWalkable(candidate, 0f);
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

                // Stuck Detection + Extra Push Along the Centroid Direction
                Vector2 newDelta = Position - originalPos;
                Vector2 stuckPush = ComputeStuckExtraPush(nearby, newDelta, originalDelta, delta);
                if (stuckPush.LengthSquared() > 0.0001f)
                {
                    Vector2 candidate = Position + stuckPush;
                    bool canApply = ENABLE_ACTORS_SLIDE_INTO_OCCUPIED
                        || _game.Map.NavigationGrid.IsWalkable(candidate, 0f);
                    if (canApply)
                    {
                        Position = candidate;
                        _unreplicatedDrift += stuckPush;
                        pushed = true;
                    }
                    // else: drop the stuck push too. UpdateStuckRecovery's escalating-repath
                    // watchdog still runs and will snap the unit out via TryUnstuckRepath if
                    // genuinely wedged.
                }

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
                const float DriftResyncThreshold = 175f;
                if (Waypoints.Count > 1
                    && _unreplicatedDrift.LengthSquared() > DriftResyncThreshold * DriftResyncThreshold)
                {
                    _movementUpdated = true;
                }
            }
            else
            {
                _smoothedSeparationPush = Vector2.Zero;
            }

            return walked || pushed;
        }

        /// <summary>
        /// Body blocking -> Calculates a push vector that pushes this unit out of any overlap
        /// with AttackableUnit neighbors. Half of the overlap is applied per tick
        /// Also the opposing unit pushes itself by the remainder in its own tick, so that a pair is
        /// completely separated in one tick (before Phase 2 Clamp).
        ///
        /// Tangential Slide
        /// </summary>
        private Vector2 ComputeSeparationPush(List<GameObject> nearby, Vector2 movementDelta)
        {
            if (nearby.Count == 0) return Vector2.Zero;

            // FirstWave -> Ignore ally lane minion collision until reaching your own outer turret,
            // so that champions cannot manipulate the initial wave by body-blocking.
            bool skipAllyLaneMinions = this is LaneMinion firstWaveSelf
                                       && firstWaveSelf.IsFirstWave
                                       && !firstWaveSelf.HasPassedFirstTurret;

            float movementMag = movementDelta.Length();
            Vector2 movementDir = movementMag > 0.001f ? movementDelta / movementMag : Vector2.Zero;
            const float TangentialMix = 0.7f;
            const float RadialMix = 0.3f;

            Vector2 totalPush = Vector2.Zero;
            foreach (var other in nearby)
            {
                if (other == this || other.IsToRemove()) continue;
                if (!(other is AttackableUnit otherUnit)) continue;
                // Dead champions don't SetToRemove (they sit at death position until respawn,
                // ~30s) so without this filter they keep body-pushing live units. Ghosted units
                // pass through everything client-side too this matches PathingHandler's predicate.
                if (otherUnit.IsDead || otherUnit.Status.HasFlag(StatusFlags.Ghosted)) continue;
                if (skipAllyLaneMinions && otherUnit is LaneMinion && otherUnit.Team == Team) continue;

                var diff = Position - other.Position;
                float distSq = diff.LengthSquared();
                float combinedRadius = CollisionRadius + other.CollisionRadius;
                if (distSq >= combinedRadius * combinedRadius) continue; // kein Overlap

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

                Vector2 pushDir;
                if (movementMag > 0.001f)
                {
                    Vector2 tangent1 = new Vector2(-radialDir.Y, radialDir.X);
                    Vector2 tangent2 = new Vector2(radialDir.Y, -radialDir.X);
                    Vector2 tangentDir = Vector2.Dot(tangent1, movementDir) > Vector2.Dot(tangent2, movementDir)
                        ? tangent1 : tangent2;
                    Vector2 mixed = radialDir * RadialMix + tangentDir * TangentialMix;
                    float mixedMag = mixed.Length();
                    pushDir = mixedMag > 0.001f ? mixed / mixedMag : radialDir;
                }
                else
                {
                    pushDir = radialDir;
                }

                totalPush += pushDir * (overlap * 0.5f);
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
        /// Constants estimated:
        ///   - MinSpeedRatioBeforeStuck = 0.1 (10% — if less than that remains → stuck)
        ///   - ExtraSeparationSpeed = 50.0 (suggested by a friend, might be off)
        /// TODO: find exact values
        /// </summary>
        private Vector2 ComputeStuckExtraPush(List<GameObject> nearby, Vector2 newDelta, Vector2 originalDelta, float deltaMs)
        {
            const float MinSpeedRatioBeforeStuck = 0.1f;
            const float ExtraSeparationSpeed = 50.0f;
            const float HardCapPerSec = 95.0f;
            const float MaxSpeedMultiplier = 1.5f;

            // Stuck Check requires a meaningful initial movement as a reference
            // otherwise we won't detect “stuck” at all (stationary units are already pushed indefinitely
            // by Early Exit).
            float originalMagSq = originalDelta.LengthSquared();
            if (originalMagSq <= 0.01f) return Vector2.Zero;

            float stuckThresholdSq = originalMagSq * (MinSpeedRatioBeforeStuck * MinSpeedRatioBeforeStuck);
            if (newDelta.LengthSquared() >= stuckThresholdSq) return Vector2.Zero; // nicht stuck

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
                if (otherUnit.IsDead || otherUnit.Status.HasFlag(StatusFlags.Ghosted)) continue;
                if (skipAllyLaneMinions && otherUnit is LaneMinion && otherUnit.Team == Team) continue;

                var diff = Position - other.Position;
                float combinedRadius = CollisionRadius + other.CollisionRadius;
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

            float maxSpeed = GetMoveSpeed();
            float pushSpeedPerSec = ExtraSeparationSpeed * CollisionRadius;
            pushSpeedPerSec = Math.Min(pushSpeedPerSec, maxSpeed * MaxSpeedMultiplier);
            pushSpeedPerSec = Math.Min(pushSpeedPerSec, HardCapPerSec);

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
        public bool SetWaypoints(NavigationPath newWaypoints, bool isForced = false)
        {
            // Waypoints should always have an origin at the current position.
            // Dashes are excluded as their paths should be set before being applied.
            // TODO: Find out the specific cases where we shouldn't be able to set our waypoints. Perhaps CC?
            // Setting waypoints during auto attacks is allowed.
            if (newWaypoints == null || newWaypoints.Count <= 1 || newWaypoints[0] != Position || (!isForced && !CanChangeWaypoints()))
            {
                return false;
            }

            // Skip the per-tick WaypointGroup broadcast when the new path is identical to
            // the existing one. The unit's traversal state is unaffected; clients already
            // know this path. Reduces wire-format noise from periodic recomputes that
            // produce the same route.
            bool sameAsExisting = newWaypoints.IsPathTheSame(Waypoints);

            Waypoints = newWaypoints;
            CurrentWaypointKey = 1;

            PathHasTrueEnd = false;

            if (!sameAsExisting)
            {
                _movementUpdated = true;
            }

            return true;
        }

        /// <summary>
        /// Backward-compat overload — wraps the supplied list in a fresh <see cref="NavigationPath"/>.
        /// Existing call sites that still build paths as <c>new List&lt;Vector2&gt; { ... }</c> keep
        /// compiling. Prefer the <see cref="NavigationPath"/> overload for new code.
        /// </summary>
        public bool SetWaypoints(List<Vector2> newWaypoints, bool isForced = false)
        {
            if (newWaypoints == null) return false;
            return SetWaypoints(new NavigationPath(newWaypoints), isForced);
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
                SetDashingState(false, reason);
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
            _game.PacketNotifier.NotifyModifyShield(this, shield.Amount, shield.Physical, shield.Magical, true);
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
                    _game.PacketNotifier.NotifyModifyShield(this, -shield.Amount, shield.Physical, shield.Magical, true);
                }
                ApiEventManager.OnShieldBreak.Publish(shield);
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
        /// Forces this unit to perform a dash which ends at the given position.
        /// </summary>
        /// <param name="endPos">Position to end the dash at.</param>
        /// <param name="dashSpeed">Amount of units the dash should travel in a second (movespeed).</param>
        /// <param name="animation">Internal name of the dash animation.</param>
        /// <param name="leapGravity">Optionally how much gravity the unit will experience when above the ground while dashing.</param>
        /// <param name="keepFacingLastDirection">Whether or not the AI unit should face the direction they were facing before the dash.</param>
        /// <param name="consideredCC">Whether or not to prevent movement, casting, or attacking during the duration of the movement.</param>
        /// TODO: Find a good way to grab these variables from spell data.
        /// TODO: Verify if we should count Dashing as a form of Crowd Control.
        /// TODO: Implement Dash class which houses these parameters, then have that as the only parameter to this function (and other Dash-based functions).
        public void DashToLocation(Vector2 endPos, float dashSpeed, string animation = "", float leapGravity = 0.0f, bool keepFacingLastDirection = true, bool consideredCC = true, string movementName = "", AttackableUnit caster = null, bool ignoreTerrain = false, Vector2 parabolicStartPoint = default, ForceMovementType movementType = ForceMovementType.FURTHEST_WITHIN_RANGE)
        {
            if (MovementParameters != null)
            {
                SetDashingState(false, MoveStopReason.ForceMovement);
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

            // False because we don't want this to be networked as a normal movement.
            SetWaypoints(new List<Vector2> { Position, newCoords }, true);

            // TODO: Take into account the rest of the arguments
            MovementParameters = new ForceMovementParameters
            {
                SetStatus = StatusFlags.None,
                ElapsedTime = 0,
                PathSpeedOverride = dashSpeed,
                ParabolicGravity = leapGravity,
                ParabolicStartPoint = parabolicStartPoint == default ? Position : parabolicStartPoint,
                KeepFacingDirection = keepFacingLastDirection,
                FollowNetID = 0,
                FollowDistance = 0,
                FollowBackDistance = 0,
                FollowTravelTime = 0,
                MovementName = movementName,
                Caster = caster ?? this
            };

            if (consideredCC)
            {
                MovementParameters.SetStatus = StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove;
            }

            SetDashingState(true, MoveStopReason.ForceMovement);

            if (animation != null && animation != "")
            {
                var animPairs = new Dictionary<string, string> { { "RUN", animation } };
                SetAnimStates(animPairs, MovementParameters);
            }

            // Movement is networked this way instead.
            // TODO: Verify if we want to use NotifyWaypointListWithSpeed instead as it does not require conversions.
            //_game.PacketNotifier.NotifyWaypointListWithSpeed(this, dashSpeed, leapGravity, keepFacingLastDirection, null, 0, 0, 20000.0f);
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
        /// <param name="setStatus">Whether or not to modify movement, casting, and attacking states.</param>
        /// TODO: Implement ForcedMovement methods and enumerators to handle different kinds of dashes.
        public virtual void SetDashingState(bool state, MoveStopReason reason = MoveStopReason.Finished)
        {
            _dashEffectsToDisable = 0;
            if (state)
            {
                _dashEffectsToDisable = MovementParameters.SetStatus;
            }
            SetStatus(StatusFlags.None, true);

            if (MovementParameters != null && state == false)
            {
                RemoveAnimStates(MovementParameters);
                var movementParams = MovementParameters;
                MovementParameters = null;

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
        public void SetAnimStates(Dictionary<string, string> animPairs, object source = null)
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
                    list.Add(new AnimOverrideInfo { OverrideValue = newValue, Source = source });
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
