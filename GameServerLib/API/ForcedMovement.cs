// In your ForcedMovement.cs file (or wherever you placed the class)
using GameServerCore.Enums;
using System.Numerics;

public enum MovementType
{
    Dash,
    Knockup,
    Knockback,
    Pull
}

public class ForcedMovement
{
    public MovementType Type { get; }
    public float TotalDuration { get; } // Duration in milliseconds
    public float Speed { get; }
    public float ParabolicGravity { get; }
    public Vector2 StartPosition { get; }
    public Vector2 EndPosition { get; }
    public bool KeepFacingLastDirection { get; }

    // -- ADDED THIS PROPERTY --
    public StatusFlags StatusFlagsToDisable { get; }

    // -- ADDED THIS PROPERTY --
    public uint FollowTargetNetID { get; }

    public float TimeElapsed { get; private set; }

    // -- UPDATED THE CONSTRUCTOR --
    public ForcedMovement(MovementType type, float duration, float speed, float gravity, Vector2 start, Vector2 end, bool keepFacing, StatusFlags statusFlagsToDisable, uint followTargetNetID = 0)
    {
        Type = type;
        TotalDuration = duration;
        Speed = speed;
        ParabolicGravity = gravity;
        StartPosition = start;
        EndPosition = end;
        KeepFacingLastDirection = keepFacing;
        StatusFlagsToDisable = statusFlagsToDisable;
        FollowTargetNetID = followTargetNetID;
        TimeElapsed = 0.0f;
    }

    /// <summary>
    /// Updates the timer for the movement.
    /// </summary>
    /// <param name="diff">Time elapsed since last update (milliseconds).</param>
    /// <returns>True if the movement has finished, otherwise false.</returns>
    public bool Update(float diff)
    {
        TimeElapsed += diff;
        return TimeElapsed >= TotalDuration;
    }
}