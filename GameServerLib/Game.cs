using GameServerCore.Enums;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Chatbox;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Packets;
using LeagueSandbox.GameServer.Players;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net;
using LeagueSandbox.GameServer.Inventory;
using PacketDefinitions420;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Timer = System.Timers.Timer;
using LeagueSandbox.GameServer.Packets.PacketHandlers;
using LeagueSandbox.GameServer.Handlers;
using GameServerLib.Handlers;
using GameServerCore.Packets.PacketDefinitions;
using GameServerCore.Packets.PacketDefinitions.Requests;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Quests;
using System.Threading;

namespace LeagueSandbox.GameServer
{
    /// <summary>
    /// Class that contains and manages all qualities of the game such as managers for networking and game mechanics, as well as the starting, pausing, and stopping of the game.
    /// </summary>
    public class Game
    {
        // Crucial Game Vars
        private PacketServer _packetServer;
        private List<GameScriptTimer> _gameScriptTimers;

        // Function Vars
        private static ILog _logger = LoggerProvider.GetLogger();
        private float _nextSyncTime = 10 * 1000;
        protected double RefreshRate =>
            Config.GameFeatures.HasFlag(FeatureFlags.EnableTournamentMode)
                ? 1000.0 / 60.0
                : 1000.0 / 30.0; // GameLoop called either 30 times (normal mode) or 60 times (tournament mode) a second.
        private HandleStartGame _gameStartHandler;

        // Server

        /// <summary>
        /// Whether the server is running or not. Usually true after the network loop has started via GameServerLauncher.
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// Whether or not the game has been paused (via a chat command usually).
        /// </summary>
        public bool IsPaused { get; set; }
        /// <summary>
        /// Time until the game unpauses (if paused).
        /// </summary>
        public long PauseTimeLeft { get; private set; }
        /// <summary>
        /// Whether or not the game is set as finished (and thus whether the server should close).
        /// </summary>
        public bool SetToExit { get; set; }

        // Networking

        /// <summary>
        /// Time since the game has started. Mostly used for networking to sync up players with the server.
        /// </summary>
        public float GameTime { get; private set; }
        /// <summary>
        /// Handler for request packets sent by game clients.
        /// </summary>
        public NetworkHandler<ICoreRequest> RequestHandler { get; }
        /// <summary>
        /// Handler for response packets sent by the server to game clients.
        /// </summary>
        public NetworkHandler<ICoreRequest> ResponseHandler { get; }
        /// <summary>
        /// Interface containing all function related packets (except handshake) which are sent by the server to game clients.
        /// </summary>
        public PacketNotifier PacketNotifier { get; private set; }

        // Game

        /// <summary>
        /// Interface containing all (public) functions used by ObjectManager. ObjectManager manages GameObjects, their properties, and their interactions such as being added, removed, colliding with other objects or terrain, vision, teams, etc.
        /// </summary>
        public ObjectManager ObjectManager { get; private set; }
        /// <summary>
        /// Interface for all protection related functions.
        /// Protection is a mechanic which determines whether or not a unit is targetable.
        /// </summary>
        public ProtectionManager ProtectionManager { get; private set; }
        /// <summary>
        /// Contains all map related game settings such as collision handler, navigation grid, announcer events, and map properties. Doubles as a Handler/Manager for all MapScripts.
        /// </summary>
        public MapScriptHandler Map { get; private set; }
        /// <summary>
        /// Class containing all information about the game's configuration such as game content location, map spawn points, whether cheat commands are enabled, etc.
        /// </summary>
        public Config Config { get; protected set; }
        /// <summary>
        /// Class which manages items of players.
        /// </summary>
        public ItemManager ItemManager { get; private set; }
        /// <summary>
        /// Class which manages all chat based commands.
        /// </summary>
        internal ChatCommandManager ChatCommandManager { get; private set; }
        /// <summary>
        /// Interface of functions used to identify players or their properties (such as their champion).
        /// </summary>
        public PlayerManager PlayerManager { get; private set; }
        /// <summary>
        /// Manager for all unique identifiers used by GameObjects.
        /// </summary>
        internal NetworkIdManager NetworkIdManager { get; private set; }
        /// <summary>
        /// Class that compiles and loads all scripts which will be used for the game (ex: spells, items, AI, maps, etc).
        /// </summary>
        internal CSharpScriptEngine ScriptEngine { get; private set; }

