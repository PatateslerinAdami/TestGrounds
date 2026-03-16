using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    public class S2C_ToggleFoW : GamePacket // 0x7E
    {
        public override GamePacketID ID => GamePacketID.S2C_ToggleFoW;
        public byte Enable { get; set; }

        protected override void ReadBody(ByteReader reader)
        {
            Enable = reader.ReadByte();
        }
        protected override void WriteBody(ByteWriter writer) 
        {
            writer.WriteByte(Enable);
        }
    }
}
