using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Domain;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;

namespace LeagueSandbox.GameServer.Handlers
{
    /// <summary>
    /// Engine-side driver of the per-barrack lane-minion spawn loop, owning the wave/drip clock and
    /// calling back into the content map script (<see cref="IMapScript.SpawnBarrackMinion"/>) for
    /// wave composition. Mirrors Riot's <c>obj_Barracks::Update</c> being ticked by
    /// <c>GameObjectManager</c>; here the manager holds the <see cref="SpawnBarrack"/>s and is ticked
    /// from <see cref="MapScriptHandler.Update"/> (engine-side, not the content script).
    /// See docs/LANE_MINION_ENGINE_INVERSION_PLAN.md.
    ///
    /// Wire-identity note: this replicates the legacy LevelScript clock arithmetic EXACTLY (single
    /// shared wave/drip clock with a global max-wave-size barrier so lanes never desync), so enabling
    /// it via <see cref="MapScriptMetadata.EngineDrivenMinionSpawn"/> is a pure refactor of WHERE the
    /// loop lives, not a behavioral change. Only the WHAT (composition) is delegated to the script.
    /// </summary>
    public class BarracksSpawnManager
    {
        // 4.20 SingleMinionSpawnDelay (LevelScript.lua): one minion every 800ms within a wave.
        private const long MINION_SPAWN_DELAY = 800;

        private readonly Game _game;
        private readonly IMapScript _mapScript;
        private readonly List<SpawnBarrack> _barracks = new List<SpawnBarrack>();

        // Shared wave/drip clock state (was the global LevelScript fields _minionNumber / _waveCount /
        // _cannonMinionCount / NextSpawnTime).
        private int _minionNumber;
        private int _waveCount;
        private int _cannonMinionCount;
        private long _nextSpawnTime;
        private bool _clockSeeded;

        public IReadOnlyList<SpawnBarrack> Barracks => _barracks;

        public BarracksSpawnManager(Game game, IMapScript mapScript)
        {
            _game = game;
            _mapScript = mapScript;
        }

        /// <summary>
        /// Builds one <see cref="SpawnBarrack"/> per barrack MapObject. The barrack MapObjects are
        /// available engine-side from <see cref="MapScriptHandler"/>'s loaded map objects
        /// (<see cref="GameObjectTypes.ObjBuildingBarracks"/>).
        /// </summary>
        public void Init(IEnumerable<MapObject> barrackObjects)
        {
            if (barrackObjects == null)
            {
                return;
            }

            foreach (var barrack in barrackObjects)
            {
                var position = new Vector2(barrack.CentralPoint.X, barrack.CentralPoint.Z);
                _barracks.Add(new SpawnBarrack(_game, barrack.GetTeamID(), barrack.GetOpposingTeamID(), barrack.GetSpawnBarrackLaneID(), position, barrack.Name));
            }
        }

        public void Update(float diff)
        {
            var meta = _mapScript.MapScriptMetadata;

            // Opt-in: maps that haven't migrated drive their own loop; do nothing here for them.
            if (!meta.EngineDrivenMinionSpawn)
            {
                return;
            }

            if (!meta.MinionSpawnEnabled)
            {
                return;
            }

            if (!_clockSeeded)
            {
                _nextSpawnTime = meta.FirstMinionSpawnTime;
                _clockSeeded = true;
            }

            float gameTime = _game.GameTime;

            // Mirrors the legacy LevelScript.Update spawn gate exactly.
            if (_minionNumber > 0)
            {
                // Drip: one minion every MINION_SPAWN_DELAY ms after the wave's first.
                if (gameTime >= _nextSpawnTime + _minionNumber * MINION_SPAWN_DELAY)
                {
                    if (SpawnDripTick())
                    {
                        _minionNumber = 0;
                        _waveCount++;
                        _nextSpawnTime = (long)gameTime + meta.SpawnInterval;
                    }
                    else
                    {
                        _minionNumber++;
                    }
                }
            }
            else if (gameTime >= _nextSpawnTime)
            {
                SpawnDripTick();
                _minionNumber++;
            }
        }

        /// <summary>
        /// Spawns the minion at the current drip index across all barracks (mirrors the legacy
        /// SetUpLaneMinion loop). Returns true when the wave is complete (the drip has reached the
        /// largest wave among the barracks), at which point the manager advances to the next wave.
        /// </summary>
        private bool SpawnDripTick()
        {
            int cannonMinionCap = 2;
            // Largest wave among the barracks this cycle — the drip runs exactly this many ticks
            // (smaller waves produce no packet on their dead slots), keeping all lanes' wave clocks
            // in lockstep so they never desync.
            int maxWaveSize = 0;

            foreach (var barrack in _barracks)
            {
                BarrackSpawnResult result = _mapScript.SpawnBarrackMinion(barrack.Team, barrack.OpposingTeam, barrack.Lane, barrack.SpawnPosition, barrack.BarracksName, _minionNumber, _waveCount, _cannonMinionCount);
                cannonMinionCap = result.CannonCap;
                maxWaveSize = Math.Max(maxWaveSize, result.WaveSize);
            }

            if (_minionNumber < maxWaveSize - 1)
            {
                return false;
            }

            if (_cannonMinionCount >= cannonMinionCap)
            {
                _cannonMinionCount = 0;
            }
            else
            {
                _cannonMinionCount++;
            }
            return true;
        }
    }
}
