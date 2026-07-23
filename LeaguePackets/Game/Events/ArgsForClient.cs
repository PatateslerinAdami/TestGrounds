namespace LeaguePackets.Game.Events
{
    public abstract class ArgsForClient: ArgsBase
    {
        // 4.20 wire: a single byte sits BEFORE ScriptNameHash (right after ArgsBase.OtherNetID).
        // It is 1 for a normal event and 0 only for the death-recap-source event (the killing blow) —
        // it anti-correlates exactly with a set SourceObjectNetID across the raw replay corpus
        // (12252x 1, 22x 0; every 0 lines up with SourceObjectNetID != 0). Default 1 = the common case.
        public byte NewByte { get; set; } = 1;
        public uint ScriptNameHash { get; set; }
        public byte EventSource { get; set; }
        public uint SourceObjectNetID { get; set; }
        public uint ParentScriptNameHash { get; set; }
        public uint ParentCasterNetID { get; set; }
        public ushort Bitfield { get; set; }
    }
}
