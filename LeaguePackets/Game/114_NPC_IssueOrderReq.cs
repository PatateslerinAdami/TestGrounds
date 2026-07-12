
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using LeaguePackets.Game.Common;

namespace LeaguePackets.Game
{
    public class NPC_IssueOrderReq : GamePacket // 0x72
    {
        public override GamePacketID ID => GamePacketID.NPC_IssueOrderReq;
        public byte OrderType { get; set; }
        public Vector2 Position { get; set; }
        public uint TargetNetID { get; set; }
        // Optional client-computed path. The wire has NO discriminator for it: the 4.17 struct is
        // a fixed OrderInfo head + flexible `unsigned char data[]`, and the S1 client serializes
        // the path via Actor_Common::PathToNetworkData and sends header+path through
        // SendVariableSizeInternal<PKT_NPC_IssueOrderReq_s> — presence is detectable ONLY by the
        // remaining packet length (which is exactly what the BytesLeft check below does), and the
        // trailing bytes are always MovementDataNormal-shaped. A path is attached for
        // MOVETO/ATTACKMOVE/ATTACKTO/ATTACKTERRAIN orders (S4 AIBaseClient.cpp:3290ff builds it);
        // other orders may arrive as the bare 18-byte head, leaving this null.
        public MovementDataNormal MovementData { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            this.OrderType = reader.ReadByte();
            this.Position = reader.ReadVector2();
            this.TargetNetID = reader.ReadUInt32();
            // Smallest real path payload is bitfield(1) + teleportNetID(4) + >=1 waypoint byte;
            // a bare or empty-path packet (bitfield 0 = one byte) parses to MovementData = null,
            // which readers must treat as "no path attached".
            if(reader.BytesLeft > 4)
            {
                this.MovementData = new MovementDataNormal(reader, 0);
            }
        }

        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteByte(OrderType);
            writer.WriteVector2(Position);
            writer.WriteUInt32(TargetNetID);
            if(MovementData != null)
            {
                MovementData.Write(writer);
            }
        }
    }
}
