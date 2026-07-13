using System.Numerics;
using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    /// <summary>
    /// Receives C2S_UnitSendDrawPath (0x106) — the cursor-path points the client streams while
    /// draw-path mode is active on its champion (see NotifyUnitSetDrawPathMode / the !drawpath
    /// command). Riot-side this fed an internal dev tool (never seen in live 4.20 replays); we
    /// visualize each received point with a short debug circle so the drawn stroke paints
    /// in-world, and log the stroke boundaries.
    /// </summary>
    public class HandleDrawPath : PacketHandlerBase<DrawPathRequest>
    {
        private static readonly log4net.ILog _logger = Logging.LoggerProvider.GetLogger();
        private readonly Game _game;
        private readonly PlayerManager _playerManager;

        public HandleDrawPath(Game game)
        {
            _game = game;
            _playerManager = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, DrawPathRequest req)
        {
            var point = new Vector2(req.Point.X, req.Point.Z);
            // Stroke boundaries are worth a log line; mid points would spam (client streams them
            // at the mode's UpdateRate while the key is held).
            if (req.NodeType != 1)
            {
                _logger.Debug($"[DrawPath] user {userId} target 0x{req.TargetNetID:X} "
                    + $"{(req.NodeType == 0 ? "STROKE START" : "STROKE END")} at {point}");
            }

            // Paint the point: small green debug circle, short-lived (same troy the !debugmode
            // visualizers use, so it exists in every client build).
            _ = new Particle(_game, null, null, point, "DebugCircle_green.troy", 0.35f, "", "", 0,
                default, false, 0.1f);
            return true;
        }
    }
}
