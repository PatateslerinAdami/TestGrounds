
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LeaguePackets.Game
{
    /// <summary>
    /// Server→client broadcast message / floating text (GamePacketID 0x18). This is what
    /// Riot 4.20 actually uses for script/buff float text (NOT DisplayFloatingText/0x19).
    /// Wire layout verified byte-for-byte against 4.20 replays (e.g. Pantheon Aegis block):
    /// GamePacket header `[opcode][SenderNetID]` (SenderNetID = the unit the text floats over),
    /// then BubbleDelay(f32) SlotNumber(i32) IsError(u8) ColorIndex(u8) FloatTextType(u32) Message(sized).
    /// <see cref="Message"/> is usually a localization key, e.g. "game_lua_Aegis_Block",
    /// "game_lua_UndyingRage", "game_floatingtext_invulnerable".
    /// </summary>
    public class NPC_MessageToClient_Broadcast : GamePacket // 0x18
    {
        public override GamePacketID ID => GamePacketID.NPC_MessageToClient_Broadcast;

        /// <summary>Seconds before the bubble/text shows. Replays consistently carry 2.0.</summary>
        public float BubbleDelay { get; set; }
        public int SlotNumber { get; set; }
        public bool IsError { get; set; }
        public byte ColorIndex { get; set; }
        /// <summary>Client animation/style profile (see GameServerCore.Enums.FloatTextType). Replays: 0=Invulnerable, 4=ManaDamage, 25=Countdown, 28=Debug.</summary>
        public uint FloatTextType { get; set; }
        public string Message { get; set; } = "";

        protected override void ReadBody(ByteReader reader)
        {

            this.BubbleDelay = reader.ReadFloat();
            this.SlotNumber = reader.ReadInt32();
            this.IsError = reader.ReadBool();
            this.ColorIndex = reader.ReadByte();
            this.FloatTextType = reader.ReadUInt32();
            this.Message = reader.ReadSizedStringLast();
        }

        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteFloat(BubbleDelay);
            writer.WriteInt32(SlotNumber);
            writer.WriteBool(IsError);
            writer.WriteByte(ColorIndex);
            writer.WriteUInt32(FloatTextType);
            writer.WriteSizedStringLast(Message);
        }
    }
}
