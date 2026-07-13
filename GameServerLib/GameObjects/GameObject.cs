using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Packets;

namespace LeagueSandbox.GameServer.GameObjects
{
    /// <summmary>
    /// Base class for all objects.
    /// GameObjects normally follow these guidelines of functionality: Position, Direction, Collision, Vision, Team, and Networking.
    /// </summmary>
    public class GameObject
    {
        // Crucial Vars (keep in mind Game is everywhere, which could be an issue for the future)
        protected Game _game;
        /// <summary>Engine-internal access for components that hang off a unit but live outside
        /// the GameObject hierarchy (e.g. Replication needs Config.GameFeatures).</summary>
        internal Game Game => _game;
        protected NetworkIdManager _networkIdManager;

        // Function Vars
        protected bool _toRemove;
        protected bool _movementUpdated;

        protected Dictionary<TeamId, bool> _visibleByTeam;
        protected HashSet<int> _spawnedForPlayers = new HashSet<int>();
        protected Dictionary<int, bool> _visibleForPlayers = new Dictionary<int, bool>();
        private static Fade _defaultFade = new Fade(0, 0, 0, 1);
        private Fade _currentFade = _defaultFade;
        private int _nextFadeId = 0;
        // What was the opacity at the moment when the current fade replaced the previous one.
        private float _currentFadeStartOpacity = 0;
        private Fade _previousFade = null;
        private List<Fade> _fades = new List<Fade>();
        // Wire model (Riot BBPush/PopCharacterFade): every FadeOut broadcasts a Push with a
        // running per-unit FadeId, every FadeIn broadcasts the matching Pop — the CLIENT owns the
        // stack (mTargetFadeValues, keyed by id). The version counter + per-player snapshot drive
        // the enter-vision catch-up in OnSync (pop what that client may still hold, replay the
        // live stack), replacing the old value-snapshot pushes that grew the client stack forever.
        private int _fadeStackVersion;
        private Dictionary<int, KeyValuePair<int, short[]>> _lastFadeStackSeenByPlayer = new Dictionary<int, KeyValuePair<int, short[]>>();

        /// <summary>
        /// A set of players with vision of this GameObject.
        /// Can be iterated through.
        /// </summary>
        public IEnumerable<int> VisibleForPlayers
        {
            get
            {
                foreach(var kv in _visibleForPlayers)
                {
                    if(kv.Value)
                    {
                        yield return kv.Key;
                    }
                }
            }
        }

        /// <summary>
        /// Players that have received a spawn packet for this object i.e., the client has it in memory,
        /// regardless of current FoW state. Use this (not VisibleForPlayers) for "destroy" packets like
        /// FX_Kill where the client must clean up state even when the object is currently out of vision.
        /// </summary>
        public IEnumerable<int> SpawnedForPlayers => _spawnedForPlayers;

        /// <summary>
        /// Arrival snap slack for missile movement. NOT a Riot constant — Riot has no distance
        /// epsilon: S1 obj_SpellLineMissile::CheckAtTargetPoint/CheckAtEndPoint are plane-crossing
        /// tests (position + velocity·dt·2 projected onto the flight axis, then SNAP to the
        /// endpoint — a 2-frame, speed-proportional lookahead), and unit arrival uses the target's
        /// bounding radius as the offset (obj_SpellMissile::CheckCollide, offset = lastTargetSize).
        /// Our deltaMovement &gt;= dist overstep check is the 1-frame equivalent; this fixed 5u is
        /// only a float-creep guard that never fires above ~150 u/s missile speed (the overstep
        /// snaps first), so it stays as-is.
        /// </summary>
        public static readonly uint MOVEMENT_EPSILON = 5;

        /// <summary>
        ///  Identifier unique to this game object.
        /// </summary>
        public uint NetId { get; }
        /// <summary>
        /// Radius of the circle which is used for collision detection between objects or terrain.
        /// </summary>
        public float CollisionRadius { get; protected set; }
        /// <summary>
        /// Radius of the circle which is used for pathfinding around objects and terrain.
        /// </summary>
        public float PathfindingRadius { get; protected set; }
        /// <summary>
        /// Position of this GameObject from a top-down view.
        /// </summary>
        public Vector2 Position { get; protected set; }
        /// <summary>
        /// Riot's engine-default spawn facing: world +Z. Replay-verified on stationary spawned
        /// minions (Jinx E chompers): the spawn 0xBA carries MovementDataStop with
        /// Forward = (0, 1). Units that never received an explicit facing keep this heading.
        /// </summary>
        public static readonly Vector3 DEFAULT_FACING = new Vector3(0, 0, 1);

