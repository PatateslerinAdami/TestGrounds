
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    // Changes a unit's voice-over bank (handler AIBaseClient.cpp:1733). Reset (bit0) set =>
    // CharacterDataStack::ResetVoiceOverride() (back to default); clear => SetVoiceOverride(VoiceOverride).
    // Used for ult/form VO swaps (Riven "Ult", Sion "Berserk"/"Max"). The byte's upper bits are junk.
    public class S2C_ChangeCharacterVoice : GamePacket // 0x96
    {
        public override GamePacketID ID => GamePacketID.S2C_ChangeCharacterVoice;
        public bool Reset { get; set; }
        public string VoiceOverride { get; set; } = "";

        protected override void ReadBody(ByteReader reader)
        {

            byte bitfield = reader.ReadByte();
            this.Reset = (bitfield & 1) != 0;

            this.VoiceOverride = reader.ReadFixedStringLast(64);
        }
        protected override void WriteBody(ByteWriter writer)
        {
            byte bitfield = 0;
            if (Reset)
            {
                bitfield |= 1;
            }
            writer.WriteByte(bitfield);

            writer.WriteFixedStringLast(VoiceOverride, 64);
        }
    }
}
