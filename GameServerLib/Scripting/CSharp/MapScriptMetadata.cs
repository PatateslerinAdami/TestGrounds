namespace LeagueSandbox.GameServer.Scripting.CSharp
{
    public class MapScriptMetadata
    {
        public float ChampionBaseGoldValue { get; set; } = 300.0f;
        public float ChampionMaxGoldValue { get; set; } = 500.0f;
        public float ChampionMinGoldValue { get; set; } = 50.0f;
        public string ExpCurveOverride { get; set; } = string.Empty;
        public float FirstBloodExtraGold { get; set; } = 100.0f;
        public int InitialLevel { get; set; } = 1;
        public bool IsKillGoldRewardReductionActive { get; set; } = true;
        public int MaxLevel { get; set; } = 18;
        public bool MinionSpawnEnabled { get; set; } = false;
        public string NavGridOverride { get; set; } = string.Empty;
        public bool OverrideSpawnPoints { get; set; } = false;
        public int RecallSpellItemId { get; set; } = 2001;
        public long SpawnInterval { get; set; } = 30 * 1000;
        /// <summary>
        /// Opt-in: when true the engine-side <c>BarracksSpawnManager</c> drives the lane-minion wave/
        /// drip clock and calls back into the map script's <c>SpawnBarrackMinion</c> for composition
        /// (Riot's engine/script split). When false (default) the map script's own Update loop drives
        /// spawning. Lets maps migrate to the engine driver one at a time.
        /// See docs/LANE_MINION_ENGINE_INVERSION_PLAN.md.
        /// </summary>
        public bool EngineDrivenMinionSpawn { get; set; } = false;
        /// <summary>
        /// Game time (ms) the first lane-minion wave spawns at. Only used when
        /// <see cref="EngineDrivenMinionSpawn"/> is true (seeds the engine driver's wave clock).
        /// </summary>
        public long FirstMinionSpawnTime { get; set; } = 10 * 1000;
    }
}
