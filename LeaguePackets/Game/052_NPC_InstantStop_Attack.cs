
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    public class NPC_InstantStop_Attack : GamePacket // 0x34
    {
        public override GamePacketID ID => GamePacketID.NPC_InstantStop_Attack;
        public byte Flags { get; set; }
        public uint MissileNetID { get; set; }
        public bool KeepAnimating { get => (Flags & 1) != 0; set => SetFlag(1, value); }
        public bool ForceDoClient { get => (Flags & 2) != 0; set => SetFlag(2, value); }
        public bool OverrideVisibility { get => (Flags & 4) != 0; set => SetFlag(4, value); }
        public bool IsSummonerSpell { get => (Flags & 8) != 0; set => SetFlag(8, value); }
        public bool DestroyMissile { get => (Flags & 16) != 0; set => SetFlag(16, value); }
        public bool CompleteCast { get => (Flags & 32) != 0; set => SetFlag(32, value); }

        private void SetFlag(byte mask, bool value)
            => Flags = value ? (byte)(Flags | mask) : (byte)(Flags & ~mask);

        protected override void ReadBody(ByteReader reader)
        {

            Flags = reader.ReadByte();
            MissileNetID = reader.ReadUInt32();
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteByte(Flags);
            writer.WriteUInt32(MissileNetID);
        }
    }
}
