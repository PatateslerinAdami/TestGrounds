namespace GameServerCore.Enums
{
    public enum NavigationGridCellFlags : ushort
    {
        HAS_GRASS = 0x1,
        NOT_PASSABLE = 0x2,
        BUSY = 0x4,
        TARGETTED = 0x8,
        MARKED = 0x10,
        PATHED_ON = 0x20,
        SEE_THROUGH = 0x40,
        // 0x80 is the client's *runtime* "last expansion direction" tag in bidirectional A*
        // (mFlags & 0x80, S1:7966-7972 / S4:11680-11686). It's per-search scratch state, not
        // a persistent map property. The master server tracks the same thing on
        // NavigationGridCell.LastTouchByBackward (a separate per-cell bool). Never test or
        // set this via the enum; the value is masked out at file load (see NavigationGridCell
        // ReadVersion5/7) so HasFlag(...) on it always returns false.
        OTHER_DIRECTION_END_TO_START = 0x80,
        HAS_GLOBAL_VISION = 0x100,
        // HAS_TRANSPARENT_TERRAIN = 0x42 // (SeeThrough | NotPassable)
    }
}