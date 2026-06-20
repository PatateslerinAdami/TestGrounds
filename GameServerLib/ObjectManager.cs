using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.NetInfo;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using log4net;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LeagueSandbox.GameServer
{
    // TODO: refactor this class

    /// <summary>
    /// Class that manages addition, removal, and updating of all GameObjects, their visibility, and buffs.
    /// </summary>
    public class ObjectManager
    {
        // Crucial Vars
        private Game _game;

        // Dictionaries of GameObjects.
        private Dictionary<uint, GameObject> _objects;
        private List<GameObject> _objectsToAdd = new List<GameObject>();

        private List<GameObject> _objectsToRemove = new List<GameObject>();

        // For the initial spawning (networking) of newly added objects.
        private Dictionary<uint, Champion> _champions;
        private Dictionary<uint, BaseTurret> _turrets;
        private Dictionary<uint, Inhibitor> _inhibitors;
        private Dictionary<uint, SpellMissile> _missiles;
        private FrozenDictionary<TeamId, HashSet<GameObject>> _visionProviders;

        // Per-team uniform spatial index of vision providers, rebuilt once per tick in
        // RebuildVisionProviderIndex. Replaces the O(objects × providers) full scan that
        // TeamHasVisionOn used to do (the #1 server hotspot at ~0.5 ms/tick). Allocation-free
        // across ticks: the bucket lists are cleared and refilled, never reallocated. A query
        // only visits providers in the cells overlapping the tested object's max-vision-radius
        // box; UnitHasVisionOn then does the exact distance + LOS test, so decisions are identical.
        private static readonly ILog _logger = LoggerProvider.GetLogger();
        private Dictionary<TeamId, List<GameObject>[]> _providerCells;
        private Dictionary<TeamId, float> _providerMaxRadius;
        private bool _providerIndexValid;
        private int _gridCols, _gridRows;
        private float _gridMinX, _gridMinY;
        private const float GRID_CELL_SIZE = 1000f;

        // General per-tick spatial index of ALL AttackableUnits (same grid dims as the provider
        // index), rebuilt once at the start of Update. Backs QueryUnitsInRange so per-tick mass
        // range queries (e.g. the turret/control-ward RevealStealth scan) cost O(local) instead of
        // GetUnitsInRange's O(N) full scan. Allocation-free across ticks (buckets cleared+refilled).
        private List<AttackableUnit>[] _unitGridCells;
        private bool _unitGridValid;
        // Flip to true to cross-check every QueryUnitsInRange against a full scan (count) and log.
        private const bool UNITGRID_PARALLEL_ASSERT = false;
        // Flip to true to cross-check every grid query against the legacy full scan and log any
        // divergence. Leave false in normal runs (the assert doubles vision work).
        private const bool VISION_PARALLEL_ASSERT = false;

        private bool _currentlyInUpdate = false;

        // Periodic full-sync of all unit positions. Real server replays show one bulk packet
        // every ~5s carrying every visible unit (24-26 entries), on top of the event-driven
        // small packets. Without this heartbeat, a unit that started a long path silently
        // drifts on the client until SetWaypoints/Stop/teleport/DriftResync fires.
        private float _timeSinceFullSync;
        private const float FULL_SYNC_INTERVAL_MS = 5000f;

        // Locks for each dictionary. Depricated since #1302.
        //private object _objectsLock = new object();
        //private object _turretsLock = new object();
        //private object _inhibitorsLock = new object();
        //private object _championsLock = new object();
        //private object _visionLock = new object();

        /// <summary>
        /// List of all possible teams in League of Legends. Normally there are only three.
        /// </summary>
        public List<TeamId> Teams { get; private set; }

        public bool IsServerFoWDisabled { get; set; } = false;

        /// <summary>
        /// Instantiates all GameObject Dictionaries in ObjectManager.
        /// </summary>
        /// <param name="game">Game instance.</param>
        public ObjectManager(Game game)
        {
            Teams = Enum.GetValues(typeof(TeamId)).Cast<TeamId>().ToList();

            _game = game;
            _objects = new Dictionary<uint, GameObject>();
            _turrets = new Dictionary<uint, BaseTurret>();
            _inhibitors = new Dictionary<uint, Inhibitor>();
            _champions = new Dictionary<uint, Champion>();
            _missiles = new Dictionary<uint, SpellMissile>();
            _visionProviders = Teams.ToDictionary(team => team, _ => new HashSet<GameObject>()).ToFrozenDictionary();
        }

        /// <summary>
        /// Function called every tick of the game.
        /// </summary>
        /// <param name="diff">Number of milliseconds since this tick occurred.</param>
        public void Update(float diff)
        {
            _currentlyInUpdate = true;
            // The provider index is rebuilt below in VisionAndLateUpdate. Any TeamHasVisionOn
            // call before that (e.g. out-of-band SpawnObject during scripts) falls back to the
            // legacy scan via this flag, so it never reads a stale index.
            _providerIndexValid = false;

            _timeSinceFullSync += diff;
            if (_timeSinceFullSync >= FULL_SYNC_INTERVAL_MS)
            {
                _timeSinceFullSync = 0f;
                foreach (var obj in _objects.Values)
                {
                    if (obj is AttackableUnit u)
                    {
                        u.RequestMovementSync();
                    }
                }
            }

            // Build the unit spatial index before the per-object update loop so range queries made
            // during it (e.g. Region.Update's RevealStealth scan) can use it. Positions are this
            // tick's start (= last tick's final); QueryUnitsInRange distance-checks LIVE positions
            // against a 1-cell-margin candidate set, so results stay exact despite mid-tick movement.
            using (Profiler.Scope("Objects.RebuildUnitGrid"))
            {
                RebuildUnitGrid();
            }

            // For all existing objects
            using (Profiler.Scope("Objects.Update"))
            {
                foreach (var obj in _objects.Values)
                {
                    using var _objScope = Profiler.Scope($"Update:{obj.GetType().Name}");
                    obj.Update(diff);
                }
            }

            int oldObjectsCount;
            using (Profiler.Scope("Objects.Cleanup"))
            {
                // It is now safe to call RemoveObject at any time,
                // but compatibility with the older remove method remains.
                foreach (var obj in _objects.Values)
                {
                    if (obj.IsToRemove())
                    {
                        RemoveObject(obj);
                    }
                }

                foreach (var obj in _objectsToRemove)
                {
                    _objects.Remove(obj.NetId);
                }

                _objectsToRemove.Clear();

                // Captured AFTER removes but BEFORE adds: LateUpdate further
                // down skips objects that didn't exist at the start of this tick.
                oldObjectsCount = _objects.Count;

                // Snapshot + clear BEFORE running any first-tick missile Update below: a
                // missile's first move can spawn further objects (chain bounces, script
                // sub-missiles) which re-enter AddObject and append to _objectsToAdd. Mutating
                // the list while iterating it threw "Collection was modified" (Jinx W crash).
                // Second-order spawns land in the now-empty list and are handled next tick.
                var justAdded = _objectsToAdd.ToArray();
                _objectsToAdd.Clear();

                foreach (var obj in justAdded)
                {
                    _objects.Add(obj.NetId, obj);
                }

                // Missiles spawned mid-update (windup-end ForceCreateMissile / spawn
                // replication) are sent to clients THIS tick, so the client starts
                // simulating them immediately. But the object update loop already ran,
                // so without this their first server-side Move would be next tick —
                // leaving the server missile a full tick (Jinx W: ~110u @30Hz) behind
                // the client visual, which reads as "the missile flies out faster than
                // the server" and lands the hit after it visually passed. Give them
                // their first move now to stay in lockstep with the client.
                foreach (var obj in justAdded)
                {
                    if (obj is SpellMissile spawnedMissile && !spawnedMissile.IsToRemove())
                    {
                        spawnedMissile.Update(diff);
                    }
                }
            }

            var players = _game.PlayerManager.GetPlayers(includeBots: false);

            using (Profiler.Scope("vision:RebuildIndex"))
            {
                RebuildVisionProviderIndex();
            }

            using (Profiler.Scope("Objects.VisionAndLateUpdate"))
            {
                int i = 0;
                foreach (GameObject obj in _objects.Values)
                {
                    // Phase split so a trace shows where the per-object cost goes:
                    // TeamsVision = team visibility recompute (loops vision providers, may raycast),
                    // LateUpdate  = obj.LateUpdate, SpawnAndSync = per-player sync/replication,
                    // OnAfterSync = post-sync bookkeeping. The raycast itself is scoped separately
                    // inside UnitHasVisionOn as "vision:raycast" so raycast cost can be subtracted
                    // from the provider-iteration + distance-cull cost (TeamsVision minus raycast).
                    using (Profiler.Scope("vision:TeamsVision"))
                    {
                        UpdateTeamsVision(obj);
                    }

                    if (i++ < oldObjectsCount)
                    {
                        using (Profiler.Scope("vision:LateUpdate"))
                        {
                            obj.LateUpdate(diff);
                        }
                    }

                    using (Profiler.Scope("vision:SpawnAndSync"))
                    {
                        foreach (var kv in players)
                        {
                            UpdateVisionSpawnAndSync(obj, kv);
                        }
                    }

                    using (Profiler.Scope("vision:OnAfterSync"))
                    {
                        obj.OnAfterSync();
                    }
                }
            }

            using (Profiler.Scope("PacketNotifier.NotifyOnReplication", "network"))
            {
                _game.PacketNotifier.NotifyOnReplication();
            }
            using (Profiler.Scope("PacketNotifier.NotifyWaypointGroup", "network"))
            {
                _game.PacketNotifier.NotifyWaypointGroup();
            }
            using (Profiler.Scope("PacketNotifier.NotifyFXCreateGroupBatch", "network"))
            {
                _game.PacketNotifier.NotifyFXCreateGroupBatch();
            }

            _currentlyInUpdate = false;
        }

        /// <summary>
        /// Normally, objects will spawn at the end of the frame, but calling this function will force the teams' and players' vision of that object to update and send out a spawn notification.
        /// </summary>
        /// <param name="obj">Object to spawn.</param>
        public void SpawnObject(GameObject obj)
        {
            UpdateTeamsVision(obj);

            var players = _game.PlayerManager.GetPlayers(includeBots: false);
            foreach (var kv in players)
            {
                UpdateVisionSpawnAndSync(obj, kv, forceSpawn: true);
            }

            obj.OnAfterSync();
        }

        public void OnReconnect(int userId, TeamId team)
        {
            // Soft-reconnect GC mark-and-sweep (Riot 4.17): mark all of the client's objects, re-replicate
            // the live world (the per-object spawns below refresh/un-mark live objects), then sweep —
            // destroy anything still marked = stale/ghost objects the client kept across the disconnect.
            // NotifySpawn is an immediate per-client send, so MARK -> spawns -> SWEEP stay FIFO-ordered.
            _game.PacketNotifier.NotifyS2C_MarkOrSweepForSoftReconnect(userId, SoftReconnectStage.MarkAllUnits);
            foreach (GameObject obj in _objects.Values)
            {
                obj.OnReconnect(userId, team);
            }
            _game.PacketNotifier.NotifyS2C_MarkOrSweepForSoftReconnect(userId, SoftReconnectStage.DestroyAllUnits);
        }

        public void SpawnObjects(ClientInfo clientInfo)
        {
            foreach (GameObject obj in _objects.Values)
            {
                UpdateVisionSpawnAndSync(obj, clientInfo, forceSpawn: true);
            }
        }

        /// <summary>
        /// Updates the vision of the teams on the object.
        /// </summary>
        void UpdateTeamsVision(GameObject obj)
        {
            foreach (var team in Teams)
            {
                obj.SetVisibleByTeam(team, IsServerFoWDisabled || !obj.IsAffectedByFoW || TeamHasVisionOn(team, obj));
            }
        }

        /// <summary>
        /// Updates the player's vision, which may not be tied to the team's vision, sends a spawn notification or updates if the object is already spawned.
        /// </summary>
        public void UpdateVisionSpawnAndSync(GameObject obj, ClientInfo clientInfo, bool forceSpawn = false)
        {
            int cid = clientInfo.ClientId;
            TeamId team = clientInfo.Team;
            Champion champion = clientInfo.Champion;

            bool shouldBeVisibleForPlayer;
            if (obj is Particle particle && particle.SpecificUnit != null)
            {
                shouldBeVisibleForPlayer = IsServerFoWDisabled
                    || particle.IsAudienceVisibleToRecipient(team, cid);
            }
            else
            {
                bool nearSighted = champion.Status.HasFlag(StatusFlags.NearSighted);
                shouldBeVisibleForPlayer = IsServerFoWDisabled || !obj.IsAffectedByFoW || (
                    nearSighted ? UnitHasVisionOn(champion, obj, nearSighted) : obj.IsVisibleByTeam(champion.Team)
                );
            }

            obj.Sync(cid, team, shouldBeVisibleForPlayer, forceSpawn);
        }

        /// <summary>
        /// Adds a GameObject to the dictionary of GameObjects in ObjectManager.
        /// </summary>
        /// <param name="o">GameObject to add.</param>
        public void AddObject(GameObject o)
        {
            if (o != null)
            {
                _objectsToRemove.Remove(o);

                if (_currentlyInUpdate)
                {
                    _objectsToAdd.Add(o);
                }
                else
                {
                    _objects.Add(o.NetId, o);
                }

                if (o is SpellMissile missile)
                {
                    _missiles.Add(missile.NetId, missile);
                }

                // TODO: This is a hack-fix for units which have packets being sent before spawning (ex: AscWarp minion)
                // Instead, we need a dedicated packet queue system which takes all packets which are not vision/spawn related,
                // and queues them if the object is not spawned yet for clients.
                if (!(o is Champion))
                {
                    SpawnObject(o);
                }

                o.OnAdded();
            }
        }

        /// <summary>
        /// Removes a GameObject from the dictionary of GameObjects in ObjectManager.
        /// </summary>
        /// <param name="o">GameObject to remove.</param>
        public void RemoveObject(GameObject o)
        {
            if (o != null)
            {
                _objectsToAdd.Remove(o);

                if (_currentlyInUpdate)
                {
                    _objectsToRemove.Add(o);
                }
                else
                {
                    _objects.Remove(o.NetId);
                }

                if (o is SpellMissile missile)
                {
                    _missiles.Remove(missile.NetId);
                }

                o.OnRemoved();
            }
        }

        /// <summary>
        /// Gets a new Dictionary of all NetID,GameObject pairs present in the dictionary of objects in ObjectManager.
        /// </summary>
        /// <returns>Dictionary of NetIDs and the GameObjects that they refer to.</returns>
        public Dictionary<uint, GameObject> GetObjects()
        {
            var ret = new Dictionary<uint, GameObject>();
            foreach (var obj in _objects)
            {
                ret.Add(obj.Key, obj.Value);
            }

            return ret;
        }

        /// <summary>
        /// Gets a GameObject from the list of objects in ObjectManager that is identified by the specified NetID.
        /// </summary>
        /// <param name="id">NetID to check.</param>
        /// <returns>GameObject instance that has the specified NetID. Null otherwise.</returns>
        public GameObject GetObjectById(uint id)
        {
            GameObject obj = _objectsToAdd.Find(o => o.NetId == id);

            if (obj == null)
            {
                obj = _objects.GetValueOrDefault(id, null);
            }

            return obj;
        }

        /// <summary>
        /// Whether or not a specified GameObject is being networked to the specified team.
        /// </summary>
        /// <param name="team">TeamId.BLUE/PURPLE/NEUTRAL</param>
        /// <param name="o">GameObject to check.</param>
        /// <returns>true/false; networked or not.</returns>
        public bool TeamHasVisionOn(TeamId team, GameObject o)
        {
            if (o == null)
            {
                return false;
            }

            if (!o.IsAffectedByFoW)
            {
                return true;
            }

            // Globally-revealed units (e.g. Jinx W's RevealSpecificUnit) are visible regardless of
            // which vision providers happen to be nearby. This test depends only on the tested unit,
            // not on any provider, so it must live here — the grid path only visits spatially-near
            // providers and would otherwise miss a revealed unit with no provider in range (e.g. a
            // long-range Jinx W hit far from the caster). UnitHasVisionOn keeps the same check for
            // the direct (nearSighted) call path.
            if (o is AttackableUnit revealedUnit
                && revealedUnit.Status.HasFlag(StatusFlags.RevealSpecificUnit))
            {
                return true;
            }

            bool useGrid = _providerIndexValid && _providerCells != null;
            bool result = useGrid ? TeamHasVisionOnGrid(team, o) : TeamHasVisionOnLegacy(team, o);

            if (VISION_PARALLEL_ASSERT && useGrid)
            {
                bool legacy = TeamHasVisionOnLegacy(team, o);
                if (legacy != result)
                {
                    _logger.Warn($"[VISION-ASSERT] grid={result} legacy={legacy} team={team} netId={o.NetId} type={o.GetType().Name}");
                }
            }

            return result;
        }

        /// <summary>
        /// Spatial-index path: only visits providers in the grid cells overlapping the tested
        /// object's max-vision-radius box. The cell box is a superset of every provider within
        /// maxRadius (a provider can only see <paramref name="o"/> if it is within its own
        /// VisionRadius, which is &lt;= maxRadius), so UnitHasVisionOn still makes the exact call.
        /// </summary>
        bool TeamHasVisionOnGrid(TeamId team, GameObject o)
        {
            float maxR = _providerMaxRadius[team];
            if (maxR <= 0f)
            {
                return false;
            }

            var cells = _providerCells[team];
            Vector2 pos = o.Position;
            int c0 = ColOf(pos.X - maxR), c1 = ColOf(pos.X + maxR);
            int r0 = RowOf(pos.Y - maxR), r1 = RowOf(pos.Y + maxR);

            for (int r = r0; r <= r1; r++)
            {
                int rowBase = r * _gridCols;
                for (int c = c0; c <= c1; c++)
                {
                    var bucket = cells[rowBase + c];
                    for (int k = 0; k < bucket.Count; k++)
                    {
                        var p = bucket[k];
                        if (!IsViableVisionProvider(p, team))
                        {
                            continue;
                        }

                        if (UnitHasVisionOn(p, o))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Original full-scan path. Kept as the correctness reference for VISION_PARALLEL_ASSERT
        /// and as the fallback used whenever the per-tick index is not yet valid.
        /// </summary>
        bool TeamHasVisionOnLegacy(TeamId team, GameObject o)
        {
            foreach (var p in _visionProviders[team])
            {
                if (!IsViableVisionProvider(p, team))
                {
                    continue;
                }

                if (UnitHasVisionOn(p, o))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Per-provider gating shared by the grid and legacy paths so they make identical
        /// decisions. Does NOT include the distance / line-of-sight test — that stays in
        /// <see cref="UnitHasVisionOn"/>.
        /// </summary>
        static bool IsViableVisionProvider(GameObject p, TeamId team)
        {
            if (p == null || p.IsToRemove())
            {
                return false;
            }

            // Dead units should never provide vision.
            if (p is AttackableUnit observerUnit && observerUnit.IsDead)
            {
                return false;
            }

            // Regions bound to dead units should not provide vision either.
            if (p is Region providerRegion
                && providerRegion.CollisionUnit is AttackableUnit regionOwner
                && regionOwner.IsDead)
            {
                return false;
            }

            // Enemy turrets should not provide vision for your team.
            if (p is BaseTurret && p.Team != team)
            {
                return false;
            }

            // Enemy lane minions should not provide vision for your team.
            if (p is Minion laneMinion
                && laneMinion.Team != team
                && laneMinion.Team != TeamId.TEAM_NEUTRAL)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Lazily sizes the per-team provider grid from the map bounds (the NavigationGrid is
        /// loaded by the time the first tick runs).
        /// </summary>
        void EnsureVisionProviderIndex()
        {
            if (_providerCells != null)
            {
                return;
            }

            var nav = _game.Map.NavigationGrid;
            _gridMinX = nav.MinGridPosition.X;
            _gridMinY = nav.MinGridPosition.Z;
            _gridCols = Math.Max(1, (int)Math.Ceiling(nav.MapWidth / GRID_CELL_SIZE));
            _gridRows = Math.Max(1, (int)Math.Ceiling(nav.MapHeight / GRID_CELL_SIZE));

            _providerCells = new Dictionary<TeamId, List<GameObject>[]>();
            _providerMaxRadius = new Dictionary<TeamId, float>();
            foreach (var team in Teams)
            {
                var cells = new List<GameObject>[_gridCols * _gridRows];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = new List<GameObject>();
                }

                _providerCells[team] = cells;
                _providerMaxRadius[team] = 0f;
            }
        }

        int ColOf(float x) => Math.Clamp((int)((x - _gridMinX) / GRID_CELL_SIZE), 0, _gridCols - 1);
        int RowOf(float y) => Math.Clamp((int)((y - _gridMinY) / GRID_CELL_SIZE), 0, _gridRows - 1);

        /// <summary>
        /// Rebuilds the per-team provider index for this tick. Clears and refills the bucket
        /// lists (no reallocation) and recomputes the per-team max vision radius used to size
        /// queries. Cheap: O(total providers) ~ a few hundred inserts per tick.
        /// </summary>
        void RebuildVisionProviderIndex()
        {
            EnsureVisionProviderIndex();

            foreach (var team in Teams)
            {
                var cells = _providerCells[team];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i].Clear();
                }

                float maxR = 0f;
                foreach (var p in _visionProviders[team])
                {
                    if (p == null)
                    {
                        continue;
                    }

                    Vector2 pos = p.Position;
                    cells[RowOf(pos.Y) * _gridCols + ColOf(pos.X)].Add(p);
                    // Effective (scaled) radius so the provider-grid query box still covers a unit
                    // whose vision radius is buff-extended (GetEffectiveVisionRadius); == VisionRadius
                    // when no vision-scale is applied.
                    float effR = p.GetEffectiveVisionRadius();
                    if (effR > maxR)
                    {
                        maxR = effR;
                    }
                }

                _providerMaxRadius[team] = maxR;
            }

            _providerIndexValid = true;
        }

        /// <summary>
        /// Rebuilds the per-tick spatial index of all AttackableUnits (shares the provider grid's
        /// cell dimensions). Cheap: O(units), buckets cleared and refilled (no reallocation).
        /// </summary>
        void RebuildUnitGrid()
        {
            EnsureVisionProviderIndex(); // sizes the shared grid dims from the map

            if (_unitGridCells == null)
            {
                _unitGridCells = new List<AttackableUnit>[_gridCols * _gridRows];
                for (int i = 0; i < _unitGridCells.Length; i++)
                {
                    _unitGridCells[i] = new List<AttackableUnit>();
                }
            }

            for (int i = 0; i < _unitGridCells.Length; i++)
            {
                _unitGridCells[i].Clear();
            }

            foreach (var obj in _objects.Values)
            {
                if (obj is AttackableUnit u)
                {
                    Vector2 pos = u.Position;
                    _unitGridCells[RowOf(pos.Y) * _gridCols + ColOf(pos.X)].Add(u);
                }
            }

            _unitGridValid = true;
        }

        /// <summary>
        /// Spatial-index-backed range query: fills <paramref name="result"/> with AttackableUnits
        /// within <paramref name="range"/> of <paramref name="checkPos"/>. EXACT vs the full-scan
        /// GetUnitsInRange — the grid only narrows candidates (built-time cell), and each candidate's
        /// LIVE position is distance-checked. The cell range is widened by 1 cell so per-tick
        /// movement (~16u, cell=1000) can't push an in-range unit out of the queried block. Use this
        /// for per-tick mass queries; one-off/cast-time callers can keep GetUnitsInRange.
        /// </summary>
        public void QueryUnitsInRange(Vector2 checkPos, float range, bool onlyAlive, List<AttackableUnit> result)
        {
            result.Clear();
            float r2 = range * range;

            if (!_unitGridValid || _unitGridCells == null)
            {
                // Index not built yet this tick (rare/out-of-band) — fall back to a full scan.
                foreach (var kv in _objects)
                {
                    if (kv.Value is AttackableUnit u
                        && (!onlyAlive || !u.IsDead)
                        && Vector2.DistanceSquared(checkPos, u.Position) <= r2)
                    {
                        result.Add(u);
                    }
                }
                return;
            }

            int c0 = Math.Max(0, ColOf(checkPos.X - range) - 1);
            int c1 = Math.Min(_gridCols - 1, ColOf(checkPos.X + range) + 1);
            int r0 = Math.Max(0, RowOf(checkPos.Y - range) - 1);
            int r1 = Math.Min(_gridRows - 1, RowOf(checkPos.Y + range) + 1);

            for (int r = r0; r <= r1; r++)
            {
                int rowBase = r * _gridCols;
                for (int c = c0; c <= c1; c++)
                {
                    var bucket = _unitGridCells[rowBase + c];
                    for (int k = 0; k < bucket.Count; k++)
                    {
                        var u = bucket[k];
                        if (onlyAlive && u.IsDead)
                        {
                            continue;
                        }

                        if (Vector2.DistanceSquared(checkPos, u.Position) <= r2)
                        {
                            result.Add(u);
                        }
                    }
                }
            }

            if (UNITGRID_PARALLEL_ASSERT)
            {
                int scan = 0;
                foreach (var kv in _objects)
                {
                    if (kv.Value is AttackableUnit u
                        && (!onlyAlive || !u.IsDead)
                        && Vector2.DistanceSquared(checkPos, u.Position) <= r2)
                    {
                        scan++;
                    }
                }

                if (scan != result.Count)
                {
                    _logger.Warn($"[UNITGRID-ASSERT] grid={result.Count} scan={scan} pos={checkPos} range={range}");
                }
            }
        }

        bool UnitHasVisionOn(GameObject observer, GameObject tested, bool nearSighted = false)
        {
            if (!tested.IsAffectedByFoW)
            {
                return true;
            }

            if (observer == null || observer.IsToRemove())
            {
                return false;
            }

            if (observer is AttackableUnit observerUnit && observerUnit.IsDead)
            {
                return false;
            }

            if (observer is Region observerRegion
                && observerRegion.CollisionUnit is AttackableUnit regionOwner
                && regionOwner.IsDead)
            {
                return false;
            }

            if (tested is AttackableUnit testedUnit
                && testedUnit.Status.HasFlag(StatusFlags.RevealSpecificUnit))
            {
                return true;
            }

            if (tested is AttackableUnit stealthedUnit
                && stealthedUnit.Status.HasFlag(StatusFlags.Stealthed)
                && !stealthedUnit.Status.HasFlag(StatusFlags.RevealSpecificUnit)
                && stealthedUnit.Team != observer.Team)
            {
                return false;
            }

            if (observer is Region regionObserver
                && regionObserver.OnlyShowTarget
                && regionObserver.VisionTarget != null
                && regionObserver.VisionTarget != tested)
            {
                return false;
            }

            if (tested is Particle particle)
            {
                if (!particle.IsAudienceVisibleToTeam(observer.Team))
                {
                    return false;
                }

                if (particle.ShouldAutoRevealForObserverTeam(observer.Team))
                {
                    return true;
                }
            }

            if (tested.Team == observer.Team && !nearSighted)
            {
                return true;
            }

            // Effective vision radius (S4 CircleRegion::GetActualRadius = base * mult + add via
            // GetVisionScale) so vision-range buffs/items extend sight; == VisionRadius by default.
            float visionR = observer.GetEffectiveVisionRadius();
            if (Vector2.DistanceSquared(observer.Position, tested.Position) >= visionR * visionR)
            {
                return false;
            }

            if (observer is Region region && region.IgnoresLineOfSight)
            {
                return true;
            }

            bool isSelfCheck = observer is Region r && r.VisionTarget == tested;
            if (isSelfCheck)
            {
                return true;
            }

            // Scoped on its own: this LOS raycast is the suspected vision hotspot. In a trace,
            // sum("vision:raycast") = total time in LOS raycasts; the parent vision scope minus
            // this is the provider-iteration + distance-cull overhead. Constant name (no string
            // interpolation) so the per-call cost stays negligible even at hundreds of calls/tick.
            using (Profiler.Scope("vision:raycast"))
            {
                return !_game.Map.NavigationGrid.IsAnythingBetween(observer, tested, true);
            }
        }

        /// <summary>
        /// Adds a GameObject to the list of Vision Providers in ObjectManager.
        /// </summary>
        /// <param name="obj">GameObject to add.</param>
        /// <param name="team">The team that GameObject can provide vision to.</param>
        public void AddVisionProvider(GameObject obj, TeamId team)
        {
            //lock (_visionLock)
            {
                _visionProviders[team].Add(obj);
            }
        }

        /// <summary>
        /// Removes a GameObject from the list of Vision Providers in ObjectManager.
        /// </summary>
        /// <param name="obj">GameObject to remove.</param>
        /// <param name="team">The team that GameObject provided vision to.</param>
        public void RemoveVisionProvider(GameObject obj, TeamId team)
        {
            //lock (_visionLock)
            {
                _visionProviders[team].Remove(obj);
            }
        }

        /// <summary>
        /// Gets a list of all GameObjects of type AttackableUnit that are within a certain distance from a specified position.
        /// </summary>
        /// <param name="checkPos">Vector2 position to check.</param>
        /// <param name="range">Distance to check.</param>
        /// <param name="onlyAlive">Whether dead units should be excluded or not.</param>
        /// <returns>List of all AttackableUnits within the specified range and of the specified alive status.</returns>
        public List<AttackableUnit> GetUnitsInRange(Vector2 checkPos, float range, bool onlyAlive = false)
        {
            var units = new List<AttackableUnit>();
            foreach (var kv in _objects)
            {
                if (kv.Value is AttackableUnit u && Vector2.DistanceSquared(checkPos, u.Position) <= range * range &&
                    (onlyAlive && !u.IsDead || !onlyAlive))
                {
                    units.Add(u);
                }
            }

            return units;
        }

        /// <summary>
        /// Counts the number of units attacking a specified GameObject of type AttackableUnit.
        /// </summary>
        /// <param name="target">AttackableUnit potentially being attacked.</param>
        /// <returns>Number of units attacking target.</returns>
        public int CountUnitsAttackingUnit(AttackableUnit target)
        {
            return GetObjects().Count(x =>
                x.Value is ObjAIBase aiBase &&
                aiBase.Team == target.Team.GetEnemyTeam() &&
                !aiBase.IsDead &&
                aiBase.TargetUnit != null &&
                aiBase.TargetUnit == target
            );
        }

        /// <summary>
        /// Forces all GameObjects of type ObjAIBase to stop targeting the specified AttackableUnit.
        /// </summary>
        /// <param name="target">AttackableUnit that should be untargeted.</param>
        public void StopTargeting(AttackableUnit target)
        {
            foreach (var kv in _objects)
            {
                var u = kv.Value as AttackableUnit;
                if (u == null)
                {
                    continue;
                }

                var ai = u as ObjAIBase;
                if (ai != null)
                {
                    ai.Untarget(target);
                }
            }
        }

        /// <summary>
        /// Adds a GameObject of type BaseTurret to the list of BaseTurrets in ObjectManager.
        /// </summary>
        /// <param name="turret">BaseTurret to add.</param>
        public void AddTurret(BaseTurret turret)
        {
            _turrets.Add(turret.NetId, turret);
        }

        /// <summary>
        /// Gets a GameObject of type BaseTurret from the list of BaseTurrets in ObjectManager who is identified by the specified NetID.
        /// Unused.
        /// </summary>
        /// <param name="netId"></param>
        /// <returns>BaseTurret instance identified by the specified NetID.</returns>
        public BaseTurret GetTurretById(uint netId)
        {
            if (!_turrets.ContainsKey(netId))
            {
                return null;
            }

            return _turrets[netId];
        }

        /// <summary>
        /// Removes a GameObject of type BaseTurret from the list of BaseTurrets in ObjectManager.
        /// Unused.
        /// </summary>
        /// <param name="turret">BaseTurret to remove.</param>
        public void RemoveTurret(BaseTurret turret)
        {
            _turrets.Remove(turret.NetId);
        }

        /// <summary>
        /// How many turrets of a specified team are destroyed in the specified lane.
        /// Used for building protection, specifically for cases where new turrets are added after map turrets.
        /// Unused.
        /// </summary>
        /// <param name="team">Team of the BaseTurrets to check.</param>
        /// <param name="lane">Lane to check.</param>
        /// <returns>Number of turrets in the lane destroyed.</returns>
        /// TODO: Implement AzirTurrets so this can be used.
        public int GetTurretsDestroyedForTeam(TeamId team, Lane lane)
        {
            int destroyed = 0;
            foreach (var turret in _turrets.Values)
            {
                if (turret.Team == team && turret.Lane == lane && turret.IsDead)
                {
                    destroyed++;
                }
            }

            return destroyed;
        }

        /// <summary>
        /// Adds a GameObject of type Inhibitor to the list of Inhibitors in ObjectManager.
        /// </summary>
        /// <param name="inhib">Inhibitor to add.</param>
        public void AddInhibitor(Inhibitor inhib)
        {
            _inhibitors.Add(inhib.NetId, inhib);
        }

        /// <summary>
        /// Gets a GameObject of type Inhibitor from the list of Inhibitors in ObjectManager who is identified by the specified NetID.
        /// </summary>
        /// <param name="netId"></param>
        /// <returns>Inhibitor instance identified by the specified NetID.</returns>
        public Inhibitor GetInhibitorById(uint id)
        {
            if (!_inhibitors.ContainsKey(id))
            {
                return null;
            }

            return _inhibitors[id];
        }

        /// <summary>
        /// Removes a GameObject of type Inhibitor from the list of Inhibitors in ObjectManager.
        /// </summary>
        /// <param name="inhib">Inhibitor to remove.</param>
        public void RemoveInhibitor(Inhibitor inhib)
        {
            _inhibitors.Remove(inhib.NetId);
        }

        /// <summary>
        /// Whether or not all of the Inhibitors of a specified team are destroyed.
        /// </summary>
        /// <param name="team">Team of the Inhibitors to check.</param>
        /// <returns>true/false; destroyed or not</returns>
        public bool AllInhibitorsDestroyedFromTeam(TeamId team)
        {
            foreach (var inhibitor in _inhibitors.Values)
            {
                if (inhibitor.Team == team && inhibitor.InhibitorState == DampenerState.RespawningState)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Adds a GameObject of type Champion to the list of Champions in ObjectManager.
        /// </summary>
        /// <param name="champion">Champion to add.</param>
        public void AddChampion(Champion champion)
        {
            _champions.Add(champion.NetId, champion);
        }

        /// <summary>
        /// Removes a GameObject of type Champion from the list of Champions in ObjectManager.
        /// </summary>
        /// <param name="champion">Champion to remove.</param>
        public void RemoveChampion(Champion champion)
        {
            _champions.Remove(champion.NetId);
        }

        /// <summary>
        /// Gets a new list of all Champions found in the list of Champions in ObjectManager.
        /// </summary>
        /// <returns>List of all valid Champions.</returns>
        public List<Champion> GetAllChampions()
        {
            var champs = new List<Champion>();
            foreach (var kv in _champions)
            {
                var c = kv.Value;
                if (c != null)
                {
                    champs.Add(c);
                }
            }

            return champs;
        }

        /// <summary>
        /// Gets a new list of all Champions of the specified team found in the list of Champios in ObjectManager.
        /// </summary>
        /// <param name="team">TeamId.BLUE/PURPLE/NEUTRAL</param>
        /// <returns>List of valid Champions of the specified team.</returns>
        public List<Champion> GetAllChampionsFromTeam(TeamId team)
        {
            var champs = new List<Champion>();
            foreach (var kv in _champions)
            {
                var c = kv.Value;
                if (c.Team == team)
                {
                    champs.Add(c);
                }
            }

            return champs;
        }

        /// <summary>
        /// Gets a list of all GameObjects of type Champion that are within a certain distance from a specified position.
        /// </summary>
        /// <param name="checkPos">Vector2 position to check.</param>
        /// <param name="range">Distance to check.</param>
        /// <param name="onlyAlive">Whether dead Champions should be excluded or not.</param>
        /// <returns>List of all Champions within the specified range of the position and of the specified alive status.</returns>
        public List<Champion> GetChampionsInRange(Vector2 checkPos, float range, bool onlyAlive = false)
        {
            var champs = new List<Champion>();
            foreach (var kv in _champions)
            {
                var c = kv.Value;
                if (Vector2.DistanceSquared(checkPos, c.Position) <= range * range)
                    if (onlyAlive && !c.IsDead || !onlyAlive)
                        champs.Add(c);
            }

            return champs;
        }

        /// <summary>
        /// Gets a distance-sorted list of all GameObjects of type Champion that are within
        /// a certain distance from a specified position.
        /// </summary>
        /// <param name="checkPos">Vector2 position to check.</param>
        /// <param name="range">Distance to check.</param>
        /// <param name="onlyAlive">Whether dead Champions should be excluded or not.</param>
        /// <returns>Distance-sorted list of Champions within the specified range.</returns>
        public List<Champion> GetChampionsInRangeSorted(Vector2 checkPos, float range, bool onlyAlive = false)
        {
            return GetChampionsInRange(checkPos, range, onlyAlive)
                .OrderBy(champion => Vector2.DistanceSquared(checkPos, champion.Position))
                .ToList();
        }

        /// <summary>
        /// Gets a list of all GameObjects of type Champion that are within a certain distance from a specified position.
        /// </summary>
        /// <param name="checkPos">Vector2 position to check.</param>
        /// <param name="range">Distance to check.</param>
        /// <param name="onlyAlive">Whether dead Champions should be excluded or not.</param>
        /// <returns>List of all Champions within the specified range of the position and of the specified alive status.</returns>
        public List<Champion> GetChampionsInRangeFromTeam(Vector2 checkPos, float range, TeamId team,
            bool onlyAlive = false)
        {
            var champs = new List<Champion>();
            foreach (var kv in _champions)
            {
                var c = kv.Value;
                if (Vector2.DistanceSquared(checkPos, c.Position) <= range * range)
                    if (c.Team == team && (onlyAlive && !c.IsDead || !onlyAlive))
                        champs.Add(c);
            }

            return champs;
        }

        /// <summary>
        /// Gets a distance-sorted list of all GameObjects of type Champion from a specific team
        /// that are within a certain distance from a specified position.
        /// </summary>
        /// <param name="checkPos">Vector2 position to check.</param>
        /// <param name="range">Distance to check.</param>
        /// <param name="team">Team to filter by.</param>
        /// <param name="onlyAlive">Whether dead Champions should be excluded or not.</param>
        /// <returns>Distance-sorted list of Champions within the specified range and team.</returns>
        public List<Champion> GetChampionsInRangeFromTeamSorted(
            Vector2 checkPos,
            float range,
            TeamId team,
            bool onlyAlive = false
        )
        {
            return GetChampionsInRangeFromTeam(checkPos, range, team, onlyAlive)
                .OrderBy(champion => Vector2.DistanceSquared(checkPos, champion.Position))
                .ToList();
        }

        public bool CheckChampionsInRangeFromTeam(Vector2 checkPos, float range, TeamId team, bool onlyAlive = false)
        {
            foreach (var kv in _champions)
            {
                var c = kv.Value;
                if (Vector2.DistanceSquared(checkPos, c.Position) <= range * range)
                    if (c.Team == team && (onlyAlive && !c.IsDead || !onlyAlive))
                        return true;
            }

            return false;
        }

        /// <summary>
        /// Forces a vision update for a specific object immediately. 
        /// </summary>
        public void RefreshUnitVision(GameObject obj)
        {
            UpdateTeamsVision(obj);
            var players = _game.PlayerManager.GetPlayers(includeBots: false);
            foreach (var kv in players)
            {
                UpdateVisionSpawnAndSync(obj, kv, forceSpawn: false);
            }
        }

        /// <summary>
        /// Gets a new list of all active SpellMissiles in the game.
        /// </summary>
        public List<SpellMissile> GetAllMissiles()
        {
            return _missiles.Values.ToList();
        }
    }
}