        internal FileSystemWatcher ScriptsHotReloadWatcher { get; private set; }
        public QuestManager QuestManager { get; private set; }

        /// <summary>
        /// Instantiates all game managers and handlers.
        /// </summary>
        public Game()
        {
            ItemManager = new ItemManager();
            ChatCommandManager = new ChatCommandManager(this);
            NetworkIdManager = new NetworkIdManager();
            PlayerManager = new PlayerManager(this);
            ScriptEngine = new CSharpScriptEngine();
            RequestHandler = new NetworkHandler<ICoreRequest>();
            ResponseHandler = new NetworkHandler<ICoreRequest>();
            QuestManager = new QuestManager(this);
        }

        /// <summary>
        /// Sets up all managers and config specific settings like players.
        /// </summary>
        /// <param name="config">Game configuration file. Usually from GameInfo.json.</param>
        /// <param name="server">Server networking instance.</param>
        public void Initialize(Config config, PacketServer server)
        {
            _logger.Info("Loading Config.");
            Config = config;
            Config.LoadContent(this);
            _gameScriptTimers = new List<GameScriptTimer>();

            // CPU profiler. No-op when disabled in GameInfo.json.
            Profiler.Init(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
                Config.ProfilerEnabled);
            if (Config.ProfilerEnabled)
            {
                _logger.Info("CPU profiler enabled — trace will be written to logs/profile_<timestamp>.json on shutdown.");
            }

            ChatCommandManager.LoadCommands();

            Map = new MapScriptHandler(this);

            // TODO: GameApp should send the Response/Request handlers
            _packetServer = server;
            // TODO: switch the notifier with ResponseHandler
            PacketNotifier = new PacketNotifier(_packetServer.PacketHandlerManager, Map.NavigationGrid);

            ObjectManager = new ObjectManager(this);
            ProtectionManager = new ProtectionManager(this);
            ApiGameEvents.SetGame(this);
            ApiMapFunctionManager.SetGame(this, Map as MapScriptHandler);
            ApiFunctionManager.SetGame(this);
            ApiEventManager.SetGame(this);
            ChampionDeathHandler.Init(this);
            IsRunning = false;

            Map.Init();

            PauseTimeLeft = 30 * 60; // 30 minutes

            InitializePacketHandlers();

            _logger.Info("Add players");
            foreach (var p in Config.Players)
            {
                _logger.Info("Player " + p.Name + " Added: " + p.Champion);
                PlayerManager.AddPlayer(p);
            }
            QuestManager.Initialize();
            _logger.Info("Game is ready.");
        }

