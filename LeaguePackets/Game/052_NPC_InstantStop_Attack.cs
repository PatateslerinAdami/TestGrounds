
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
        public uint MissileNetID { get; set; }
        public bool KeepAnimating { get; set; }
        public bool DestroyMissile { get; set; }
        public bool OverrideVisibility { get; set; }
        public bool IsSummonerSpell { get; set; }
        public bool ForceDoClient { get; set; }

        // 4.20 wire layout REPLAY-VERIFIED (db0ba71d + c7119e79, ~40k packets): the bitfield comes
        // FIRST, then MissileNetID — flipped from the 4.17 mac DWARF (AIBasePackets.h has
        // missileToDestroy@0x05, bitfield@0x09). Proof: under [flags][netid] every non-zero netid is
        // a valid 0x4xxxxxxx object id and appears ONLY when DESTROYMISSILE (0x10) is set (24 exact
        // matches against same-caster CastSpellAns missile ids); under the 4.17 order there are ZERO
        // matches and the "flags" byte is just the netid MSB (0x00/0x40).
        // Bit values from the 4.17 DWARF masks, bit4 replay-confirmed for 4.20:
        // 1=KEEPANIMATING, 2=FORCESPELLCAST (ForceDoClient), 4=FORCESTOP (OverrideVisibility),
        // 8=AVATARSPELL (IsSummonerSpell), 16=DESTROYMISSILE, 32=COMPLETECAST.
        // Bits 6/7 (0x40/0x80) are new in 4.20 (heavily used on the wire, semantics unknown).
        public bool CompleteCast { get; set; }
        public bool Unknown0x40 { get; set; }
        public bool Unknown0x80 { get; set; }

        protected override void ReadBody(ByteReader reader)
        {

            byte flags = reader.ReadByte();
            this.MissileNetID = reader.ReadUInt32();
            this.KeepAnimating = (flags & 1) != 0;
            this.ForceDoClient = (flags & 2) != 0;
            this.OverrideVisibility = (flags & 4) != 0;
            this.IsSummonerSpell = (flags & 8) != 0;
            this.DestroyMissile = (flags & 16) != 0;
            this.CompleteCast = (flags & 32) != 0;
            this.Unknown0x40 = (flags & 64) != 0;
            this.Unknown0x80 = (flags & 128) != 0;
        }
        protected override void WriteBody(ByteWriter writer)
        {
            byte flags = 0;
            if (KeepAnimating)
                flags |= 1;
            if (ForceDoClient)
                flags |= 2;
            if (OverrideVisibility)
                flags |= 4;
            if (IsSummonerSpell)
                flags |= 8;
            if (DestroyMissile)
                flags |= 16;
            if (CompleteCast)
                flags |= 32;
            if (Unknown0x40)
                flags |= 64;
            if (Unknown0x80)
                flags |= 128;
            writer.WriteByte(flags);
            writer.WriteUInt32(MissileNetID);
        }
    }
}
