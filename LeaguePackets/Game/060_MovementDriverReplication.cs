
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
    public class MovementDriverReplication : GamePacket // 0x3C
    {
        public override GamePacketID ID => GamePacketID.MovementDriverReplication;
        // Factory key selecting the client's movement driver (mac decomp: PKT_MovementDriverReplication_s.movementTypeID,
        // BYTE @0x05). Must match the trailing param's mParamType below.
        public MovementParamType MovementTypeID { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public MovementDriverHomingData MovementDriverHomingData { get; set; } = null;

        protected override void ReadBody(ByteReader reader)
        {

            this.MovementTypeID = (MovementParamType)reader.ReadByte();
            this.Position = reader.ReadVector3();
            this.Velocity = reader.ReadVector3();
            // First uint32 of the trailing MovementParamBase buffer = mParamType (same enum as the header byte).
            var paramType = (MovementParamType)reader.ReadInt32();
            if (paramType == MovementParamType.TargetHoming)
            {
                this.MovementDriverHomingData = reader.ReadMovementDriverHomingData();
            }
            else
            {
                this.MovementDriverHomingData = null;
            }
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteByte((byte)MovementTypeID);
            writer.WriteVector3(Position);
            writer.WriteVector3(Velocity);
            if (MovementDriverHomingData == null)
            {
                writer.WriteInt32((int)MovementParamType.None);
            }
            else
            {
                writer.WriteInt32((int)MovementParamType.TargetHoming);
                writer.WriteMovementDriverHomingData(MovementDriverHomingData);
            }
        }
    }
}