        /// <summary>
        /// Registers Request Handlers for each request packet.
        /// </summary>
        public void InitializePacketHandlers()
        {
            // maybe use reflection, the problem is that Register is generic and so it needs to know its type at
            // compile time, maybe just use interface and in runetime figure out the type - and again there is
            // a problem with passing generic delegate to non-generic function, if we try to only constraint the
            // argument to interface ICoreRequest we will get an error cause our generic handlers use generic type
            // even with where statement that doesn't work
            RequestHandler.Register<AttentionPingRequest>(new HandleAttentionPing(this).HandlePacket);
            RequestHandler.Register<AutoAttackOptionRequest>(new HandleAutoAttackOption(this).HandlePacket);
            RequestHandler.Register<BlueTipClickedRequest>(new HandleBlueTipClicked(this).HandlePacket);
            RequestHandler.Register<BuyItemRequest>(new HandleBuyItem(this).HandlePacket);
            RequestHandler.Register<CastSpellRequest>(new HandleCastSpell(this).HandlePacket);
            RequestHandler.Register<ChatMessageRequest>(new HandleChatBoxMessage(this).HandlePacket);
            RequestHandler.Register<ClickRequest>(new HandleClick(this).HandlePacket);
            RequestHandler.Register<SpellChargeUpdateReq>(new HandleSpellChargeUpdateReq(this).HandlePacket);
            RequestHandler.Register<EmotionPacketRequest>(new HandleEmotion(this).HandlePacket);
            RequestHandler.Register<ExitRequest>(new HandleExit(_packetServer.PacketHandlerManager).HandlePacket);
            RequestHandler.Register<SyncSimTimeRequest>(new HandleSyncSimTime(this).HandlePacket);
            RequestHandler.Register<PingLoadInfoRequest>(new HandleLoadPing(this).HandlePacket);
            RequestHandler.Register<LockCameraRequest>(new HandleLockCamera(this).HandlePacket);
            RequestHandler.Register<JoinTeamRequest>(new HandleJoinTeam(this).HandlePacket);
            RequestHandler.Register<MovementRequest>(new HandleMove(this).HandlePacket);
            RequestHandler.Register<OnShopOpenedRequest>(new HandleOnShopOpened(this).HandlePacket);
            RequestHandler.Register<UndoItemRequest>(new HandleUndoItem(this).HandlePacket);
            RequestHandler.Register<MoveConfirmRequest>(new HandleMoveConfirm(this).HandlePacket);
            RequestHandler.Register<PauseRequest>(new HandlePauseReq(this).HandlePacket);
            RequestHandler.Register<QueryStatusRequest>(new HandleQueryStatus(this).HandlePacket);
            RequestHandler.Register<QuestClickedRequest>(new HandleQuestClicked(this).HandlePacket);
            RequestHandler.Register<ScoreboardRequest>(new HandleScoreboard(this).HandlePacket);
            RequestHandler.Register<SellItemRequest>(new HandleSellItem(this).HandlePacket);
            RequestHandler.Register<UpgradeSpellReq>(new HandleUpgradeSpellReq(this).HandlePacket);
            RequestHandler.Register<SpawnRequest>(new HandleSpawn(this).HandlePacket);

            _gameStartHandler = new HandleStartGame(this);
            RequestHandler.Register<StartGameRequest>(_gameStartHandler.HandlePacket);

            RequestHandler.Register<ReplicationConfirmRequest>(new HandleStatsConfirm(this).HandlePacket);
            RequestHandler.Register<SurrenderRequest>(new HandleSurrender(this).HandlePacket);
            RequestHandler.Register<SwapItemsRequest>(new HandleSwapItems(this).HandlePacket);
            RequestHandler.Register<SynchVersionRequest>(new HandleSync(this).HandlePacket);
            RequestHandler.Register<UnpauseRequest>(new HandleUnpauseReq(this).HandlePacket);
            RequestHandler.Register<UseObjectRequest>(new HandleUseObject(this).HandlePacket);
            RequestHandler.Register<ViewRequest>(new HandleView(this).HandlePacket);
        }

        public void TryFinishReconnectStart(int userId)
        {
            var player = PlayerManager.GetPeerInfo(userId);
            _gameStartHandler?.TryFinishReconnect(player);
        }

        /// <summary>
        /// Enables or disables the hot reloading of scripts. Used only for development.
        /// </summary>
        public void EnableHotReload(bool status)
        {
            string scriptsPath = Config.ContentManager.ContentPath;

            void ScriptsChanged(object _, FileSystemEventArgs ea)
            {
                // Disable raising events to avoid triggering LoadScripts() many times in a row after the first event
                ScriptsHotReloadWatcher.EnableRaisingEvents = false;
                ChatCommandManager.SendDebugMsgFormatted(DebugMsgType.INFO, LoadScripts() ? "Scripts reloaded." : "Scripts failed to reload.");
                ScriptsHotReloadWatcher.EnableRaisingEvents = true;
            }

            if (status && ScriptsHotReloadWatcher == null)
            {
                ScriptsHotReloadWatcher = new FileSystemWatcher
                {
                    Path = scriptsPath,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite,
                    Filter = "*.*",
                };
                ScriptsHotReloadWatcher.Changed += ScriptsChanged;
            }
            else
            {
                ScriptsHotReloadWatcher.Changed -= ScriptsChanged;
                ScriptsHotReloadWatcher = null;
            }
        }