        /// <summary>
        /// 3D orientation of this GameObject (based on ground-level).
        /// </summary>
        public Vector3 Direction { get; protected set; }
        /// <summary>
        /// Team identifier, refer to TeamId enum.
        /// </summary>
        public TeamId Team { get; protected set; }
        /// <summary>
        /// Radius of the circle which is used for vision; detecting if objects are visible given terrain, and if so, networked to the player (or team) that owns this game object.
        /// </summary>
        public float VisionRadius { get; protected set; }

        // Per-object vision-radius SCALE (S4 Riot::Region::GetSizeModifiersBasedOnAttachment ->
        // AttackableUnit::GetVisionScale). The effective vision radius is
        //   VisionRadius * VisionScaleMultiplier + VisionScaleAdditive
        // mirroring S4 CircleRegion::GetActualRadius() = mRadius * mult + add (Region.cpp:425).
        // Base GameObject has no scale (mult=1, add=0); AttackableUnit overrides to apply
        // vision-range buff/item bonuses (obj_AI_Base::GetVisionScale = 1+pct, flat). Vision
        // queries (ObjectManager) use GetEffectiveVisionRadius() so dynamic vision bonuses take
        // effect without re-baking VisionRadius.
        public virtual float VisionScaleMultiplier => 1f;
        public virtual float VisionScaleAdditive => 0f;
        public float GetEffectiveVisionRadius() => VisionRadius * VisionScaleMultiplier + VisionScaleAdditive;


        public virtual bool IsAffectedByFoW => false;
        public virtual bool SpawnShouldBeHidden => false;

        /// <summary>
        /// Whether this object auto-registers as a team vision provider on spawn / team change.
        /// Wards opt out (see <see cref="Minion.IsWard"/>): a ward's vision comes solely from an
        /// explicit perception-bubble Region, so the per-ward radius and reveal-stealth live in one
        /// place. This matches Riot, where a ward's vision IS its networked region (bound to the
        /// ward unit) rather than the unit's intrinsic perception — and avoids double-providing.
        /// </summary>
        public virtual bool AutoProvidesVision => true;

        /// <summary>
        /// Instantiation of an object which represents the base class for all objects in League of Legends.
        /// </summary>
        public GameObject(Game game, Vector2 position, float collisionRadius = 40f, float pathingRadius = 40f, float visionRadius = 0f, uint netId = 0, TeamId team = TeamId.TEAM_NEUTRAL)
        {
            _game = game;
            _networkIdManager = game.NetworkIdManager;
            if (netId != 0)
            {
                NetId = netId; // Custom netId
            }
            else
            {
                NetId = _networkIdManager.GetNewNetId(); // base class assigns a netId
            }
            Position = position;
            Direction = DEFAULT_FACING;
            CollisionRadius = collisionRadius;
            PathfindingRadius = pathingRadius;
            VisionRadius = visionRadius;

            _visibleByTeam = new Dictionary<TeamId, bool>();
            var teams = Enum.GetValues(typeof(TeamId)).Cast<TeamId>();
            foreach (var t in teams)
            {
                _visibleByTeam.Add(t, false);
            }

            Team = team;
            _movementUpdated = false;
            _toRemove = false;
        }

        /// <summary>
        /// Called by ObjectManager after AddObject (usually right after instatiation of GameObject).
        /// </summary>
        public virtual void OnAdded()
        {
            _game.Map.CollisionHandler.AddObject(this);
            if (AutoProvidesVision)
            {
                _game.ObjectManager.AddVisionProvider(this, Team);
            }
        }

        /// <summary>
        /// Called by ObjectManager every tick.
        /// </summary>
        /// <param name="diff">Number of milliseconds that passed before this tick occurred.</param>
        public virtual void Update(float diff)
        {
        }

        public virtual void LateUpdate(float diff)
        {
        }

        /// <summary>
        /// Whether or not the object should be removed from the game (usually both server and client-side). Refer to ObjectManager.
        /// </summary>
        public bool IsToRemove()
        {
            return _toRemove;
        }

        /// <summary>
        /// Will cause ObjectManager to remove the object (usually) both server and client-side next update.
        /// </summary>
        public virtual void SetToRemove()
        {
            _toRemove = true;
        }

        /// <summary>
        /// Called by ObjectManager after the object has been SetToRemove.
        /// </summary>
        public virtual void OnRemoved()
        {
            _game.Map.CollisionHandler.RemoveObject(this);
            _game.ObjectManager.RemoveVisionProvider(this, Team);
        }

