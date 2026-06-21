namespace GameServerCore.Packets.PacketDefinitions.Requests
{
    // C2S_TeamBalanceVote (0xFB) payload. Mirrors SurrenderRequest: a single yes/no vote bit.
    // The team-balance vote shares the surrender vote HUD on the client (HudVote.mIsBalanceVote,
    // HudFlashVote.cpp:111) and the same reason codes — see TeamBalanceHandler.
    public class TeamBalanceRequest : ICoreRequest
    {
        public bool VotedYes { get; set; }

        public TeamBalanceRequest(bool vote)
        {
            VotedYes = vote;
        }
    }
}
