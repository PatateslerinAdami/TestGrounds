
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    // Anti-cheat client->server signal (4.17 PKT_ClientCheatDetectionSignal). The client sends it when
    // it detects a cheat (e.g. camera-zoom hack, from HudCameraLogic). Riot platform feature; unused by us.
    public class ClientCheatDetectionSignal : GamePacket, IUnusedPacket // 0x7D
    {
        public override GamePacketID ID => GamePacketID.ClientCheatDetectionSignal;

        protected override void ReadBody(ByteReader reader) 
        {
        }
        protected override void WriteBody(ByteWriter writer) {}
    }
}