        /// <summary>
        /// Loads the scripts contained in every content package.
        /// </summary>
        /// <returns>Whether all scripts were loaded successfully or not.</returns>
        public bool LoadScripts()
        {
            bool scriptLoadingResults = Config.ContentManager.LoadScripts();

            if (scriptLoadingResults)
            {
                foreach (var unit in ObjectManager.GetObjects().Values)
                {
                    if (unit is ObjAIBase obj)
                    {
                        if (obj.Spells.ContainsKey((int)SpellSlotType.PassiveSpellSlot))
                        {
                            obj.LoadCharScript(obj.Spells[(int)SpellSlotType.PassiveSpellSlot]);
                        }
                        else
                        {
                            obj.LoadCharScript();
                        }
                        obj.GetBuffs().ForEach(buff => buff.LoadScript());
                        obj.Spells.Values.ToList().ForEach(spell => spell.LoadScript());
                    }
                }
            }

            return scriptLoadingResults;
        }

        public bool CheckIfAllPlayersLeft()
        {
            var players = PlayerManager.GetPlayers(false);
            // The number of those who are disconnected and not even loads.
            var count = players.Count(p => !p.IsStartedClient && p.IsDisconnected);
            Console.WriteLine($"The number of disconnected players {count}/{players.Count}");
            if(count == players.Count)
            {
                _logger.Info("All players have left the server. It's lonely here :(");
                SetToExit = true;
                // Notify whatever match-coordinator is watching (if any). The
                // event is no-op when no subscriber is wired (legacy/standalone
                // launches). See LeagueSandbox.GameServer.Networking for the
                // wire-up; coordinators implement the gameserver_control.proto
                // schema to receive this as a MatchEnded message.
                MatchEnded?.Invoke(MatchEndCause.AllPlayersDisconnected, /*winningTeam*/ 0);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reasons the match might have concluded. Mirrors the categories
        /// in <c>Networking/Protobuf/gameserver_control.proto</c>'s
        /// MatchEnded.Reason enum, but kept as a plain C# enum here so
        /// non-coordinator code (logs, future replay metadata, etc) can
        /// reuse it without taking a hard dependency on the protobuf type.
        /// </summary>
        public enum MatchEndCause
        {
            Unspecified            = 0,
            AllPlayersDisconnected = 1,
            TeamSurrender          = 2,
            NexusDestroyed         = 3,
            TimeLimitReached       = 4,
            ShutdownRequested      = 5,
            InternalError          = 6,
        }

        /// <summary>
        /// Fired exactly once when the match ends, regardless of cause.
        /// The second argument is the winning team (0 = none/draw, 1 = blue,
        /// 2 = purple). Subscribers (e.g. the coordinator client wired up
        /// in Server.cs) MUST tolerate being called from any thread.
        /// </summary>
        public event Action<MatchEndCause, int>? MatchEnded;

        /// <summary>
        /// Function which initiates ticking of the game's logic.
        /// </summary>
        public void GameLoop()
        {
            double refreshRate = RefreshRate;
            double timeout = 0;

            Stopwatch lastMapDurationWatch = new Stopwatch();

            bool wasNotPaused = true;
            bool firstCycle = true;

            float timeToForcedStart = Config.ForcedStart;

            // Kick off the dedicated I/O thread. From this point onwards, all
            // ENet polling and outbound sends happen on that thread; the game
            // thread interacts with the network only via the bridge queues.
            _packetServer.StartNetThread();

            // Name the thread so the profiler labels it usefully in Perfetto.
            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
            {
                Thread.CurrentThread.Name = "GameLoop";
            }

            long tickNumber = 0;
            while (!SetToExit)
            {
                // Numbered so Perfetto doesn't visually coalesce adjacent
                // same-name slices into one giant bar.
                using var _tickScope = Profiler.Scope($"Tick {tickNumber++}");

                double lastSleepDuration = lastMapDurationWatch.Elapsed.TotalMilliseconds;
                lastMapDurationWatch.Restart();

                // Drain everything the net thread has produced since the last
                // tick. Done at the very top so the rest of the tick sees a
                // consistent view of the world (including any packet handlers
                // that mutate game state).
                using (Profiler.Scope("DrainInboundEvents", "network"))
                {
                    DrainInboundEvents();
                }
                
                float deltaTime = (float)lastSleepDuration;
                if(firstCycle)
                {
                    firstCycle = false;
                    // To avoid Update(0)
                    deltaTime = (float)refreshRate;
                }

                if (IsPaused)
                {
                    if (wasNotPaused)
                    {
                        refreshRate = 1000.0;
                        wasNotPaused = false;
                    }
                    else
                    {
                        PauseTimeLeft--;
                        if (PauseTimeLeft <= 0)
                        {
                            //TODO: fix these
                            //PacketNotifier.NotifyUnpauseGame();

                            // Pure water framing
                            var players = PlayerManager.GetPlayers();
                            var unpauser = players[0].Champion;
                            foreach (var player in players)
                            {
                                PacketNotifier.NotifyResumePacket(unpauser, player, false);
                            }
                            Unpause();
                        }
                    }
                }

                if (!IsPaused)
                {
                    refreshRate = RefreshRate;
                    wasNotPaused = true;

                    if(!IsRunning && timeToForcedStart > 0)
                    {
                        if(timeToForcedStart <= deltaTime && !CheckIfAllPlayersLeft())
                        {
                            _logger.Info($"Patience is over. The game will start earlier.");
                            _gameStartHandler.ForceStart();
                        }
                        timeToForcedStart -= deltaTime;
                    }

                    if (IsRunning)
                    {
                        using (Profiler.Scope("Game.Update"))
                        {
                            Update(deltaTime);
                        }
                    }
                }

                double lastUpdateDuration = lastMapDurationWatch.Elapsed.TotalMilliseconds;

                double overshoot = Math.Max(0, lastSleepDuration - refreshRate);
                timeout = Math.Max(0, refreshRate - lastUpdateDuration - overshoot);

                if (timeout > 0)
                {
                    Thread.Sleep((int)timeout);
                }
            }

            _packetServer.StopNetThread();

            // Flush the CPU trace to disk. Safe to call even when the profiler
            // was disabled.
            Profiler.Shutdown();
        }

        // Drains all events the net thread has queued for the game thread:
        // converted request packets and disconnect notifications. Runs once
        // at the top of every tick so handler-induced state changes are
        // visible to Update(diff) immediately afterwards.
        private void DrainInboundEvents()
        {
            var bridge = _packetServer.Bridge;
            while (bridge.Inbound.TryDequeue(out var ev))
            {
                switch (ev)
                {
                    case InboundRequest r:
                        try
                        {
                            _packetServer.PacketHandlerManager.DispatchInboundRequest(r.ClientId, r.Request);
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"Inbound request dispatch failed for client {r.ClientId}: {e}");
                        }
                        break;
                    case InboundDisconnect d:
                        _packetServer.PacketHandlerManager.ProcessDisconnectOnGameThread(d.ClientId);
                        break;
                }
            }
        }

        public float SetCurrentMinutes(float newMinutes)
        {
            // Convert minutes to milliseconds
            GameTime = (long)(newMinutes * 1000f * 60f);
            return GameTime;
        }

        public float GetCurrentMinutes()
        {
            // Convert milliseconds to minutes
            float minutes = (float)(GameTime / (1000f * 60f));
            return minutes;
        }

        /// <summary>
        /// Function called every tick of the game.
        /// </summary>
        /// <param name="diff">Number of milliseconds since this tick occurred.</param>
        public void Update(float diff)
        {
            // This section dictates the priority of updates.
            GameTime += diff;
            // Collision
            using (Profiler.Scope("Map.Update"))
            {
                Map.Update(diff);
            }
            // Objects
            using (Profiler.Scope("ObjectManager.Update"))
            {
                ObjectManager.Update(diff);
            }
            // Protection (TODO: Move this into ObjectManager).
            using (Profiler.Scope("ProtectionManager.Update"))
            {
                ProtectionManager.Update(diff);
            }
            using (Profiler.Scope("ChatCommands.Update"))
            {
                ChatCommandManager.GetCommands().ForEach(command => command.Update(diff));
            }
            using (Profiler.Scope("GameScriptTimers.Update", "scripts"))
            {
                // Tick a snapshot: a callback may register further timers (added to the live list,
                // ticked next frame) or spawn objects — iterating the live list would throw
                // "Collection was modified".
                foreach (var gsTimer in _gameScriptTimers.ToArray())
                {
                    gsTimer.Update(diff);
                }
                _gameScriptTimers.RemoveAll(gsTimer => gsTimer.IsDead());
            }

            // By default, synchronize the game time between server and clients every 10 seconds
            _nextSyncTime += diff;
            if (_nextSyncTime >= 10 * 1000)
            {
                PacketNotifier.NotifySynchSimTimeS2C(GameTime);
                _nextSyncTime = 0;
            }
        }

        /// <summary>
        /// Adds a timer to the list of timers so that it ticks with the game.
        /// </summary>
        /// <param name="timer">Timer instance.</param>
        public void AddGameScriptTimer(GameScriptTimer timer)
        {
            _gameScriptTimers.Add(timer);
        }

        /// <summary>
        /// Removes a timer from the list of timers which causes it to become inactive.
        /// </summary>
        /// <param name="timer">Timer instance.</param>
        public void RemoveGameScriptTimer(GameScriptTimer timer)
        {
            _gameScriptTimers.Remove(timer);
        }

        /// <summary>Game-loop ticks per second at the current refresh rate (30 or 60).</summary>
        public double TicksPerSecond => 1000.0 / RefreshRate;

        /// <summary>
        /// Function to set the game as running. Allows the game loop to start.
        /// </summary>
        public void Start()
        {
            IsRunning = true;
            try
            {
                Map.MapScript.OnMatchStart();
            }
            catch(Exception e)
            {
                _logger.Error(null, e);
            }
        }

        /// <summary>
        /// Function to set the game as not running. Prevents the game loop from continuing.
        /// </summary>
        public void Stop()
        {
            IsRunning = false;
        }

        /// <summary>
        /// Temporarily prevents the game loop from continuing and notifies players.
        /// </summary>
        public void Pause()
        {
            if (PauseTimeLeft <= 0)
            {
                return;
            }
            IsPaused = true;
            foreach (var player in PlayerManager.GetPlayers(false))
            {
                PacketNotifier.NotifyPausePacket(player, (int)PauseTimeLeft, true);
            }
        }

        /// <summary>
        /// Releases the game loop from a temporary pause.
        /// </summary>
        public void Unpause()
        {
            IsPaused = false;
        }

        /// <summary>
        /// Unused function meant to get the instances of a specific type who rely on Game as a parameter.
        /// </summary>
        /// <returns>List of instances of type T.</returns>
        private static List<T> GetInstances<T>(Game g)
        {
            return Assembly.GetCallingAssembly()
                .GetTypes()
                .Where(t => t.BaseType == typeof(T))
                .Select(t => (T)Activator.CreateInstance(t, g)).ToList();
        }

        /// <summary>
        /// Prepares to close the Game 10 seconds after being called.
        /// </summary>
        public void SetGameToExit()
        {
            _logger.Info("Game is over. Game Server will exit in 10 seconds.");
            var timer = new Timer(10000) { AutoReset = false };
            timer.Elapsed += (a, b) => SetToExit = true;
            timer.Start();
        }
    }
}
