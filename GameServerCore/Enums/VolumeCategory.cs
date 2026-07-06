namespace GameServerCore.Enums
{
    /// <summary>
    /// Audio volume categories the client can mute/unmute (S2C_MuteVolumeCategory → Audio::SetMute).
    /// Values match the 4.17 decomp `Audio::VolumeCategory::Type`.
    /// </summary>
    public enum VolumeCategory : byte
    {
        Master = 0,
        Music = 1,
        SFX = 2,
        Announcer = 3,
        UnitResponses = 4,
        Environment = 5,
        SFXMain = 6,
    }
}
