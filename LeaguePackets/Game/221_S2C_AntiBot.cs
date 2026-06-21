
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    // Anti-cheat server->client packet (4.17 PKT_S2C_AntiBot). Riot platform feature; unused by us.
    public class S2C_AntiBot : GamePacket, IUnusedPacket // 0xDD
    {
        public override GamePacketID ID => GamePacketID.S2C_AntiBot;

        protected override void ReadBody(ByteReader reader)
        {
        }
        protected override void WriteBody(ByteWriter writer) { }
    }
}
