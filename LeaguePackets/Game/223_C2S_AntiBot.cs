
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    // Anti-cheat client->server response (4.17 PKT_C2S_AntiBot). Riot platform feature; unused by us
    // (body never reverse-engineered — empty). C2S, so never appears in S2C replays.
    public class C2S_AntiBot : GamePacket // 0xDF
    {
        public override GamePacketID ID => GamePacketID.C2S_AntiBot;

        protected override void ReadBody(ByteReader reader) 
        {
        }
        protected override void WriteBody(ByteWriter writer) { }
    }
}
