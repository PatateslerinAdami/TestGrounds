namespace GameServerCore.Packets.PacketDefinitions.Requests
{
    /// <summary>
    /// C2S_PlayVOCommand: a player issued a SmartPing/VO command. The server relays it to clients
    /// (S2C_PlayVOCommand, vision-gated) so they play the champion's voice line + highlight the icon.
    /// </summary>
    public class PlayVOCommandRequest : ICoreRequest
    {
        public uint CommandID { get; }
        public uint TargetNetID { get; }
        public uint EventHash { get; }
        public bool HighlightPlayerIcon { get; }
        public bool FromPing { get; }
        public bool AlliesOnly { get; }

        public PlayVOCommandRequest(uint commandId, uint targetNetId, uint eventHash, bool highlightPlayerIcon, bool fromPing, bool alliesOnly)
        {
            CommandID = commandId;
            TargetNetID = targetNetId;
            EventHash = eventHash;
            HighlightPlayerIcon = highlightPlayerIcon;
            FromPing = fromPing;
            AlliesOnly = alliesOnly;
        }
    }
}
