
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    public enum CreateHeroDeath : uint
    {
        Alive = 0,
        Zombie = 1,
        Dead = 2
    }

    public class S2C_CreateHero : GamePacket // 0x4C
    {
        public override GamePacketID ID => GamePacketID.S2C_CreateHero;
        public uint NetID { get; set; }
        public int ClientID { get; set; }
        public byte NetNodeID { get; set; }
        public byte SkillLevel { get; set; }
        public bool TeamIsOrder { get; set; }
        public bool IsBot { get; set; }
        public byte BotRank { get; set; }
        public byte SpawnPositionIndex { get; set; }
        public int SkinID { get; set; }
        public string Name { get; set; } = "";
        public string Skin { get; set; } = "";
        public float DeathDurationRemaining { get; set; }
        public float TimeSinceDeath { get; set; }

        public CreateHeroDeath CreateHeroDeath { get; set; }
        // Trailing bitfield. The 4.17 header packed DeathState (bits 0-2, DEATHSTATE_MASK=7) and
        // ChangeHero (bit 3, CHANGEHERO_MASK=8) here; 4.20 moved DeathState into the explicit
        // uint32 above (+3 bytes, replay: all packets 203 bytes), leaving ChangeHero as the only
        // remaining flag — re-packed at bit 0. ChangeHero = client resolves an EXISTING hero by
        // NetID and swaps it in place instead of creating a new one (GameClient.cpp:869 hero-swap
        // path). Wire evidence (621 packets, ~39 games): bit 0 is NEVER set (no hero swap occurs
        // in normal games); bits 1-7 are uninitialized garbage — per-GAME constant across all
        // heroes (same stack frame reused in Riot's spawn loop), ~50-78% set per bit. We always
        // send them clean.
        public bool IsChangeHero { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            this.NetID = reader.ReadUInt32();
            this.ClientID = reader.ReadInt32();
            this.NetNodeID = reader.ReadByte();
            this.SkillLevel = reader.ReadByte();

            byte bitfield1 = reader.ReadByte();
            this.TeamIsOrder = (bitfield1 & 0x01) != 0;
            this.IsBot = (bitfield1 & 0x02) != 0;

            this.BotRank = reader.ReadByte();
            this.SpawnPositionIndex = reader.ReadByte();
            this.SkinID = reader.ReadInt32();
            this.Name = reader.ReadFixedString(128);
            this.Skin = reader.ReadFixedString(40);
            this.DeathDurationRemaining = reader.ReadFloat();
            this.TimeSinceDeath = reader.ReadFloat();
            this.CreateHeroDeath = (CreateHeroDeath)reader.ReadUInt32();

            byte bitfield2 = reader.ReadByte();
            this.IsChangeHero = (bitfield2 & 0x01) != 0;
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteUInt32(NetID);
            writer.WriteInt32(ClientID);
            writer.WriteByte(NetNodeID);
            writer.WriteByte(SkillLevel);

            byte bitfield1 = 0;
            if(TeamIsOrder)
            {
                bitfield1 |= 0x01;
            }
            if(IsBot)
            {
                bitfield1 |= 0x02;
            }
            writer.WriteByte(bitfield1);

            writer.WriteByte(BotRank);
            writer.WriteByte(SpawnPositionIndex);
            writer.WriteInt32(SkinID);
            writer.WriteFixedString(Name, 128);
            writer.WriteFixedString(Skin, 40);
            writer.WriteFloat(DeathDurationRemaining);
            writer.WriteFloat(TimeSinceDeath);
            writer.WriteUInt32((uint)CreateHeroDeath);

            byte bitfield2 = 0;
            if (IsChangeHero)
            {
                bitfield2 |= 0x01;
            }
            writer.WriteByte(bitfield2);
        }
    }
}
