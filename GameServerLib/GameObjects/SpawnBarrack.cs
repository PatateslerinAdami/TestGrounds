using System.Numerics;
using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.GameObjects
{
    /// <summary>
    /// Server-side, per-barrack lane-minion spawn controller. Mirrors Riot's engine object
    /// <c>obj_Barracks</c> (mac decomp 4.17 <c>Barracks.cpp</c>): the engine owns a barrack per
    /// lane+team and drives its wave/drip spawn loop, calling back into the content map script for
    /// wave composition. See docs/LANE_MINION_ENGINE_INVERSION_PLAN.md.
    ///
    /// Deliberately NOT a <see cref="GameObject"/> (Fountain pattern): it has no NetId, grants no
    /// vision, has no collision footprint, and is never replicated to clients. It exists purely to
    /// hold spawn state and (from Phase 2 on) call CreateLaneMinion — so it cannot leak a phantom
    /// object onto the wire.
    /// </summary>
    public class SpawnBarrack
    {
        private Game _game;

        /// <summary>Team that owns this barrack (whose minions it spawns).</summary>
        public TeamId Team { get; }
        /// <summary>Enemy team (whose lane this barrack pushes into; for inhibitor-state lookup).</summary>
        public TeamId OpposingTeam { get; }
        /// <summary>Lane this barrack spawns minions into.</summary>
        public Lane Lane { get; }
        /// <summary>World position the wave spawns from (barrack CentralPoint).</summary>
        public Vector2 SpawnPosition { get; }
        /// <summary>The barrack MapObject name (attribution / Barrack_SpawnUnit source).</summary>
        public string BarracksName { get; }

        public SpawnBarrack(Game game, TeamId team, TeamId opposingTeam, Lane lane, Vector2 spawnPosition, string barracksName)
        {
            _game = game;
            Team = team;
            OpposingTeam = opposingTeam;
            Lane = lane;
            SpawnPosition = spawnPosition;
            BarracksName = barracksName;
        }
    }
}
