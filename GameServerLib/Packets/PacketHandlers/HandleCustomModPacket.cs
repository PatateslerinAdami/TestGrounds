using GameServerCore.Enums;
using GameServerCore.Packets.Handlers;
using GameServerCore.Packets.PacketDefinitions.Requests;
using LeaguePackets;
using LENet;
using System;
using Channel = GameServerCore.Packets.Enums.Channel;

namespace LeagueSandbox.GameServer.Packets.PacketHandlers
{
    public class HandleCustomModPacket : PacketHandlerBase<CustomModPacketRequest>
    {
        private readonly Game _game;

        public HandleCustomModPacket(Game game)
        {
            _game = game;
        }

        public override bool HandlePacket(int userId, CustomModPacketRequest req)
        {
            if (req.Payload == null || req.Payload.Length < 2)
            {
                return false;
            }

            int magicIndex = req.Payload.Length - 2;
            ushort magic = BitConverter.ToUInt16(req.Payload, magicIndex);

            if (magic != 0x1337)
            {
                return false; 
            }

            if (req.CommandId == 1) 
            {
                if (req.Payload.Length >= 6)
                {
                    float newSpeed = BitConverter.ToSingle(req.Payload, 0);

                    _game.TimeScale = newSpeed;
                    _game.PacketNotifier.NotifyCustomModPacket(req.CommandId, req.Payload);
                }
            }

            return true;
        }
    }
}