using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace GameServerCore.Domain
{
    /// <summary>
    /// Result of an engine-driven per-barrack minion spawn (Riot's GetMinionSpawnInfo return shape,
    /// reduced to what the engine clock needs). See docs/LANE_MINION_ENGINE_INVERSION_PLAN.md.
    /// </summary>
    public struct BarrackSpawnResult
    {
        /// <summary>Number of minions in this barrack's wave (drives the drip's max-wave-size barrier).</summary>
        public int WaveSize;
        /// <summary>Current cannon-wave cap (drives the manager's per-wave cannon-count rollover).</summary>
        public int CannonCap;
    }

    public interface IMapScript : IUpdate
    {
        MapScriptMetadata MapScriptMetadata { get; }
        bool HasFirstBloodHappened { get; set; }
        string LaneMinionAI { get; }
        Dictionary<TeamId, Dictionary<int, Dictionary<int, Vector2>>> PlayerSpawnPoints { get; }
        Dictionary<TeamId, Dictionary<MinionSpawnType, string>> MinionModels { get; }
        void Init(Dictionary<GameObjectTypes, List<MapObject>> mapObjects);
        void OnMatchStart()
        {
        }
        void SpawnAllCamps()
        {
        }
        Vector2 GetFountainPosition(TeamId team)
        {
            return Vector2.Zero;
        }
        /// <summary>
        /// Engine-driven spawn callback (only used when
        /// <see cref="MapScriptMetadata.EngineDrivenMinionSpawn"/> is true): spawn this barrack's
        /// minion at index <paramref name="minionNumber"/> of wave <paramref name="waveCount"/> and
        /// return the barrack's wave size + current cannon cap. The engine's
        /// <c>BarracksSpawnManager</c> owns the wave/drip clock and calls this per barrack per drip
        /// tick. Default: no-op (maps that drive their own loop don't implement it).
        /// </summary>
        BarrackSpawnResult SpawnBarrackMinion(TeamId barrackTeam, TeamId opposingTeam, Lane lane, Vector2 position, string barracksName, int minionNumber, int waveCount, int cannonMinionCount)
        {
            return new BarrackSpawnResult();
        }
    }
}