
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    // Anti-cheat server->client action/ban packet (4.17 PKT_S2C_AntiBotAction). Riot platform; unused by us.
    public class S2C_AntiBotAction : GamePacket, IUnusedPacket // 0xDE
    {
        public override GamePacketID ID => GamePacketID.S2C_AntiBotAction;

        protected override void ReadBody(ByteReader reader) 
        {
        }
        protected override void WriteBody(ByteWriter writer) {}
    }
}
