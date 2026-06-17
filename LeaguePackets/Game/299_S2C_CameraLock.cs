
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    public class S2C_CameraLock : GamePacket // 0x12B
    {
        public override GamePacketID ID => GamePacketID.S2C_CameraLock;
        // Replay-verified (Sion R / Unstoppable Onslaught — sole sender across 38 replays, synced to his R cast):
        // lock toggle. 1 = lock/steer camera on (charge start), 0 = unlock (charge end).
        public bool Unknown0 { get; set; }
        // Camera distance — 900.0 while locked, 0 while unlocked (Sion R). (FIXME: exact meaning of the 3 floats;
        // Unknown2/Unknown3 are always 0 in replays, likely camera target offset X/Y, unused for Sion R.)
        public float Unknown1 { get; set; }
        public float Unknown2 { get; set; }
        public float Unknown3 { get; set; }
        // Secondary flag, mirrors Unknown0 in replays (1 on lock, 0 on unlock).
        public bool Unknown4 { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            byte bitfield1 = reader.ReadByte();
            Unknown0 = (bitfield1 & 0x01) != 0;

            Unknown1 = reader.ReadFloat();
            Unknown2 = reader.ReadFloat();
            Unknown3 = reader.ReadFloat();

            byte bitfield2 = reader.ReadByte();
            Unknown4 = (bitfield2 & 0x01) != 0;
        }
        protected override void WriteBody(ByteWriter writer)
        {
            byte bitfield1 = 0;
            if (Unknown0)
                bitfield1 |= 0x01;
            writer.WriteByte(bitfield1);

            writer.WriteFloat(Unknown1);
            writer.WriteFloat(Unknown2);
            writer.WriteFloat(Unknown3);

            byte bitfield2 = 0;
            if (Unknown4)
                bitfield2 |= 0x01;
            writer.WriteByte(bitfield2);
        }
    }
}
