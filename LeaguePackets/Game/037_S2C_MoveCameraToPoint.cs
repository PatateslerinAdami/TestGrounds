
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace LeaguePackets.Game
{
    public class S2C_MoveCameraToPoint : GamePacket // 0x25
    {
        public override GamePacketID ID => GamePacketID.S2C_MoveCameraToPoint;
        // Bit 0 is the ONLY defined flag (PKT_S2C_MoveCameraToPoint_s:
        // STARTATCURRENTCAMERAPOSITION_MASK = 1; the client handler AIHeroClient.cpp:921 reads
        // `bitfield & 1` and nothing else). Set → the camera pans from its CURRENT position to
        // targetPosition and startPosition is ignored; clear → it pans from startPosition.
        // A former "UnlockCamera" bit-1 property here was invented — no such flag exists.
        public bool StartFromCurrentPosition { get; set; }
        // Only used when StartFromCurrentPosition is false.
        public Vector3 StartPosition { get; set; }
        public Vector3 TargetPosition { get; set; }
        public float TravelTime { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            byte bitfield = reader.ReadByte();
            this.StartFromCurrentPosition = (bitfield & 0x01) != 0;

            this.StartPosition = reader.ReadVector3();
            this.TargetPosition = reader.ReadVector3();
            this.TravelTime = reader.ReadFloat();
        }
        protected override void WriteBody(ByteWriter writer)
        {
            byte bitfield = 0;
            if (StartFromCurrentPosition)
                bitfield |= 0x01;
            writer.WriteByte(bitfield);

            writer.WriteVector3(StartPosition);
            writer.WriteVector3(TargetPosition);
            writer.WriteFloat(TravelTime);
        }
    }
}
