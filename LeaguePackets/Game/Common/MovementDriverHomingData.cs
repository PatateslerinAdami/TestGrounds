using System;
using System.Numerics;


namespace LeaguePackets.Game.Common
{
    public class MovementDriverHomingData
    {
        public uint TargetNetID { get; set; }
        public float TargetHeightModifier { get; set; }
        public Vector3 TargetPosition { get; set; }
        public float Speed { get; set; }
        public float Gravity { get; set; }
        public float RateOfTurn { get; set; }
        public float Duration { get; set; }
        // Reserved bitfield (mac decomp 4.17 TargetHomingMovementParam.mMovementPropertyFlags, uint32):
        // it is only init-to-0, copied param->driver and driver->packet, and NEVER read — the homing
        // logic (CalcVelocityModifier) ignores it and no code sets a non-zero value. No enum/bit
        // meanings are defined in this patch, so it stays a raw uint (kept a flags shape per the name;
        // don't invent bit values). Likely used by a later patch / a driver type absent in 4.17.
        public uint MovementPropertyFlags { get; set; }
    }

    public static class MovementDriverHomingDataExtension
    {
        public static MovementDriverHomingData ReadMovementDriverHomingData(this ByteReader reader)
        {
            var data = new MovementDriverHomingData();
            data.TargetNetID = reader.ReadUInt32();
            data.TargetHeightModifier = reader.ReadFloat();
            data.TargetPosition = reader.ReadVector3();
            data.Speed = reader.ReadFloat();
            data.Gravity = reader.ReadFloat();
            data.RateOfTurn = reader.ReadFloat();
            data.Duration = reader.ReadFloat();
            data.MovementPropertyFlags = reader.ReadUInt32();
            return data;
        }
        public static void WriteMovementDriverHomingData(this ByteWriter writer, MovementDriverHomingData data)
        {
            writer.WriteUInt32(data.TargetNetID);
            writer.WriteFloat(data.TargetHeightModifier);
            writer.WriteVector3(data.TargetPosition);
            writer.WriteFloat(data.Speed);
            writer.WriteFloat(data.Gravity);
            writer.WriteFloat(data.RateOfTurn);
            writer.WriteFloat(data.Duration);
            writer.WriteUInt32(data.MovementPropertyFlags);
        }
    }
}
