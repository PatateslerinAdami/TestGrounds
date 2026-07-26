using GameServerCore.Domain;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Logging;
using System.Collections.Generic;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace MapScripts.Map8
{
    public static class LevelScriptObjects
    {
        private static Dictionary<GameObjectTypes, List<MapObject>> _mapObjects;

        public static List<InfoPoint> InfoPoints = new List<InfoPoint>();
        public static Dictionary<TeamId, Fountain> FountainList = new Dictionary<TeamId, Fountain>();
        static Dictionary<TeamId, List<LaneTurret>> TurretList = new Dictionary<TeamId, List<LaneTurret>> { { TeamId.TEAM_BLUE, new List<LaneTurret>() }, { TeamId.TEAM_PURPLE, new List<LaneTurret>() } };

        //Turret netIds are used for the capture point announcements
        static Dictionary<TeamId, LaneTurret> AnnouncementUnits = new Dictionary<TeamId, LaneTurret>();
        public static Dictionary<TeamId, string> TowerModels { get; set; } = new Dictionary<TeamId, string>
        {
            {TeamId.TEAM_BLUE, "OdinOrderTurretShrine" },

            {TeamId.TEAM_PURPLE, "OdinChaosTurretShrine" }
        };
        public static List<Vector2> OuterRingWaypoints = new List<Vector2>()
        {

            /* 0  */ new Vector2(3610.9226f,3216.8652f),// node 0 left exit
            /* 1  */ new Vector2(3244.1106f,3727.395f), 
            /* 2  */ new Vector2(2859.0447f,4361.87f),
            /* 3  */ new Vector2(2364.983f,5805.6553f), 
            /* 4  */ new Vector2(2387.6304f,6750.171f), // node 1, bottom exit
            /* 5  */ new Vector2(2864.537f,7727.3164f), 
            /* 6  */ new Vector2(3070.2078f,8887.6875f),// node 1, top exit
            /* 7  */ new Vector2(4150.06f,10002.6455f),  
            /* 8  */ new Vector2(5851.13f,10842.542f), // node 2 left exit
            /* 9  */ new Vector2(6948.013f,10690.012f),  
            /* 10 */ new Vector2(8059.759f,10863.691f),// node 2 right exit
            /* 11 */ new Vector2(9545.128f,10211.386f),
            /* 12 */ new Vector2(10898.374f,8849.669f),// node 3 up exit
            /* 13 */ new Vector2(11044.935f,7675.5737f),
            /* 14 */ new Vector2(11548.407f,6822.652f),// node 3 bottom exit
            /* 15 */ new Vector2(11226.626f,4780.272f),
            /* 16 */ new Vector2(10364.267f,3123.754f),// node 4 right exit
            /* 17 */ new Vector2(9369.474f,2815.0679f),
            /* 18 */ new Vector2(8641.735f,1932.0095f),// node 4 left exit
            /* 19 */ new Vector2(6938.65f,1801.8568f),
            /* 20 */ new Vector2(5219.536f,1897.6672f),// node 0 right exit
            /* 21 */ new Vector2(4547.2026f,2782.665f),
        };

        public static void LoadObjects(Dictionary<GameObjectTypes, List<MapObject>> mapObjects)
        {
            _mapObjects = mapObjects;

            CreateBuildings();
            LoadFountains();
            InitializeInfoPointPathData();
        }
        public static void InitializeInfoPointPathData()
        {
            foreach (var node in InfoPoints)
            {
                switch (node.Index)
                {
                    case 0: // Bottom Left 
                            // Left = Heading to Bottom Right (Index 4)
                            // Right = Heading to Top Left (Index 1)
                        node.ConfigureLeftPath(
                            spawn: new Vector2(4248.6963f, 1169.9697f),
                            getToList: new List<Vector2> { new Vector2(4706.641f, 1259.611f), new Vector2(5155.027f, 1689.7472f) },
                            circleIndex: 20
                        );
                        node.ConfigureRightPath(
                            spawn: new Vector2(3094.7805f, 2052.4443f),
                            getToList: new List<Vector2> { new Vector2(3078.8464f, 2509.1797f), new Vector2(3377.8574f, 2991.413f) },
                            circleIndex: 0
                        );
                        break;

                    case 1: // Top Left 
                            // Left = Heading to Bottom Left (Index 0)
                            // Right = Heading to Top (Index 2)
                        node.ConfigureLeftPath(
                            spawn: new Vector2(891.49603f, 7725.269f),
                            getToList: new List<Vector2> { new Vector2(1106.3507f, 7156.923f), new Vector2(2099.306f, 6834.8877f) },
                            circleIndex: 4
                        );
                        node.ConfigureRightPath(
                            spawn: new Vector2(1426.9653f, 8860.267f),
                            getToList: new List<Vector2> { new Vector2(1770.4729f, 9193.081f), new Vector2(2755.965f, 8962.923f) },
                            circleIndex: 6
                        );
                        break;

                    case 2: // Top
                            // Left = Heading to Top Left (Index 1)
                            // Right = Heading to Top Right (Index 3)
                        node.ConfigureLeftPath(
                            spawn: new Vector2(6629.3203f, 12112.406f),
                            getToList: new List<Vector2> { new Vector2(6112.3066f, 11826.429f), new Vector2(5955.72f, 11201.969f) },
                            circleIndex: 8
                        );
                        node.ConfigureRightPath(
                            spawn: new Vector2(7249.1084f, 12088.828f),
                            getToList: new List<Vector2> { new Vector2(7851.78f, 11766.658f), new Vector2(7993.3394f, 11206.448f) },
                            circleIndex: 10
                        );
                        break;
                    case 3: // Top Right 
                            // Left = Heading to Top (Index 2)
                            // Right = Heading to Bottom Right (Index 4)
                        node.ConfigureLeftPath(
                            spawn: new Vector2(12468.741f, 9023.209f),
                            getToList: new List<Vector2> { new Vector2(11906.954f, 9173.242f), new Vector2(11077.72f, 8881.14f) },
                            circleIndex: 12
                        );
                        node.ConfigureRightPath(
                            spawn: new Vector2(13047.967f, 7620.207f),
                            getToList: new List<Vector2> { new Vector2(12851.277f, 7147.982f), new Vector2(11881.35f, 6876.224f) },
                            circleIndex: 14
                        );
                        break;
                    case 4: // Bottom Right
                            // Left = Heading to Top Right (Index 3)
                            // Right = Heading to Bottom Left (Index 0)
                        node.ConfigureLeftPath(
                            spawn: new Vector2(10750.053f, 2041.3547f),
                            getToList: new List<Vector2> { new Vector2(10869.814f, 2348.3647f), new Vector2(10516.621f, 2952.9238f) },
                            circleIndex: 16
                        );
                        node.ConfigureRightPath(
                            spawn: new Vector2(9573.964f, 1166.7341f),
                            getToList: new List<Vector2> { new Vector2(9289.186f, 1149.5382f), new Vector2(8810.073f, 1609.1157f) },
                            circleIndex: 18
                        );
                        break;
                }
            }
        }
        public static void OnMatchStart()
        {
            LoadShops();
            /*
            foreach (var infoPoint in InfoPoints)
            {
                const byte particleFlexId = 0;
                const uint particleAttachType = 1;

                NotifyAttachFlexParticle(infoPoint.Point.NetId, particleFlexId, infoPoint.Index, particleAttachType);
                NotifyHandleCapturePointUpdate(infoPoint.Index, infoPoint.Point.NetId, 0, 0, CapturePointUpdateCommand.AttachToObject);
            }
            */
        }

        public static void OnUpdate(float diff)
        {
            foreach (var fountain in FountainList.Values)
            {
                fountain.Update(diff);
            }
        }

        static void LoadFountains()
        {
            foreach (var fountain in _mapObjects[GameObjectTypes.ObjBuilding_SpawnPoint])
            {
                var team = fountain.GetTeamID();
                FountainList.Add(team, CreateFountain(team, new Vector2(fountain.CentralPoint.X, fountain.CentralPoint.Z)));
            }
        }

        static void LoadShops()
        {
            foreach (var shop in _mapObjects[GameObjectTypes.ObjBuilding_Shop])
            {
                CreateShop(shop.Name, new Vector2(shop.CentralPoint.X, shop.CentralPoint.Z), shop.GetTeamID());
            }
        }

        static void CreateBuildings()
        {
            foreach (var turretObj in _mapObjects[GameObjectTypes.ObjAIBase_Turret])
            {
                var teamId = turretObj.GetTeamID();
                var position = new Vector2(turretObj.CentralPoint.X, turretObj.CentralPoint.Z);
                var fountainTurret = CreateLaneTurret(turretObj.Name + "_A", TowerModels[teamId], position, teamId, TurretType.FOUNTAIN_TURRET, Lane.LANE_Unknown, "TurretAI", turretObj);
                TurretList[teamId].Add(fountainTurret);

                if (!fountainTurret.Name.Contains('1'))
                {
                    AnnouncementUnits.Add(fountainTurret.Team, fountainTurret);
                }

                AddObject(fountainTurret);
            }

            byte pointIndex = 0;
            foreach (var infoPoint in _mapObjects[GameObjectTypes.InfoPoint])
            {
                InfoPoints.Add(new InfoPoint(new Vector2(infoPoint.CentralPoint.X, infoPoint.CentralPoint.Z), pointIndex, infoPoint.Name[infoPoint.Name.Length - 1]));
                pointIndex++;
            }
        }
        public static void SyncStateForPlayer(int userId)
        {
            CreateTimer(3.0f, () =>
            {
            });
            foreach (var infoPoint in InfoPoints)
            {
                uint netId = infoPoint.Point.NetId;
                byte cpIndex = infoPoint.Index;

                    NotifyAttachFlexParticle(netId, 0, cpIndex, 1, userId);
                    NotifyHandleCapturePointUpdate(cpIndex, netId, 0, (byte)0, (CapturePointUpdateCommand)0, userId);
            }
        }
    }

    public class InfoPoint
    {
        public Minion Point;
        public char Id;
        public byte Index;

        public Vector2 LeftSpawnPoint;
        public List<Vector2> LeftGetToCircleWaypoints = new List<Vector2>();
        public int LeftCircleIndex;

        public Vector2 RightSpawnPoint;
        public List<Vector2> RightGetToCircleWaypoints = new List<Vector2>();
        public int RightCircleIndex;

        public InfoPoint(Vector2 position, byte index, char id)
        {
            Point = CreateMinion("OdinNeutralGuardian", "OdinNeutralGuardian", position, ignoreCollision: true, aiScript: "OdinCapturePointAI", direction: new Vector3(0, 0, 1));
            if (Point.AIScript is AIScripts.OdinCapturePointAI captureAI)
            {
                captureAI.PointLetter = id;
                captureAI.PointIndex = index;
            }
            Point.SetStatus(StatusFlags.CanMoveEver, false);
            Point.Stats.CurrentMana = 25000f;
            Point.DisableFoW = true;
            Id = id;
            Index = index;

            NotifyAttachFlexParticle(Point.NetId, 0, Index, 1);
            NotifyHandleCapturePointUpdate(Index, Point.NetId, 0, (byte)0, (CapturePointUpdateCommand)0);

            AddPosPerceptionBubble(Point.Position, 1600, 25000.0f, TeamId.TEAM_BLUE, false, collisionArea: 120.0f, grassRadius: 150f, regionType: RegionType.Unknown2);
            AddPosPerceptionBubble(Point.Position, 1600, 25000.0f, TeamId.TEAM_PURPLE, false, collisionArea: 120.0f, grassRadius: 150f, regionType:RegionType.Unknown2);
            CreateTimer(0.1f, () =>
            {
            });
        }
        public void ConfigureLeftPath(Vector2 spawn, List<Vector2> getToList, int circleIndex)
        {
            LeftSpawnPoint = spawn;
            LeftGetToCircleWaypoints = getToList ?? new List<Vector2>();
            LeftCircleIndex = circleIndex;
        }

        public void ConfigureRightPath(Vector2 spawn, List<Vector2> getToList, int circleIndex)
        {
            RightSpawnPoint = spawn;
            RightGetToCircleWaypoints = getToList ?? new List<Vector2>();
            RightCircleIndex = circleIndex;
        } 
    }
}
