
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    // Owner-only S2C: cancels a unit's spell targeting reticle. The body is a SINGLE bitfield byte
    // (decomp PKT_CHAR_CancelTargetingReticle_s): bits 0-5 = SpellSlot (SLOT_MASK 0x3F), bit 6 =
    // IsSummonerSpell (0x40). The client resets the selected spell for that slot (ResetSelectedSpell).
    // Pairs with the spell-targeter / cursor-reticle system (Xerath R, Viktor R).
    public class CHAR_CancelTargetingReticle : GamePacket // 0x86
    {
        public override GamePacketID ID => GamePacketID.CHAR_CancelTargetingReticle;
        public byte SpellSlot { get; set; }
        public bool IsSummonerSpell { get; set; }

        protected override void ReadBody(ByteReader reader)
        {
            byte bitfield = reader.ReadByte();
            this.SpellSlot = (byte)(bitfield & 0x3F);
            this.IsSummonerSpell = (bitfield & 0x40) != 0;
        }
        protected override void WriteBody(ByteWriter writer)
        {
            byte bitfield = (byte)(SpellSlot & 0x3F);
            if (IsSummonerSpell)
                bitfield |= 0x40;
            writer.WriteByte(bitfield);
        }
    }
}
