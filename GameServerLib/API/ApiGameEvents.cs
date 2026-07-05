using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
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

        public static void AnnounceCapturePointCaptured(LaneTurret turret, char point, Champion captor = null)
        {
            IEvent captured;
            switch (char.ToUpper(point))
            {
                case 'A':
                    captured = new OnCapturePointCaptured_A();
                    break;
                case 'B':
                    captured = new OnCapturePointCaptured_B();
                    break;
                case 'C':
                    captured = new OnCapturePointCaptured_C();
                    break;
                case 'D':
                    captured = new OnCapturePointCaptured_D();
                    break;
                case 'E':
                    captured = new OnCapturePointCaptured_E();
                    break;
                default:
                    _logger.Warn($"Announcement with Id {point} doesn't exist! Please use numbers between 1 and 5");
                    return;
            }

            if (captor != null)
            {
                captured.OtherNetID = captor.NetId;
            }

            _game.PacketNotifier.NotifyOnEvent(captured, turret);
        }

        public static void AnnounceCapturePointNeutralized(LaneTurret turret, char point)
        {
            IEvent neutralized;
            switch (char.ToUpper(point))
            {
                case 'A':
                    neutralized = new OnCapturePointNeutralized_A();
                    break;
                case 'B':
                    neutralized = new OnCapturePointNeutralized_B();
                    break;
                case 'C':
                    neutralized = new OnCapturePointNeutralized_C();
                    break;
                case 'D':
                    neutralized = new OnCapturePointNeutralized_D();
                    break;
                case 'E':
                    neutralized = new OnCapturePointNeutralized_E();
                    break;
                default:
                    _logger.Warn($"Announcement with Id {point} doesn't exist! Please use numbers between 1 and 5");
                    return;
            }

            _game.PacketNotifier.NotifyOnEvent(neutralized, turret);
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

        /// <summary>
        /// Fills the shared ArgsMinionKill payload of the epic-monster kill announces
        /// (OnKillDragon/OnKillWorm/OnKillSpiderBoss). Replay-verified (4.20, 3x OnKillDragon +
        /// 1x OnKillWorm): OtherNetID = monster, GoldGiven = 0, assist list = champions with an
        /// active assist marker on the monster (killer never included), MinionSkinNameHash =
        /// lowercase-SDBM hash of the monster model (SRU_Dragon = 0x694958FC, SRU_Baron =
        /// 0x68AC12C9), MinionSkinID and MinionMapSideTeamID = 0 (neutral).
        /// </summary>
        private static void FillEpicMonsterKillEvent(ArgsMinionKill killEvent, DeathData data)
        {
            killEvent.OtherNetID = data.Unit.NetId;
            killEvent.GoldGiven = 0.0f;
            var assists = data.Unit.GetEnemyChampionAssists(data.Killer);
            killEvent.AssistCount = assists.Count;
            for (int i = 0; i < assists.Count && i < killEvent.Assists.Length; i++)
            {
                killEvent.Assists[i] = assists[i].NetId;
            }
            killEvent.MinionSkinNameHash = GameServerCore.Content.HashFunctions.HashStringNorm(data.Unit.Model);
            killEvent.MinionSkinID = data.Unit is ObjAIBase ai ? ai.SkinID : 0;
            killEvent.MinionMapSideTeamID = 0;
        }

        public static void AnnounceKillDragon(DeathData data)
        {
            var killDragon = new OnKillDragon();
            FillEpicMonsterKillEvent(killDragon, data);
            _game.PacketNotifier.NotifyS2C_OnEventWorld(killDragon, data.Killer);
        }

        public static void AnnounceKillWorm(DeathData data)
        {
            var killWorm = new OnKillWorm();
            FillEpicMonsterKillEvent(killWorm, data);
            _game.PacketNotifier.NotifyS2C_OnEventWorld(killWorm, data.Killer);
        }

        public static void AnnounceKillSpiderBoss(DeathData data)
        {
            // No replay with this event captured; assumed to follow the dragon/worm shape.
            var killSpiderBoss = new OnKillSpiderBoss();
            FillEpicMonsterKillEvent(killSpiderBoss, data);
            _game.PacketNotifier.NotifyS2C_OnEventWorld(killSpiderBoss, data.Killer);
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
