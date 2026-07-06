namespace GameServerCore.Enums
{
    /// <summary>
    /// Why an AI lost its current target — passed with the OnTargetLost event (Riot OnTargetLost(reason, unit)).
    /// Only <see cref="LostVisibility"/> drives the champion "go to last known location" re-acquisition
    /// (Hero.lua); the other reasons hard-drop the target (see docs/LOST_TARGET_REACQUISITION_PLAN.md).
    /// </summary>
    public enum TargetLostReason
    {
        /// <summary>Target cleared for an unclassified/manual reason (default). No special re-acquire.</summary>
        Cleared = 0,
        /// <summary>Target died.</summary>
        Death = 1,
        /// <summary>Target became untargetable (e.g. revive/invuln state).</summary>
        Untargetable = 2,
        /// <summary>Target left acquisition range.</summary>
        OutOfRange = 3,
        /// <summary>Target left this unit's team vision (alive + targetable) — drives go-to-last-known.</summary>
        LostVisibility = 4,
    }
}
