
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    public class NPC_LevelUp : GamePacket // 0x3F
    {
        public override GamePacketID ID => GamePacketID.NPC_LevelUp;
        public byte Level { get; set; }
        public byte AvailablePoints { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            this.Level = reader.ReadByte();
            this.AvailablePoints = reader.ReadByte();
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteByte(Level);
            writer.WriteByte(AvailablePoints);
        }
    }
}
