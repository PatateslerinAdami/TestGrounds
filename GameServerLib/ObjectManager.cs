using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.NetInfo;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LeagueSandbox.GameServer
{
    /// <summary>
    /// Class that manages addition, removal, and updating of all GameObjects, their visibility, and buffs.
    /// </summary>
    public class ObjectManager
    {
        // Crucial Vars
        private Game _game;

        // All GameObjects, keyed by NetId. Uses the Riot-faithful ManagerTemplate (paired hash-map +
        // insertion-ORDERED list) so iteration order is deterministic and add dedups by id instead of
        // throwing on a duplicate NetId (mirrors Riot's push_back_unique). See ManagerTemplate<T>.
        private ManagerTemplate<GameObject> _objects;
        private List<GameObject> _objectsToAdd = new List<GameObject>();

        private List<GameObject> _objectsToRemove = new List<GameObject>();

        // Objects eligible for the per-tick Update() pass (Riot's m_ObjectsToUpdate). A strict subset of
        // _objects: everything except those that opt out via GameObject.ReceivesUpdate = false (static
        // objects with a no-op Update — shops, level props). Membership is maintained in lockstep with
        // _objects (same add/remove points + deferral), so the Update loop iterates a stable set.
        private ManagerTemplate<GameObject> _objectsToUpdate;

        // Per-type registries (Riot's per-type ManagerTemplate model: AIHero/obj_AI_Turret/obj_Building/
        // SpellMissile ... each a paired hash-map + insertion-ordered list). Used for the initial spawning
        // (networking) of newly added objects and for typed lookups/iteration without scanning _objects.
        private ManagerTemplate<Champion> _champions;
        private ManagerTemplate<BaseTurret> _turrets;
        private ManagerTemplate<Inhibitor> _inhibitors;
        private ManagerTemplate<SpellMissile> _missiles;
        // All AttackableUnits (champions/minions/turrets/buildings) — the subset of _objects that every
        // range/targeting scan actually cares about. Mirrors Riot's AttackableUnit ManagerTemplate and lets
        // GetUnitsInRange / RebuildUnitGrid / StopTargeting / CountUnitsAttackingUnit iterate units only
        // (no particles/regions/missiles), instead of filtering the full _objects set. Routed centrally in
        // AddObject/RemoveObject.
        private ManagerTemplate<AttackableUnit> _attackableUnits;
        // All Minions (lane minions, pets, ...) — mirrors Riot's obj_AI_Minion manager. A Minion is also
        // in _attackableUnits (it's an ObjAIBase); this registry lets the recurring minion queries
        // (GetAllMinions / CountAllLaneMinions, called from the level script's MAX_MINIONS throttle)
        // iterate minions only instead of copying + scanning the whole _objects set.
        private ManagerTemplate<Minion> _minions;
        // All Markers — mirrors Riot's obj_AI_Marker manager. A Marker is a bare GameObject (NOT an
        // AttackableUnit), so it lives only here + _objects. No hot consumer scans markers yet; this
        // registry is kept for Riot structural fidelity and gives GetAllMarkers() O(1) typed iteration
        // for whenever marker logic (e.g. Azir soldiers) needs it.
        private ManagerTemplate<Marker> _markers;
        // All LevelProps — mirrors Riot's ManagedLevelProp manager. A LevelProp is a bare GameObject
        // (NOT an AttackableUnit). No hot consumer scans props yet (only per-object collision-exclusion
        // checks in CollisionHandler); kept for Riot structural fidelity + GetAllLevelProps().
        private ManagerTemplate<LevelProp> _props;
        // All ShopObjects — mirrors Riot's obj_Shop manager. A ShopObject is a bare GameObject (NOT an
        // AttackableUnit — a shop is never a valid attack target). Fidelity registry + GetAllShops().
        private ManagerTemplate<ShopObject> _shops;
        // All FollowerObjects (master-attached, master-following objects; Riot's FollowerObject
        // manager). A FollowerObject is an ObjAIBase, so it is ALSO in _attackableUnits — but it's
        // untargetable (IsTargetableByUnit=false), so range/targeting scans skip it. e.g. SyndraOrbs.
        private ManagerTemplate<FollowerObject> _followers;

        // Fog-of-War / vision authority + the per-tick spatial grids (provider grid for vision, unit
        // grid for range queries). Extracted from this class so ObjectManager is just the object
        // registry; ObjectManager keeps the vision-to-object/networking orchestration + public facades.
        private VisionManager _vision;

        private bool _currentlyInUpdate = false;

        // Periodic full-sync of all unit positions. Real server replays show one bulk packet
        // every ~5s carrying every visible unit (24-26 entries), on top of the event-driven
        // small packets. Without this heartbeat, a unit that started a long path silently
        // drifts on the client until SetWaypoints/Stop/teleport/DriftResync fires.
        private float _timeSinceFullSync;
        private const float FULL_SYNC_INTERVAL_MS = 5000f;

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
            _objects = new ManagerTemplate<GameObject>();
            _objectsToUpdate = new ManagerTemplate<GameObject>();
            _turrets = new ManagerTemplate<BaseTurret>();
            _inhibitors = new ManagerTemplate<Inhibitor>();
            _champions = new ManagerTemplate<Champion>();
            _missiles = new ManagerTemplate<SpellMissile>();
            _attackableUnits = new ManagerTemplate<AttackableUnit>();
            _minions = new ManagerTemplate<Minion>();
            _markers = new ManagerTemplate<Marker>();
            _props = new ManagerTemplate<LevelProp>();
            _shops = new ManagerTemplate<ShopObject>();
            _followers = new ManagerTemplate<FollowerObject>();
            _vision = new VisionManager(_game, Teams, _attackableUnits);
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
            _vision.InvalidateProviderIndex();

            _timeSinceFullSync += diff;
            if (_timeSinceFullSync >= FULL_SYNC_INTERVAL_MS)
            {
                _timeSinceFullSync = 0f;
                foreach (var obj in _objects)
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
                _vision.RebuildUnitGrid();
            }

            // Per-tick Update pass over the update-eligible set only (Riot's m_ObjectsToUpdate); static
            // objects (ReceivesUpdate=false) are excluded. Vision/LateUpdate/sync below still cover ALL
            // _objects.
            using (Profiler.Scope("Objects.Update"))
            {
                foreach (var obj in _objectsToUpdate)
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
                foreach (var obj in _objects)
                {
                    if (obj.IsToRemove())
                    {
                        RemoveObject(obj);
                    }
                }

                foreach (var obj in _objectsToRemove)
                {
                    _objects.Erase(obj, obj.NetId);
                    _objectsToUpdate.Erase(obj, obj.NetId); // no-op if it had opted out
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
                    _objects.PushBackUnique(obj, obj.NetId);
                    if (obj.ReceivesUpdate)
                    {
                        _objectsToUpdate.PushBackUnique(obj, obj.NetId);
                    }
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
                _vision.RebuildVisionProviderIndex();
            }

            using (Profiler.Scope("Objects.VisionAndLateUpdate"))
            {
                int i = 0;
                foreach (GameObject obj in _objects)
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
            using (Profiler.Scope("PacketNotifier.NotifyWaypointGroupWithSpeed", "network"))
            {
                _game.PacketNotifier.NotifyWaypointGroupWithSpeed();
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
            foreach (GameObject obj in _objects)
            {
                obj.OnReconnect(userId, team);
            }
            _game.PacketNotifier.NotifyS2C_MarkOrSweepForSoftReconnect(userId, SoftReconnectStage.DestroyAllUnits);
        }

        public void SpawnObjects(ClientInfo clientInfo)
        {
            foreach (GameObject obj in _objects)
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
                    nearSighted ? _vision.UnitHasVisionOn(champion, obj, nearSighted) : obj.IsVisibleByTeam(champion.Team)
                );

                // SpecificUnitToExclude: hide from the excluded recipient even if FoW would show it.
                if (shouldBeVisibleForPlayer && obj is Particle excludeParticle
                    && excludeParticle.SpecificUnitExclude is Champion excluded && excluded.ClientId == cid)
                {
                    shouldBeVisibleForPlayer = false;
                }
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
                    _objects.PushBackUnique(o, o.NetId);
                    if (o.ReceivesUpdate)
                    {
                        _objectsToUpdate.PushBackUnique(o, o.NetId);
                    }
                }

                if (o is SpellMissile missile)
                {
                    _missiles.PushBackUnique(missile, missile.NetId);
                }

                // Central per-type routing (Riot adds each object to its typed manager in AddObject).
                // Registered immediately (not via the deferred _objectsToAdd path) so typed lookups see
                // the unit the same tick it is created — matching _missiles above and the OnAdded-based
                // AddChampion/AddTurret/AddInhibitor self-registration below.
                if (o is AttackableUnit unit)
                {
                    _attackableUnits.PushBackUnique(unit, unit.NetId);
                }

                if (o is Minion addedMinion)
                {
                    _minions.PushBackUnique(addedMinion, addedMinion.NetId);
                }

                if (o is Marker addedMarker)
                {
                    _markers.PushBackUnique(addedMarker, addedMarker.NetId);
                }

                if (o is LevelProp addedProp)
                {
                    _props.PushBackUnique(addedProp, addedProp.NetId);
                }

                if (o is ShopObject addedShop)
                {
                    _shops.PushBackUnique(addedShop, addedShop.NetId);
                }

                if (o is FollowerObject addedFollower)
                {
                    _followers.PushBackUnique(addedFollower, addedFollower.NetId);
                }

                // Synchronous spawn at creation — intentional, not a hack (this used to carry a
                // "packet queue system" TODO from an old AscWarp ordering bug): spawning here means
                // the spawn packet precedes every same-tick follow-up packet (buffs/FX from the
                // creating script) that references the new NetID. Vision scoping still applies —
                // Sync's forceSpawn path only spawns to a client when visible || !SpawnShouldBeHidden;
                // hidden units spawn later via the vision pass (0xBA). Residual out-of-order arrivals
                // are Riot's CLIENT's job, not a server queue: the 4.17 client holds FX whose bind
                // object is unknown and retries DisplayParticle for ~1s (Spell::Effect::EffectHashMap
                // mHoldForFrameParticles, SpellEffectPacketsClient.cpp). Champions are excluded
                // because their spawn rides the connection handshake (SpawnObjects/HandleSpawn).
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
                    _objects.Erase(o, o.NetId);
                    _objectsToUpdate.Erase(o, o.NetId); // no-op if it had opted out
                }

                if (o is SpellMissile missile)
                {
                    _missiles.Erase(missile, missile.NetId);
                }

                if (o is AttackableUnit unit)
                {
                    _attackableUnits.Erase(unit, unit.NetId);
                }

                if (o is Minion removedMinion)
                {
                    _minions.Erase(removedMinion, removedMinion.NetId);
                }

                if (o is Marker removedMarker)
                {
                    _markers.Erase(removedMarker, removedMarker.NetId);
                }

                if (o is LevelProp removedProp)
                {
                    _props.Erase(removedProp, removedProp.NetId);
                }

                if (o is ShopObject removedShop)
                {
                    _shops.Erase(removedShop, removedShop.NetId);
                }

                if (o is FollowerObject removedFollower)
                {
                    _followers.Erase(removedFollower, removedFollower.NetId);
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
                ret.Add(obj.NetId, obj);
            }

            return ret;
        }

        /// <summary>
        /// Copy-free, insertion-ordered read-only view of ALL GameObjects. Prefer this over
        /// <see cref="GetObjects"/> when you only need to iterate (no NetId keying): GetObjects()
        /// allocates a fresh Dictionary on every call, which is wasteful on per-tick/per-event paths.
        /// Do not mutate the manager while iterating this.
        /// </summary>
        public IReadOnlyList<GameObject> GetAllObjects()
        {
            return _objects.GetArray();
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
                obj = _objects.Find(id);
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
            return _vision.TeamHasVisionOn(team, o);
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
            _vision.QueryUnitsInRange(checkPos, range, onlyAlive, result);
        }

        /// <summary>
        /// Adds a GameObject to the list of Vision Providers in ObjectManager.
        /// </summary>
        /// <param name="obj">GameObject to add.</param>
        /// <param name="team">The team that GameObject can provide vision to.</param>
        public void AddVisionProvider(GameObject obj, TeamId team)
        {
            _vision.AddVisionProvider(obj, team);
        }

        /// <summary>
        /// Removes a GameObject from the list of Vision Providers in ObjectManager.
        /// </summary>
        /// <param name="obj">GameObject to remove.</param>
        /// <param name="team">The team that GameObject provided vision to.</param>
        public void RemoveVisionProvider(GameObject obj, TeamId team)
        {
            _vision.RemoveVisionProvider(obj, team);
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
            foreach (var u in _attackableUnits)
            {
                if (Vector2.DistanceSquared(checkPos, u.Position) <= range * range &&
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
            int count = 0;
            foreach (var u in _attackableUnits)
            {
                if (u is ObjAIBase aiBase &&
                    aiBase.Team == target.Team.GetEnemyTeam() &&
                    !aiBase.IsDead &&
                    aiBase.TargetUnit != null &&
                    aiBase.TargetUnit == target)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Forces all GameObjects of type ObjAIBase to stop targeting the specified AttackableUnit.
        /// </summary>
        /// <param name="target">AttackableUnit that should be untargeted.</param>
        public void StopTargeting(AttackableUnit target)
        {
            foreach (var u in _attackableUnits)
            {
                if (u is ObjAIBase ai)
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
            _turrets.PushBackUnique(turret, turret.NetId);
        }

        /// <summary>
        /// Gets a GameObject of type BaseTurret from the list of BaseTurrets in ObjectManager who is identified by the specified NetID.
        /// Unused.
        /// </summary>
        /// <param name="netId"></param>
        /// <returns>BaseTurret instance identified by the specified NetID.</returns>
        public BaseTurret GetTurretById(uint netId)
        {
            return _turrets.Find(netId);
        }

        /// <summary>
        /// Removes a GameObject of type BaseTurret from the list of BaseTurrets in ObjectManager.
        /// Unused.
        /// </summary>
        /// <param name="turret">BaseTurret to remove.</param>
        public void RemoveTurret(BaseTurret turret)
        {
            _turrets.Erase(turret, turret.NetId);
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
            foreach (var turret in _turrets)
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
            _inhibitors.PushBackUnique(inhib, inhib.NetId);
        }

        /// <summary>
        /// Gets a GameObject of type Inhibitor from the list of Inhibitors in ObjectManager who is identified by the specified NetID.
        /// </summary>
        /// <param name="netId"></param>
        /// <returns>Inhibitor instance identified by the specified NetID.</returns>
        public Inhibitor GetInhibitorById(uint id)
        {
            return _inhibitors.Find(id);
        }

        /// <summary>
        /// Removes a GameObject of type Inhibitor from the list of Inhibitors in ObjectManager.
        /// </summary>
        /// <param name="inhib">Inhibitor to remove.</param>
        public void RemoveInhibitor(Inhibitor inhib)
        {
            _inhibitors.Erase(inhib, inhib.NetId);
        }

        /// <summary>
        /// Whether or not all of the Inhibitors of a specified team are destroyed.
        /// </summary>
        /// <param name="team">Team of the Inhibitors to check.</param>
        /// <returns>true/false; destroyed or not</returns>
        public bool AllInhibitorsDestroyedFromTeam(TeamId team)
        {
            foreach (var inhibitor in _inhibitors)
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
            _champions.PushBackUnique(champion, champion.NetId);
            PacketLogger.TagChampion(champion.NetId);
        }

        /// <summary>
        /// Removes a GameObject of type Champion from the list of Champions in ObjectManager.
        /// </summary>
        /// <param name="champion">Champion to remove.</param>
        public void RemoveChampion(Champion champion)
        {
            _champions.Erase(champion, champion.NetId);
        }

        /// <summary>
        /// Gets a new list of all Champions found in the list of Champions in ObjectManager.
        /// </summary>
        /// <returns>List of all valid Champions.</returns>
        public List<Champion> GetAllChampions()
        {
            var champs = new List<Champion>();
            foreach (var c in _champions)
            {
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
            foreach (var c in _champions)
            {
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
            foreach (var c in _champions)
            {
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
            foreach (var c in _champions)
            {
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
            foreach (var c in _champions)
            {
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
            return _missiles.ToList();
        }

        /// <summary>
        /// Copy-free read-only view of all Minions (lane minions, pets, ...) in insertion order.
        /// Iterate this instead of scanning GetObjects() when you only care about minions.
        /// </summary>
        public IReadOnlyList<Minion> GetAllMinions()
        {
            return _minions.GetArray();
        }

        /// <summary>
        /// Copy-free read-only view of all Markers in insertion order (Riot: obj_AI_Marker manager).
        /// </summary>
        public IReadOnlyList<Marker> GetAllMarkers()
        {
            return _markers.GetArray();
        }

        /// <summary>
        /// Copy-free read-only view of all LevelProps in insertion order (Riot: ManagedLevelProp manager).
        /// </summary>
        public IReadOnlyList<LevelProp> GetAllLevelProps()
        {
            return _props.GetArray();
        }

        /// <summary>
        /// Copy-free read-only view of all ShopObjects in insertion order (Riot: obj_Shop manager).
        /// </summary>
        public IReadOnlyList<ShopObject> GetAllShops()
        {
            return _shops.GetArray();
        }

        /// <summary>
        /// Copy-free read-only view of all FollowerObjects in insertion order (Riot: FollowerObject
        /// manager). e.g. Syndra's orbs.
        /// </summary>
        public IReadOnlyList<FollowerObject> GetAllFollowers()
        {
            return _followers.GetArray();
        }
    }
}
