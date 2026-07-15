using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Util;
using log4net;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LeagueSandbox.GameServer
{
    /// <summary>
    /// Server-side Fog-of-War / vision authority, extracted from <see cref="ObjectManager"/> (Riot keeps
    /// vision OUT of its object manager too — a NetVisibilityObject manager + client-side
    /// <c>Riot::FogOfWar::Field</c>). Owns:
    ///   * the per-team vision-provider set (<see cref="_visionProviders"/>),
    ///   * the per-tick provider spatial grid used by <see cref="TeamHasVisionOn"/>, and
    ///   * a general per-tick AttackableUnit spatial grid used by <see cref="QueryUnitsInRange"/>
    ///     (both grids share the same cell dimensions, sized once from the map).
    /// ObjectManager keeps the orchestration that applies vision to objects/networking
    /// (UpdateTeamsVision / UpdateVisionSpawnAndSync) and exposes thin facades for the public API.
    /// </summary>
    public class VisionManager
    {
        private readonly Game _game;
        private readonly List<TeamId> _teams;
        // Live reference to ObjectManager's AttackableUnit registry (used to (re)build the unit grid).
        private readonly ManagerTemplate<AttackableUnit> _attackableUnits;
        private static readonly ILog _logger = LoggerProvider.GetLogger();

        private readonly FrozenDictionary<TeamId, HashSet<GameObject>> _visionProviders;

        // Per-team uniform spatial index of vision providers, rebuilt once per tick in
        // RebuildVisionProviderIndex. Replaces the O(objects × providers) full scan that
        // TeamHasVisionOn used to do (the #1 server hotspot at ~0.5 ms/tick). Allocation-free
        // across ticks: the bucket lists are cleared and refilled, never reallocated. A query
        // only visits providers in the cells overlapping the tested object's max-vision-radius
        // box; UnitHasVisionOn then does the exact distance + LOS test, so decisions are identical.
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

        public VisionManager(Game game, List<TeamId> teams, ManagerTemplate<AttackableUnit> attackableUnits)
        {
            _game = game;
            _teams = teams;
            _attackableUnits = attackableUnits;
            _visionProviders = teams.ToDictionary(team => team, _ => new HashSet<GameObject>()).ToFrozenDictionary();
        }

        /// <summary>
        /// Marks the provider index stale for this tick. Any TeamHasVisionOn call before
        /// RebuildVisionProviderIndex runs (e.g. out-of-band SpawnObject during scripts) then falls
        /// back to the legacy scan, so it never reads a stale index.
        /// </summary>
        public void InvalidateProviderIndex()
        {
            _providerIndexValid = false;
        }

        /// <summary>
        /// Adds a GameObject to the set of Vision Providers for the specified team.
        /// </summary>
        public void AddVisionProvider(GameObject obj, TeamId team)
        {
            _visionProviders[team].Add(obj);
        }

        /// <summary>
        /// Removes a GameObject from the set of Vision Providers for the specified team.
        /// </summary>
        public void RemoveVisionProvider(GameObject obj, TeamId team)
        {
            _visionProviders[team].Remove(obj);
        }

        /// <summary>
        /// Whether or not a specified GameObject is being networked to the specified team.
        /// </summary>
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

            // Dead units should never provide vision. (Zombies have IsDead=false under Model B, so
            // they remain live vision providers until their real death — no special-casing needed.)
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
            foreach (var team in _teams)
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
        public void RebuildVisionProviderIndex()
        {
            EnsureVisionProviderIndex();

            foreach (var team in _teams)
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
        public void RebuildUnitGrid()
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

            foreach (var u in _attackableUnits)
            {
                Vector2 pos = u.Position;
                _unitGridCells[RowOf(pos.Y) * _gridCols + ColOf(pos.X)].Add(u);
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
                // Index not built yet this tick (rare/out-of-band) — fall back to a units-only scan.
                foreach (var u in _attackableUnits)
                {
                    if ((!onlyAlive || !u.IsDead)
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
                foreach (var u in _attackableUnits)
                {
                    if ((!onlyAlive || !u.IsDead)
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

        /// <summary>
        /// Exact single-provider visibility test: distance cull (effective vision radius) + line-of-sight
        /// raycast, plus the reveal/stealth/region-target special cases. Called by the grid and legacy
        /// paths and by ObjectManager's near-sighted per-player check.
        /// </summary>
        public bool UnitHasVisionOn(GameObject observer, GameObject tested, bool nearSighted = false)
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
    }
}
