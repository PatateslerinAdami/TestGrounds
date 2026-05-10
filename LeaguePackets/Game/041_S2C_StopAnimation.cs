using LeaguePackets.Game.Common;

namespace LeaguePackets.Game
{
    public class S2C_StopAnimation : GamePacket // 0x29
    {
        public override GamePacketID ID => GamePacketID.S2C_StopAnimation;

        /// <summary>
        /// Bitfield: see <see cref="StopAnimationFlags"/> for bit semantics.
        /// </summary>
        public StopAnimationFlags Flags { get; set; }

        public string AnimationName { get; set; } = "";

        protected override void ReadBody(ByteReader reader)
        {
            this.Flags = (StopAnimationFlags)reader.ReadByte();
            this.AnimationName = reader.ReadFixedStringLast(64);
        }
        protected override void WriteBody(ByteWriter writer)
        {
            writer.WriteByte((byte)Flags);
            writer.WriteFixedStringLast(AnimationName, 64);
        }
    }
}
