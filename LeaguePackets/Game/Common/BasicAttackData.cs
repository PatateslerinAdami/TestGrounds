using System;
using System.Numerics;

namespace LeaguePackets.Game.Common
{
    public class BasicAttackData
    {
        public uint TargetNetID { get; set; }
        public Vector3 TargetPosition { get; set; }
        public float ExtraTime { get; set; }
        public uint MissileNextID { get; set; }
        public byte AttackSlot { get; set; }
    }
    public static class BasicAttackDataExtension
    {
        public static BasicAttackData ReadBasicAttackDataPacket(this ByteReader reader)
        {
            var attack = new BasicAttackData();
            attack.TargetNetID = reader.ReadUInt32();
            attack.ExtraTime = (reader.ReadByte() - 128) / 100.0f;
            attack.MissileNextID = reader.ReadUInt32();
            attack.AttackSlot = reader.ReadByte();
            attack.TargetPosition = reader.ReadVector3();
            return attack;
        }

        public static void WriteBasicAttackDataPacket(this ByteWriter writer, BasicAttackData attack)
        {
            if(attack == null)
            {
                attack = new BasicAttackData();
            }
            writer.WriteUInt32(attack.TargetNetID);
            float extraTime = float.IsNaN(attack.ExtraTime) ? 0 : attack.ExtraTime;
            extraTime = Math.Clamp(extraTime, -1.28f, 1.27f);
            writer.WriteByte((byte)Math.Clamp((int)(extraTime * 100.0f) + 128, 0, 255));
            writer.WriteUInt32(attack.MissileNextID);
            writer.WriteByte(attack.AttackSlot);
            writer.WriteVector3(attack.TargetPosition);
        }
    }
}