        /// <summary>
        /// Refers to the height that the object is at in 3D space.
        /// </summary>
        public virtual float GetHeight()
        {
            return _game.Map.NavigationGrid.GetHeightAtLocation(Position);
        }

        /// <summary>
        /// Gets the position of this GameObject in 3D space, where the Y value represents height.
        /// Mostly used for packets.
        /// </summary>
        /// <returns>Vector3 position.</returns>
        public virtual Vector3 GetPosition3D()
        {
            return new Vector3(Position.X, GetHeight(), Position.Y);
        }

        /// <summary>
        /// Sets the server-sided position of this object.
        /// </summary>
        public virtual void SetPosition(float x, float y)
        {
            SetPosition(new Vector2(x, y));
        }

        /// <summary>
        /// Sets the server-sided position of this object.
        /// </summary>
        public virtual void SetPosition(Vector2 vec)
        {
            Position = vec;
        }

        /// <summary>
        /// Sets the collision radius of this GameObject.
        /// </summary>
        /// <param name="newRadius">Radius to set.</param>
        public void SetCollisionRadius(float newRadius)
        {
            CollisionRadius = newRadius;
        }

        /// <summary>
        /// Sets this GameObject's current orientation (only X and Z are used in movement).
        /// </summary>
        public void FaceDirection(Vector3 newDirection, bool isInstant = true, float turnTime = 0.08333f)
        {
            if (newDirection == Vector3.Zero || float.IsNaN(newDirection.X) || float.IsNaN(newDirection.Y) || float.IsNaN(newDirection.Z))
            {
                return;
            }

            Direction = newDirection;
            if (_game.ObjectManager.GetObjectById(NetId) != null)
            {
                _game.PacketNotifier.NotifyFaceDirection(this, newDirection, isInstant, turnTime);
            }
        }

        /// <summary>
        /// Whether or not the specified object is colliding with this object.
        /// </summary>
        /// <param name="o">An object that could be colliding with this object.</param>
        public virtual bool IsCollidingWith(GameObject o)
        {
            return Vector2.DistanceSquared(Position, o.Position) < (CollisionRadius + o.CollisionRadius) * (CollisionRadius + o.CollisionRadius);
        }

        /// <summary>
        /// Called by ObjectManager when the object is ontop of another object or when the object is inside terrain.
        /// </summary>
        public virtual void OnCollision(GameObject collider, bool isTerrain = false)
        {
            // TODO: Verify if we should trigger events here.

            if (isTerrain)
            {
                // Escape functionality should be moved to GameObject.OnCollision.
                // only time we would collide with terrain is if we are inside of it, so we should teleport out of it.
                Vector2 exit = _game.Map.NavigationGrid.GetClosestTerrainExit(Position, PathfindingRadius + 1.0f);
                SetPosition(exit);
            }
        }

        protected virtual void OnSpawn(int userId, TeamId team, bool doVision)
        {
            _game.PacketNotifier.NotifySpawn(this, team, userId, _game.GameTime, doVision);
        }

        protected virtual void OnEnterVision(int userId, TeamId team)
        {
            _game.PacketNotifier.NotifyVisibilityChange(this, team, true, userId);
        }

        protected virtual void OnSync(int userId, TeamId team)
        {
        }

        protected virtual void OnLeaveVision(int userId, TeamId team)
        {
            _game.PacketNotifier.NotifyVisibilityChange(this, team, false, userId);
        }

        public virtual void Sync(int userId, TeamId team, bool visible, bool forceSpawn = false)
        {
            visible = visible || !IsAffectedByFoW;

            if (!forceSpawn && IsSpawnedForPlayer(userId))
            {
                if (IsAffectedByFoW && (IsVisibleForPlayer(userId) != visible))
                {
                    if(visible)
                    {
                        OnEnterVision(userId, team);
                    }
                    else
                    {
                        OnLeaveVision(userId, team);
                    }
                    SetVisibleForPlayer(userId, visible);
                }
                else if(visible)
                {
                    OnSync(userId, team);
                }
            }
            else if (visible || !SpawnShouldBeHidden)
            {
                OnSpawn(userId, team, visible);
                SetVisibleForPlayer(userId, visible);
                SetSpawnedForPlayer(userId);
            }
            
            if (forceSpawn)
            {
                // Fresh client object -> its fade stack is empty; forget what this player saw.
                _lastFadeStackSeenByPlayer.Remove(userId);
            }
            if (visible)
            {
                var seen = _lastFadeStackSeenByPlayer.GetValueOrDefault(userId,
                    new KeyValuePair<int, short[]>(-1, null));
                if (seen.Key != _fadeStackVersion)
                {
                    // Clear whatever this client may still hold from before it lost vision —
                    // a Pop for an id the client doesn't have is a no-op.
                    if (seen.Value != null)
                    {
                        foreach (short id in seen.Value)
                        {
                            _game.PacketNotifier.NotifyS2C_SetFadeOut_Pop(this, id, userId);
                        }
                    }
                    // Replay the live stack in order: finished entries as instant pushes, a
                    // still-animating entry with its remaining time. An empty stack means the
                    // pops above already reverted the client to the 1.0 default.
                    var ids = new short[_fades.Count];
                    for (int i = 0; i < _fades.Count; i++)
                    {
                        Fade f = _fades[i];
                        float timeLeft = Math.Max(0, f.Duration - (_game.GameTime - f.StartTime));
                        _game.PacketNotifier.NotifyS2C_SetFadeOut_Push(this, (short)f.Id, timeLeft, f.Opacity, userId);
                        ids[i] = (short)f.Id;
                    }
                    _lastFadeStackSeenByPlayer[userId] = new KeyValuePair<int, short[]>(_fadeStackVersion, ids);
                }
            }
        }

