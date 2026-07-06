
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    // S2C_Exit is Riot's enum name (PKT_S2C_Exit = 152); functionally it is the DISCONNECT INDICATOR
    // (the client handler is PKT_S2C_ShowDisconnect): it tells clients that the champion `NetID` has
    // disconnected so they draw the DC indicator over it. `ShowToEnemies` (bit0) picks the audience —
    // the client shows it to allies when false, to enemies when true (per-recipient team check).
    public class S2C_Exit : GamePacket // 0x98
    {
        public override GamePacketID ID => GamePacketID.S2C_Exit;
        public uint NetID { get; set; }
        public bool ShowToEnemies { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            this.NetID = reader.ReadUInt32();
            byte bitfield = reader.ReadByte();
            this.ShowToEnemies = (bitfield & 1) != 0;
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteUInt32(NetID);
            byte bitfield = 0;
            if (ShowToEnemies)
                bitfield |= 1;
            writer.WriteByte(bitfield);
        }
    }
}
