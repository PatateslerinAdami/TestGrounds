namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior
{
    /// <summary>
    /// AI events dispatched through <see cref="BaseAIScript"/>'s event bus, mirroring Riot's Lua
    /// EventSystem (RegisterForEvent / Event). Components subscribe to these and the core AI emits
    /// them on lifecycle / CC / combat transitions. Custom component-to-component events
    /// (RiverCornered, Melee/RangeAttacked, WanderPause/Resume) live here too so the bus stays
    /// type-safe instead of string-keyed.
    /// </summary>
    public enum AIEvent
    {
        // Lifecycle
        ComponentInit,
        ComponentHalt,

        // Crowd control (emitted by the core AI when the matching buff/status toggles)
        OnFearBegin,
        OnFearEnd,
        OnFleeBegin,
        OnFleeEnd,
        OnTauntBegin,
        OnTauntEnd,
        OnCharmBegin,
        OnCharmEnd,

        // Combat / targeting
        OnCallForHelp,
        OnTargetLost,
        OnTargetDied,
        OnMeleeAttacked,
        OnRangeAttacked,

        // Component coordination (e.g. CC pauses wandering)
        WanderPause,
        WanderResume,
        RiverCornered
    }
}