        public virtual void OnAfterSync()
        {
        }

        public virtual void OnReconnect(int userId, TeamId team)
        {
            if(IsSpawnedForPlayer(userId))
            {
                Sync(userId, team, IsVisibleForPlayer(userId), true);
            }
        }

        /// <summary>
        /// Sets the object's team.
        /// </summary>
        /// <param name="team">TeamId.BLUE/PURPLE/NEUTRAL</param>
        public virtual void SetTeam(TeamId team)
        {
            _game.ObjectManager.RemoveVisionProvider(this, Team);
            Team = team;
            if (AutoProvidesVision)
            {
                _game.ObjectManager.AddVisionProvider(this, Team);
            }
            if (_game.IsRunning)
            {
                _game.PacketNotifier.NotifySetTeam(this as AttackableUnit);
            }
        }

        /// <summary>
        /// Whether or not the object is within vision of the specified team.
        /// </summary>
        /// <param name="team">A team which could have vision of this object.</param>
        public bool IsVisibleByTeam(TeamId team)
        {
            return !IsAffectedByFoW || _visibleByTeam[team];
        }

        /// <summary>
        /// Sets the object as visible to a specified team.
        /// Should be called in the ObjectManager. By itself, it only affects the return value of IsVisibleByTeam.
        /// </summary>
        /// <param name="team">A team which could have vision of this object.</param>
        /// <param name="visible">New value.</param>
        public void SetVisibleByTeam(TeamId team, bool visible = true)
        {
            _visibleByTeam[team] = visible;
        }

        /// <summary>
        /// Gets a list of all teams that have vision of this object.
        /// </summary>
        public List<TeamId> TeamsWithVision()
        {
            List<TeamId> toReturn = new List<TeamId>();
            foreach(var team in _visibleByTeam.Keys)
            {
                if (_visibleByTeam[team])
                {
                    toReturn.Add(team);
                }
            }
            return toReturn;
        }

        /// <summary>
        /// Whether or not the object is visible for the specified player.
        /// <summary>
        /// <param name="userId">The player in relation to which the value is obtained</param>
        public bool IsVisibleForPlayer(int userId)
        {
            return !IsAffectedByFoW || _visibleForPlayers.GetValueOrDefault(userId, false);
        }

        /// <summary>
        /// Sets the object as visible and or not to a specified player.
        /// Should be called in the ObjectManager. By itself, it only affects the return value of IsVisibleForPlayer.
        /// <summary>
        /// <param name="userId">The player for which the value is set.</param>
        /// <param name="visible">New value.</param>
        public void SetVisibleForPlayer(int userId, bool visible = true)
        {
            _visibleForPlayers[userId] = visible;
        }

        /// <summary>
        /// Whether or not the object is spawned on the player's client side.
        /// <summary>
        /// <param name="userId">The player in relation to which the value is obtained</param>
        public bool IsSpawnedForPlayer(int userId)
        {
            return _spawnedForPlayers.Contains(userId);
        }

        /// <summary>
        /// Sets the object as spawned on the player's client side.
        /// Should be called in the ObjectManager. By itself, it only affects the return value of IsSpawnedForPlayer.
        /// <summary>
        /// <param name="userId">The player for which the value is set.</param>
        public void SetSpawnedForPlayer(int userId)
        {
            _spawnedForPlayers.Add(userId);
        }

