using System;
using System.Numerics;

namespace LeagueSandbox.GameServer.Movement;

// Mutable per actor integration state, independent of combat orders and packets.
public sealed class NavigationActorState
{
    public int PathRevision { get; private set; }
    public int RepathAttempts { get; private set; }
    public int RecoveryTicks { get; private set; }
    public float BlockedSeconds { get; private set; }
    public float RetrySeconds { get; private set; }
    // Duration represented by the completed Move() displacement in ObjAIBase.Velocity.
    public float LastDeltaSeconds { get; set; }
    public Vector2 RequestedMovement { get; set; }
    public Vector2 CandidateMovement { get; set; }
    public Vector2 AcceptedMovement { get; set; }
    // Sum of changes imposed by emulator speed caps/terrain checks in this solve.
    // Excludes ordinary collision steering; not a measurement of client error.
    public Vector2 SolverConstraintDelta { get; set; }
    public string LastReason { get; set; } = "idle";
    public bool GhostProofForAllies { get; set; }
    public bool GhostProofForEnemies { get; set; }
    public bool Waiting { get; set; }
    public Vector2? Goal { get; set; }
    private float _progressTime;
    private float _progressDistance = float.PositiveInfinity;

    public void NewPath()
    {
        PathRevision++;
        Waiting = false;
        _progressTime = 0;
        _progressDistance = float.PositiveInfinity;
        Progress();
    }
    public void RepairedPath() { PathRevision++; _progressDistance = float.PositiveInfinity; }
    public bool ObserveProgress(float seconds, float remainingDistance, float speed)
    {
        _progressTime += seconds;
        if (float.IsPositiveInfinity(_progressDistance)) _progressDistance = remainingDistance;
        if (_progressDistance - remainingDistance >= Math.Max(2, speed * .1f))
        {
            _progressDistance = remainingDistance;
            _progressTime = 0;
            Progress();
        }
        // Sideways steps are allowed. Recovery is based on sustained route progress.
        if (_progressTime < .5f) return false;
        Blocked(seconds);
        return true;
    }
    public void AdvanceTime(float seconds) => RetrySeconds = Math.Max(0, RetrySeconds - seconds);
    public void Blocked(float seconds)
    {
        BlockedSeconds += seconds;
        RecoveryTicks++;
        LastReason = "blocked";
    }
    public void Progress()
    {
        BlockedSeconds = 0;
        RecoveryTicks = 0;
        RepathAttempts = 0;
        RetrySeconds = 0;
    }
    public void RepathAttempt(bool accurate)
    {
        // Bound retry latency even after a long obstruction. The global queue
        // limits work, an ever growing delay must not strand a released actor.
        RetrySeconds = Math.Min(2, .25f * (1 + RepathAttempts));
        RepathAttempts = Math.Min(32, RepathAttempts + 1);
    }
}
