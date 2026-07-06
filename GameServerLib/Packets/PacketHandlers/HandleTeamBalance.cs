using GameServerCore.Packets.PacketDefinitions.Requests;
using GameServerCore.Packets.Handlers;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;
using LeagueSandbox.GameServer.Players;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    // C2S_TeamBalanceVote (0xFB) handler. Direct mirror of HandleSurrender — resolves the voting
    // champion from the userId and forwards the yes/no vote to the team's TeamBalanceHandler.
    public class HandleTeamBalance : PacketHandlerBase<TeamBalanceRequest>
    {
        private readonly Game _game;
        private readonly PlayerManager _pm;

        public HandleTeamBalance(Game game)
        {
            _game = game;
            _pm = game.PlayerManager;
        }

        public override bool HandlePacket(int userId, TeamBalanceRequest req)
        {
            var c = _pm.GetPeerInfo(userId).Champion;
            HandleTeamBalanceVote(userId, c, req.VotedYes);
            return true;
        }
    }
}