        /// <summary>
        /// Sets the position of this GameObject to the specified position.
        /// </summary>
        /// <param name="x">X coordinate to set.</param>
        /// <param name="y">Y coordinate to set.</param>
        public virtual void TeleportTo(float x, float y)
        {
            var position = _game.Map.NavigationGrid.GetClosestTerrainExit(new Vector2(x, y), PathfindingRadius + 1.0f);

            SetPosition(position);

            // TODO: Find a suitable function for this. Maybe modify NotifyWaypointGroup to accept simple objects.
            _game.PacketNotifier.NotifyEnterVisibilityClient(this);
            _movementUpdated = false;
        }

        /// <summary>
        /// Forces this GameObject to perform the given internally named animation.
        /// </summary>
        /// <param name="animName">Internal name of an animation to play.</param>
        /// <param name="scaleTime">How fast the animation should play. Default 1x speed.</param>
        /// <param name="startProgress">Time in the animation to start at.</param>
        /// TODO: Verify if this description is correct, if not, correct it.
        /// <param name="scaleSpeed">How much the speed of the GameObject should affect the animation.</param>
        /// <param name="flags">Animation flags. Refer to AnimationFlags enum.</param>
        public void PlayAnimation(string animName, float scaleTime = 1.0f, float startProgress = 0, float scaleSpeed = 0, AnimationFlags flags = 0)
        {
            _game.PacketNotifier.NotifyS2C_PlayAnimation(this, animName, flags, scaleTime, startProgress, scaleSpeed);
        }

        /// <summary>
        /// Forces this GameObject's current animations to pause/unpause.
        /// </summary>
        /// <param name="pause">Whether or not to pause/unpause animations.</param>
        public void PauseAnimation(bool pause)
        {
            _game.PacketNotifier.NotifyS2C_PauseAnimation(this, pause);
        }

        /// <summary>
        /// Forces this GameObject to stop playing the specified animation (or optionally all
        /// animations via <see cref="StopAnimationFlags.StopAll"/> + empty animation name).
        /// </summary>
        /// <param name="animation">Internal name of the animation to stop. Empty string + <c>StopAll</c> stops every track.</param>
        /// <param name="flags">Combination of <see cref="StopAnimationFlags"/>. Default <see cref="StopAnimationFlags.IgnoreLock"/> matches the prior bool-API default (`ignoreLock=true`).</param>
        public void StopAnimation(string animation, StopAnimationFlags flags = StopAnimationFlags.IgnoreLock)
        {
            _game.PacketNotifier.NotifyS2C_StopAnimation(this, animation, flags);
        }

        public float GetOpacity()
        {
            // Instant fades (Duration 0) would make the division below 0/0 = NaN on the
            // same-tick read — they are already at their target.
            if (_currentFade.Duration <= 0)
            {
                return _currentFade.Opacity;
            }
            float t = Math.Min(1, (_game.GameTime - _currentFade.StartTime) / _currentFade.Duration);
            return t * (_currentFade.Opacity - _currentFadeStartOpacity) + _currentFadeStartOpacity;
        }

        private void SetFade(float opacity, float duration)
        {
            _currentFadeStartOpacity = GetOpacity();
            _previousFade = _currentFade;
            _currentFade = new Fade(_nextFadeId++, _game.GameTime, duration, opacity);
        }

        public Fade FadeOut(float opacity, float duration)
        {
            duration *= 1000;

            SetFade(opacity, duration);
            _fades.Add(_currentFade);

            // Riot wire: one Push per fade instance, carrying its per-unit id — the client keeps
            // the stack itself. Players without vision catch up via the OnSync stack replay.
            _fadeStackVersion++;
            _game.PacketNotifier.NotifyS2C_SetFadeOut_Push(this, (short)_currentFade.Id, duration, opacity);
            return _currentFade;
        }

        public bool FadeIn(Fade fade, float duration = 1.0f)
        {
            duration *= 1000;

            if (!_fades.Remove(fade))
            {
                return false;
            }

            // Riot wire: the matching Pop by id. The client mirrors our logic exactly (revert to
            // the previous entry — or the 1.0 default — only when the popped entry was the top;
            // a mid-stack pop just removes the entry with no visual change).
            _fadeStackVersion++;
            _game.PacketNotifier.NotifyS2C_SetFadeOut_Pop(this, (short)fade.Id);

            if (fade == _currentFade)
            {
                float opacity = 1;
                if (_fades.Count > 0)
                {
                    Fade lastFade = _fades[_fades.Count - 1];
                    float lastFadeTimeLeft = lastFade.Duration - (_game.GameTime - lastFade.StartTime);

                    duration = Math.Max(duration, lastFadeTimeLeft);
                    opacity = lastFade.Opacity;
                }
                SetFade(opacity, duration);
            }
            return true;
        }
    }
}
