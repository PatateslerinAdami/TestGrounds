using GameServerCore.Packets.PacketDefinitions;

namespace GameServerCore.Packets.PacketDefinitions.Requests
{
    public class CustomModPacketRequest : ICoreRequest
    {
        public byte CommandId { get; set; }
        public byte[] Payload { get; set; } 
    }
}