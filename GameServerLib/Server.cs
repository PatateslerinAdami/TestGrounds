using GameServerCore.Packets.Handlers;
using GameServerCore.Packets.PacketDefinitions;
using GameserverControl;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Networking;
using log4net;
using PacketDefinitions420;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace LeagueSandbox.GameServer
{
    /// <summary>
    /// Class which controls the starting of the game and network loops.
    /// </summary>
    internal class Server : IDisposable
    {
        private string[] _blowfishKeys;
        private string _serverVersion = "1.0.0";
        private static ILog _logger = LoggerProvider.GetLogger();
        private Game _game;
        private Config _config;
        private ushort _serverPort { get; }

        // Coordinator client. Null when the GameServer is launched in
        // legacy/standalone mode (the JSON config omits the
        // `coordinatorChannel` object — see Config.LoadConfig). The wire
        // protocol is defined in Networking/Protobuf/gameserver_control.proto.
        private CoordinatorClient _coordClient;

        // Wall-clock anchor for MatchEnded.duration_seconds. Stamped when
        // the GameServer reports Ready to the coordinator (which is the
        // moment from the coordinator's perspective that the match begins).
        private Stopwatch _matchClock;

        /// <summary>
        /// Initialize base variables for future usage.
        /// </summary>
        public Server(Game game, ushort port, string configJson)
        {
            _game = game;
            _serverPort = port;
            _config = Config.LoadFromJson(game, configJson);

            _blowfishKeys = new string[_config.Players.Count];
            for(int i = 0; i < _config.Players.Count; i++)
            {
                _blowfishKeys[i] = _config.Players[i].BlowfishKey;
            }
        }

        /// <summary>
        /// Called upon the Program successfully initializing GameServerLauncher.
        /// </summary>
        public void Start()
        {
            var build = $"League Sandbox Build {ServerContext.BuildDateString}";
            var packetServer = new PacketServer();

            Console.Title = build;

            _logger.Debug(build);
            _logger.Debug($"Bloodwell {_serverVersion}");
            _logger.Info($"Game started on port: {_serverPort}");

            packetServer.InitServer(_serverPort, _blowfishKeys, _game, _game.RequestHandler, _game.ResponseHandler);
            _game.Initialize(_config, packetServer);

            // Coordinator handshake. Done AFTER InitServer so by the time the
            // coordinator receives Ready{}, the ENet UDP socket is genuinely
            // bound and accepting client handshakes — closing the proxy/client
            // dead-window race that motivated this whole channel.
            //
            // The coordinator endpoint is supplied via the top-level
            // `coordinatorChannel` object in GameInfo.json (parsed into
            // `_config.CoordinatorChannel` in Config.LoadConfig). When the
            // realm runs in legacy mode it omits the object entirely; the
            // GameServer then runs as before with no outbound TCP attempt.
            //
            // A coordinator-side failure (refused connect, timeout, etc) is
            // logged but never aborts the server: a misconfigured coordinator
            // must not break otherwise-working matches.
            var coordConfig = _config.CoordinatorChannel;
            if (coordConfig != null && coordConfig.IsValid)
            {
                _coordClient = new CoordinatorClient(
                    coordConfig.Host,
                    coordConfig.Port,
                    coordConfig.MatchId,
                    _serverVersion);

                _coordClient.ShutdownRequested += OnCoordinatorShutdownRequested;
                _coordClient.ConnectionLost   += OnCoordinatorConnectionLost;

                // Bridge: Game's match-end event → MatchEnded protobuf frame.
                _game.MatchEnded += OnGameMatchEnded;

                try
                {
                    _matchClock = Stopwatch.StartNew();
                    _coordClient.ConnectAndSendReady(_serverPort);
                }
                catch (Exception e)
                {
                    _logger.Warn($"[Coordinator] Failed to connect / send Ready: {e.Message}. " +
                                 "Continuing in standalone mode.");
                    _coordClient.Dispose();
                    _coordClient = null;
                    _matchClock = null;
                }
            }
        }

        /// <summary>
        /// Called after the Program has finished setting up the Server for players to join.
        /// Blocks until the game loop exits, then disposes the coordinator client
        /// so its TCP socket closes cleanly before the process tears down.
        /// </summary>
        public void StartNetworkLoop()
        {
            try
            {
                _game.GameLoop();
            }
            finally
            {
                // Dispose here (not just from Program.Main) so the coordinator
                // sees a clean socket close immediately after the match ends,
                // rather than whenever process teardown happens to flush the FD.
                Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Tears down the coordinator client (which closes its socket cleanly,
        /// which the coordinator interprets as either a clean exit if MatchEnded
        /// was already sent, or as a crash if it wasn't).
        /// </summary>
        public void Dispose()
        {
            _coordClient?.Dispose();
            // PathNode.DestroyTable();
        }

        // ── Coordinator bridge ─────────────────────────────────────────────

        private void OnGameMatchEnded(Game.MatchEndCause cause, int winningTeam)
        {
            if (_coordClient == null) return;

            var reason = cause switch
            {
                Game.MatchEndCause.AllPlayersDisconnected => MatchEnded.Types.Reason.AllPlayersDisconnected,
                Game.MatchEndCause.TeamSurrender          => MatchEnded.Types.Reason.TeamSurrender,
                Game.MatchEndCause.NexusDestroyed         => MatchEnded.Types.Reason.NexusDestroyed,
                Game.MatchEndCause.TimeLimitReached       => MatchEnded.Types.Reason.TimeLimitReached,
                Game.MatchEndCause.ShutdownRequested      => MatchEnded.Types.Reason.ShutdownRequested,
                Game.MatchEndCause.InternalError          => MatchEnded.Types.Reason.InternalError,
                _                                         => MatchEnded.Types.Reason.Unspecified,
            };

            int durationSeconds = _matchClock != null
                ? (int)_matchClock.Elapsed.TotalSeconds
                : 0;

            _coordClient.SendMatchEnded(reason, durationSeconds, winningTeam);
        }

        private void OnCoordinatorShutdownRequested(string reason)
        {
            _logger.Info($"[Coordinator] Shutdown requested ({reason}). Marking game for exit.");
            // Best we can cleanly do from outside the game thread: ask Game
            // to terminate at its next loop iteration. Game will then fire
            // MatchEnded(InternalError or AllPlayersDisconnected depending on
            // path); we proactively send SHUTDOWN_REQUESTED first so the
            // coordinator gets the right cause regardless.
            int durationSeconds = _matchClock != null
                ? (int)_matchClock.Elapsed.TotalSeconds
                : 0;
            _coordClient?.SendMatchEnded(
                MatchEnded.Types.Reason.ShutdownRequested,
                durationSeconds,
                /*winningTeam*/ 0,
                reason);
            _game.SetToExit = true;
        }

        private void OnCoordinatorConnectionLost(Exception ex)
        {
            // Players currently in the match are unaffected; they keep playing
            // until the natural game-end paths fire. We just lose the ability
            // to send further frames.
            if (ex != null)
                _logger.Info($"[Coordinator] Control channel lost: {ex.Message}. " +
                             "Match continues; further coordinator messages will be silently dropped.");
            else
                _logger.Info("[Coordinator] Control channel closed cleanly.");
        }
    }
}
