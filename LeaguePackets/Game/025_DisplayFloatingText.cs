
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    /// <summary>
    /// Server→client floating text over a unit (GamePacketID 0x19).
    /// IMPORTANT — FloatTextType width: the 4.17 mac decomp (PKT_DisplayFloatingText_s) declares this
    /// as a single BYTE, but the LIVE 4.20 client expects a 4-byte (u32) field. Sending it as a BYTE
    /// makes the packet 3 bytes short and CRASHES the 4.20 client (verified in-game 2026-06-11).
    /// 0x19 never appears in 4.20 replays, so there is no replay evidence — the 4.20 target wins:
    /// keep u32. Do NOT "correct" this to BYTE from the 4.17 decomp.
    /// </summary>
    public class DisplayFloatingText : GamePacket // 0x19
    {
        public override GamePacketID ID => GamePacketID.DisplayFloatingText;
        public uint TargetNetID { get; set; }

        public uint FloatTextType { get; set; }
        public int Param { get; set; }
        public string Message { get; set; } = "";

        protected override void ReadBody(ByteReader reader)
        {

            this.TargetNetID = reader.ReadUInt32();
            this.FloatTextType = reader.ReadUInt32();
            this.Param = reader.ReadInt32();
            this.Message = reader.ReadFixedStringLast(128);
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteUInt32(TargetNetID);
            writer.WriteUInt32(FloatTextType);
            writer.WriteInt32(Param);
            writer.WriteFixedStringLast(Message, 128);
        }
    }
}
