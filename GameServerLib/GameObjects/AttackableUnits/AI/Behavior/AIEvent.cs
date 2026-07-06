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

        // Capability regain (Riot OnCanMove / OnCanAttack — fire when the CanMove / CanAttack capability
        // transitions disabled→enabled, i.e. movement/attack-disabling CC just ended). Scripts react by
        // re-acquiring (Aggro.lua: NetSetState(AI_IDLE) + FindTargetOrMove). Polled from StatusFlags like
        // the CC events. Prerequisite for the MoveOrder-driver decouple — see docs/AI_EVENT_AUDIT.md.
        OnCanMove,
        OnCanAttack,

        // Movement (Riot OnStopMove / OnStoppedMoving). OnStopMove = a stop COMMAND was issued
        // (StopMovement — CC / cast / player-stop / settle). OnStoppedMoving = the unit FINISHED moving
        // (path consumed / arrived). Used by archetype scripts to transition state on arrival (Poro retreat,
        // Hero clears target-pos on stop). E2, docs/AI_EVENT_AUDIT.md. Prerequisite for the MoveOrder decouple
        // (the engine's settle→Stop auto-mutation becomes an explicit script reaction to these).
        OnStopMove,
        OnStoppedMoving,

        // Riot OnReachedDestinationForGoingToLastLocation: the unit reached the last-known position of a
        // target it lost to vision without re-sighting it (Hero.lua → AI_IDLE + rescan). Emitted on the
        // path-end edge while CurrentState == AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION. Champion-only in
        // practice. See docs/LOST_TARGET_REACQUISITION_PLAN.md.
        OnReachedDestinationForGoingToLastLocation,

        // Combat / targeting
        OnCallForHelp,
        OnReceiveImportantCallForHelp,
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
