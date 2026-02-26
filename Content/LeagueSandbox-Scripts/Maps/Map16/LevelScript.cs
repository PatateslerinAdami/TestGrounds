using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Domain;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;
using static LeagueSandbox.GameServer.API.ApiGameEvents;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;

namespace MapScripts.Map16
{
    public class CLASSIC : IMapScript
    {
        public MapScriptMetadata MapScriptMetadata { get; set; } = new MapScriptMetadata();
        public bool HasFirstBloodHappened { get; set; }
        public string LaneMinionAI { get; }
        public Dictionary<TeamId, Dictionary<int, Dictionary<int, Vector2>>> PlayerSpawnPoints { get; }
        public Dictionary<TeamId, Dictionary<MinionSpawnType, string>> MinionModels { get; }
        //public void Init(Dictionary<GameObjectTypes, List<MapObject>> mapObjects);
        public void Init(Dictionary<GameObjectTypes, List<MapObject>> objects)
        {
        }
        public void OnMatchStart()
        {
        }
        public void SpawnAllCamps()
        {
        }
        public Vector2 GetFountainPosition(TeamId team)
        {
            return Vector2.Zero;
        }
    }
}
