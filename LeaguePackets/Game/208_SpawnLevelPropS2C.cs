
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace LeaguePackets.Game
{
    public class SpawnLevelPropS2C : GamePacket // 0xD0
    {
        public override GamePacketID ID => GamePacketID.SpawnLevelPropS2C;
        public uint NetID { get; set; }
        public byte NetNodeID { get; set; }
        public int SkinID { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 FacingDirection { get; set; }
        public Vector3 PositionOffset { get; set; }
        public Vector3 Scale { get; set; }
        // PKT_SpawnLevelPropS2C_s "bitfield" (MultiplayerPackets.h): uint16 holding ONLY the
        // TeamID — TEAMID_MASK = 511 / shift 0, bits 9-15 undefined (clean 0 on the Riot wire;
        // every observed prop is team 300 = neutral). Masked on read/write.
        // NOTE the 4.17 header struct is STALE for this packet: it declares `type` as a single
        // BYTE (195-byte packet, skillLevel before rank), but the 4.20 wire is 198 bytes with a
        // 4-byte type field (=2 for all observed props, beyond the header's enum 0/1) — verified
        // by parsing Name/PropName cleanly at the shifted offsets across 53 replay packets
        // (sru_lizard, SRU_storeKeeperNorth, ...). Rank/SkillLevel order is not decidable from
        // replays (both always 0).
        public ushort TeamID { get; set; }
        public byte SkillLevel { get; set; }
        public byte Rank { get; set; }
        public byte Type { get; set; }
        public string Name { get; set; } = "";
        public string PropName { get; set; } = "";

        protected override void ReadBody(ByteReader reader)
        {

            this.NetID = reader.ReadUInt32();
            this.NetNodeID = reader.ReadByte();
            this.SkinID = reader.ReadInt32();
            this.Position = reader.ReadVector3();
            this.FacingDirection = reader.ReadVector3();
            this.PositionOffset = reader.ReadVector3();
            this.Scale = reader.ReadVector3();
            this.TeamID = (ushort)(reader.ReadUInt16() & 0x1FF);
            this.Rank = reader.ReadByte();
            this.SkillLevel = reader.ReadByte();
            this.Type = (byte)reader.ReadUInt32();
            this.Name = reader.ReadFixedString(64);
            this.PropName = reader.ReadFixedStringLast(64);
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteUInt32(NetID);
            writer.WriteByte(NetNodeID);
            writer.WriteInt32(SkinID);
            writer.WriteVector3(Position);
            writer.WriteVector3(FacingDirection);
            writer.WriteVector3(PositionOffset);
            writer.WriteVector3(Scale);
            writer.WriteUInt16((ushort)(TeamID & 0x1FF));
            writer.WriteByte(Rank);
            writer.WriteByte(SkillLevel);
            writer.WriteUInt32((byte)Type);
            writer.WriteFixedString(Name, 64);
            writer.WriteFixedStringLast(PropName, 64);
        }
    }
}
