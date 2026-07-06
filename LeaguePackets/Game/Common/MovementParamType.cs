namespace LeaguePackets.Game.Common
{
    /// <summary>
    /// Movement-driver type (Riot <c>MovementParamType</c>, mac decomp 4.17). Used as the factory key
    /// that selects the client's IMovementDriver (<c>MDF::Factory::CreateObject</c>): it is carried both
    /// as the packet header byte <c>movementTypeID</c> and as the first uint32 (<c>mParamType</c>) of the
    /// trailing <c>MovementParamBase</c> in the driver buffer — the two must match.
    /// In 4.17 the only registered driver is <see cref="TargetHoming"/>; 0 means no driver.
    /// </summary>
    public enum MovementParamType : byte
    {
        /// <summary>No movement driver (the trailing param buffer is empty).</summary>
        None = 0,
        /// <summary>Target-homing driver (<c>MovementDriver::TargetHomingMovement</c>) — carries MovementDriverHomingData.</summary>
        TargetHoming = 1,
    }
}
