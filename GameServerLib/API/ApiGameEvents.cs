using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Logging;
using log4net;

namespace LeagueSandbox.GameServer.API
{
    public static class ApiGameEvents
    {
        private static Game _game;
        private static ILog _logger = LoggerProvider.GetLogger();
        public static void SetGame(Game game)
        {
            _game = game;
        }

        public static void AnnounceCaptureAltar(Minion altar, byte index)
        {
            _game.PacketNotifier.NotifyS2C_OnEventWorld(new OnCaptureAltar() { CapturePoint = index, OtherNetID = altar.NetId });
        }

        public static void AnnounceCapturePointCaptured(Minion turret, char point, Champion captor = null)
        {
            IEvent captured;
            uint pointId = 0;

            switch (char.ToUpper(point))
            {
                case 'A': 
                    captured = new OnCapturePointCaptured_A(); 
                    pointId = 0; 
                    break;
                case 'B': 
                    captured = new OnCapturePointCaptured_B(); 
                    pointId = 1; 
                    break;
                case 'C': 
                    captured = new OnCapturePointCaptured_C(); 
                    pointId = 2; 
                    break;
                case 'D': 
                    captured = new OnCapturePointCaptured_D(); 
                    pointId = 3; 
                    break;
                case 'E': 
                    captured = new OnCapturePointCaptured_E(); 
                    pointId = 4; 
                    break;
                default:
                    _logger.Warn($"Announcement with Id {point} doesn't exist! Please use letters between A and E");
                    return;
            }

            var capArgs = (ArgsCapturePoint)captured;
            capArgs.CapturePoint = pointId;
            capArgs.OtherNetID = turret.NetId;

            AttackableUnit source = captor != null ? (AttackableUnit)captor : turret;
            _game.PacketNotifier.NotifyOnEvent(captured, source);
        }

        public static void AnnounceCapturePointNeutralized(Minion turret, char point)
        {
            IEvent neutralized;
            uint pointId = 0;

            switch (char.ToUpper(point))
            {
                case 'A': 
                    neutralized = new OnCapturePointNeutralized_A(); 
                    pointId = 0; 
                    break;
                case 'B': 
                    neutralized = new OnCapturePointNeutralized_B(); 
                    pointId = 1; 
                    break;
                case 'C': 
                    neutralized = new OnCapturePointNeutralized_C(); 
                    pointId = 2; 
                    break;
                case 'D': 
                    neutralized = new OnCapturePointNeutralized_D(); 
                    pointId = 3; 
                    break;
                case 'E': 
                    neutralized = new OnCapturePointNeutralized_E(); 
                    pointId = 4; 
                    break;
                default:
                    _logger.Warn($"Announcement with Id {point} doesn't exist! Please use letters between A and E");
                    return;
            }

            var neutArgs = (ArgsCapturePoint)neutralized;
            neutArgs.CapturePoint = pointId;
            neutArgs.OtherNetID = turret.NetId;

            _game.PacketNotifier.NotifyS2C_OnEventWorld(neutralized);
        }
        public static void AnnounceChampionAscended(Champion champion)
        {
            _game.PacketNotifier.NotifyS2C_OnEventWorld(new OnChampionAscended() { OtherNetID = champion.NetId }, champion);
        }

        public static void AnnounceClearAscended()
        {
            _game.PacketNotifier.NotifyS2C_OnEventWorld(new OnClearAscended());
            ApiMapFunctionManager.NotifyAscendant();
        }

        public static void AnnounceKillDragon(DeathData data)
        {
            var killDragon = new OnKillDragon()
            {
                //TODO: Figure out all the parameters, their values look random(?).
                //All Map11 replays have the same values in this event besides OtherNetId.
                OtherNetID = data.Unit.NetId
            };
            _game.PacketNotifier.NotifyS2C_OnEventWorld(killDragon, data.Killer);
        }

        public static void AnnounceKillWorm(DeathData data)
        {
            var killDragon = new OnKillWorm()
            {
                //TODO: Figure out all the parameters, their values look random(?).
                OtherNetID = data.Unit.NetId
            };
            _game.PacketNotifier.NotifyS2C_OnEventWorld(killDragon, data.Killer);
        }

        public static void AnnounceKillSpiderBoss(DeathData data)
        {
            var killDragon = new OnKillSpiderBoss()
            {
                //Couldn't find a replay with this event, but i assume it should follow the same logic as the other 2.
                OtherNetID = data.Unit.NetId
            };
            _game.PacketNotifier.NotifyS2C_OnEventWorld(killDragon, data.Killer);
        }

        public static void AnnounceMinionAscended(Minion minion)
        {
            _game.PacketNotifier.NotifyS2C_OnEventWorld(new OnMinionAscended() { OtherNetID = minion.NetId }, minion);
        }

        public static void AnnounceMinionsSpawn()
        {
            _game.PacketNotifier.NotifyS2C_OnEventWorld(new OnMinionsSpawn());
        }

        public static void AnnouceNexusCrystalStart()
        {
            _game.PacketNotifier.NotifyS2C_OnEventWorld(new OnNexusCrystalStart());
        }

        public static void AnnounceStartGameMessage(int message, int map = 0)
        {
            IEvent annoucement;
            switch (message)
            {
                case 1:
                    annoucement = new OnStartGameMessage1();
                    break;
                case 2:
                    annoucement = new OnStartGameMessage2();
                    break;
                case 3:
                    annoucement = new OnStartGameMessage3();
                    break;
                case 4:
                    annoucement = new OnStartGameMessage4();
                    break;
                case 5:
                    annoucement = new OnStartGameMessage5();
                    break;
                default:
                    _logger.Warn($"Announcement with Id {message} doesn't exist! Please use numbers between 1 and 5");
                    return;
            }
            (annoucement as ArgsGlobalMessageGeneric).MapNumber = map;

            _game.PacketNotifier.NotifyS2C_OnEventWorld(annoucement);
        }

        public static void AnnounceVictoryPointThreshold(LaneTurret turret, int index)
        {
            IEvent pointThreshHold;
            switch (index)
            {
                case 1:
                    pointThreshHold = new OnVictoryPointThreshold1();
                    break;
                case 2:
                    pointThreshHold = new OnVictoryPointThreshold2();
                    break;
                case 3:
                    pointThreshHold = new OnVictoryPointThreshold3();
                    break;
                case 4:
                    pointThreshHold = new OnVictoryPointThreshold4();
                    break;
                default:
                    _logger.Warn($"Announcement with Id {index} doesn't exist! Please use numbers between 1 and 5");
                    return;
            }

            _game.PacketNotifier.NotifyOnEvent(pointThreshHold, turret);
        }
    }
}
