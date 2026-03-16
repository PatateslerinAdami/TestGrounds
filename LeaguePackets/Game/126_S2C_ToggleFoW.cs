using LeaguePackets;

public class S2C_ToggleFoW : GamePacket // 0x7E
{
    public override GamePacketID ID => GamePacketID.S2C_ToggleFoW;
    public byte Enable { get; set; }

    protected override void ReadBody(ByteReader reader)
    {
        Enable = reader.ReadByte();
    }

    protected override void WriteBody(ByteWriter writer)
    {
        writer.WriteByte(Enable);
    }
}