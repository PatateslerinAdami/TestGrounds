
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    public class S2C_MarkOrSweepForSoftReconnect : GamePacket // 0xEF
    {
        public override GamePacketID ID => GamePacketID.S2C_MarkOrSweepForSoftReconnect;
        // 4.17 decomp: PKT_S2C_MarkOrSweepForSoftReconnect_s { uint8 bitfield } with
        // Stage { MARK_ALL_UNITS = 0, DESTROY_ALL_UNITS = 1 }. Single BYTE on the wire (was wrongly u32).
        public byte Stage { get; set; }

        protected override void ReadBody(ByteReader reader)
        {
            this.Stage = reader.ReadByte();
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteByte(Stage);
        }
    }
}
