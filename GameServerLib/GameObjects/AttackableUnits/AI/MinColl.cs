using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using System.Numerics;

public static class MinColl
{
    [ThreadStatic]
    private static List<ObjAIBase> _colliderScratch;

    [ThreadStatic]
    private static List<ObjAIBase> _avoidanceScratch;

    const float PushForceMultiplier = 5.1f;
    const float HeadOnAngleThreshold = 0.707f;
    const float Epsilon = 1e-5f;

    const float StuckSpeedRatio = 0.2f;
    const float ExtraSeparationSpeed = 0.1f;
    const float TimeBetweenRepaths = 0.25f;

    private const int HardStopLoopCount = 3;

    private static bool UsesAccurateCollision(ObjAIBase actor)
    {
        return actor is Champion || actor is Pet;
    }

    public static float GetActorRadius(ObjAIBase actor) =>
        Math.Clamp(actor.PathfindingRadius, 0.0f, 250.0f);

    public static float GetHardRadius(ObjAIBase actor)
    {
        if (UsesAccurateCollision(actor))
        {
            return GetActorRadius(actor) * 0.2f;
        }

        return GetActorRadius(actor);
    }
    public static float GetSoftRadius(ObjAIBase actor)
    {
        if (UsesAccurateCollision(actor))
        {
            return GetActorRadius(actor) * 0.3f;
        }

        return GetActorRadius(actor) * 2.0f;
    }
    public static float GetNeighborQueryRadius(ObjAIBase actor)
    {
        return GetSoftRadius(actor) * 2.0f;
    }

    public static bool CanCollide(ObjAIBase self, ObjAIBase other)
    {
        if (self == other || other == null)
        {
            return false;
        }

        if (self.IsDead || other.IsDead)
        {
            return false;
        }

        bool selfGhost = self.Status.HasFlag(StatusFlags.Ghosted);
        bool otherGhost = other.Status.HasFlag(StatusFlags.Ghosted);
        if (!selfGhost && !otherGhost) return true;
        bool allied = self.Team == other.Team;
        bool selfProof = self.Status.HasFlag(StatusFlags.GhostProof)
            || (allied ? self.NavigationState.GhostProofForAllies : self.NavigationState.GhostProofForEnemies);
        bool otherProof = other.Status.HasFlag(StatusFlags.GhostProof)
            || (allied ? other.NavigationState.GhostProofForAllies : other.NavigationState.GhostProofForEnemies);
        if (!selfProof && !otherProof) return false;

        return true;
    }

    public static Vector2 GetMovementForStep(ObjAIBase actor, float seconds)
    {
        float sampledSeconds = actor.NavigationState.LastDeltaSeconds;
        var displacement = actor.Velocity;
        if (!float.IsFinite(seconds) || seconds <= 0
            || !float.IsFinite(sampledSeconds) || sampledSeconds <= 0
            || !float.IsFinite(displacement.X) || !float.IsFinite(displacement.Y))
            return Vector2.Zero;
        var movement = displacement * (seconds / sampledSeconds);
        return float.IsFinite(movement.X) && float.IsFinite(movement.Y)
            ? movement : Vector2.Zero;
    }

    public static Vector2 CalculateSteeredMovement(
       ObjAIBase self,
       Vector2 desiredMovement,
       float maxDistance,
       float deltaTime,
       IEnumerable<ObjAIBase> neighbors,
       out bool isStuck, bool limitSpeed = false)
    {
        isStuck = false;

        if (Math.Abs(desiredMovement.X) <= Epsilon
            && Math.Abs(desiredMovement.Y) <= Epsilon)
        {
            return desiredMovement;
        }

        Vector2 oldPosition = self.Position;
        Vector2 nextPosition = oldPosition + desiredMovement;

        float movementBudget = Math.Min(desiredMovement.Length(), Math.Max(0, maxDistance));

        float myHardRadius = GetHardRadius(self);
        float mySoftRadius = GetSoftRadius(self);

        var colliders =
            _colliderScratch ??
            (_colliderScratch = new List<ObjAIBase>(16));

        var avoidance =
            _avoidanceScratch ??
            (_avoidanceScratch = new List<ObjAIBase>(16));

        colliders.Clear();
        avoidance.Clear();

        foreach (var testActor in neighbors)
        {
            if (!CanCollide(self, testActor))
            {
                continue;
            }

            Vector2 oldToActor =
                testActor.Position - oldPosition;

            if (Vector2.Dot(desiredMovement, oldToActor) <= 0.0f)
            {
                continue;
            }

            Vector2 candidateToActor =
                testActor.Position - nextPosition;

            float distanceSq =
                candidateToActor.LengthSquared();

            float minDistanceThreshold = Math.Clamp(
                Math.Min(
                    myHardRadius,
                    GetActorRadius(testActor)
                ) * 0.25f,
                12.0f,
                17.0f
            );

            float hardThreshold =
                GetActorRadius(testActor)
                + myHardRadius
                + minDistanceThreshold;

            if (distanceSq >= 144.0f
                && distanceSq < hardThreshold * hardThreshold)
            {
                colliders.Add(testActor);
                continue;
            }

            float softThreshold =
                GetActorRadius(testActor)
                + mySoftRadius;

            if (distanceSq > 0.0f
                && distanceSq < softThreshold * softThreshold)
            {
                
                if (Math.Abs(desiredMovement.X) > Epsilon
                    || Math.Abs(desiredMovement.Y) > Epsilon)
                {
                    var neighborMovement = GetMovementForStep(testActor, deltaTime);
                    float dmx = desiredMovement.X - neighborMovement.X;
                    float dmy = desiredMovement.Y - neighborMovement.Y;

                    if (Math.Abs(dmx) > Epsilon
                        || Math.Abs(dmy) > Epsilon)
                    {
                        avoidance.Add(testActor);
                    }
                }
            }
        }

        
        if (colliders.Count > 0)
        {
            Vector2 movement = desiredMovement;
            bool stuckWithRepulse = false;

            for (int i = 0; i < HardStopLoopCount; i++)
            {
                bool passStuck;

                movement = ResolveHardCollisionPass(
                    self,
                    movement,
                    maxDistance,
                    deltaTime,
                    colliders,
                    out passStuck
                );
                if (limitSpeed) movement = LimitSteeringDistance(self, movement, movementBudget);

                if (self.NavigationState.RepathAttempts >= 4)
                {
                    bool added = false;
                    foreach (var other in neighbors)
                    {
                        if (!CanCollide(self, other) || colliders.Contains(other)) continue;
                        float threshold = GetHardRadius(self) + GetActorRadius(other)
                            + Math.Clamp(Math.Min(GetHardRadius(self), GetActorRadius(other)) * .25f, 12, 20);
                        if (Vector2.DistanceSquared(self.Position + movement, other.Position) >= threshold * threshold) continue;
                        colliders.Add(other);
                        added = true;
                    }
                    if (added && i == HardStopLoopCount - 1)
                    {
                        movement = ResolveHardCollisionPass(self, movement, maxDistance, deltaTime, colliders, out bool responseStuck);
                        passStuck |= responseStuck;
                        if (limitSpeed) movement = LimitSteeringDistance(self, movement, movementBudget);
                    }
                }

                if (passStuck)
                {
                    stuckWithRepulse = true;
                }
            }

            isStuck = stuckWithRepulse;
            return movement;
        }

        if (avoidance.Count > 0)
        {
            float cachedMovementLengthSq =
                desiredMovement.LengthSquared();

            Vector2 objFwd =
                Vector2.Normalize(desiredMovement);

            Vector2 baryCenter = Vector2.Zero;
            Vector2 groupMovement = Vector2.Zero;
            float inverseCount = 1.0f / avoidance.Count;

            foreach (var actor in avoidance)
            {
                baryCenter += actor.Position * inverseCount;
                groupMovement += GetMovementForStep(actor, deltaTime) * inverseCount;
            }

            Vector2 toTargetPosition =
                baryCenter - oldPosition;

            if (Vector2.Dot(objFwd, toTargetPosition) < 0.0f)
            {
                return desiredMovement;
            }

            float groupRadius = 0.0f;

            foreach (var actor in avoidance)
            {
                float radius =
                    Vector2.Distance(
                        actor.Position,
                        baryCenter
                    ) + GetActorRadius(actor);

                groupRadius =
                    Math.Max(groupRadius, radius);
            }

            float minDistanceThreshold = Math.Clamp(
                Math.Min(
                    groupRadius,
                    myHardRadius
                ) * 0.25f,
                12.0f,
                15.0f
            );

            float softThreshold =
                groupRadius
                + mySoftRadius
                + minDistanceThreshold;

            Vector2 relPosition =
                baryCenter -
                (oldPosition + desiredMovement);

            float distanceSq =
                relPosition.LengthSquared();

            Vector2 collisionNormal =
                distanceSq > Epsilon
                    ? Vector2.Normalize(relPosition)
                    : objFwd;

            float pushDistance = Math.Max(
                softThreshold -
                (float)Math.Sqrt(distanceSq),
                0.0f
            );

            Vector2 groupForward;

            if (groupMovement.LengthSquared() > Epsilon)
            {
                groupForward =
                    Vector2.Normalize(groupMovement);
            }
            else if (toTargetPosition.LengthSquared() > 0.0f)
            {
                groupForward =
                    Vector2.Normalize(toTargetPosition);
            }
            else
            {
                groupForward = Vector2.Zero;
            }

            Vector2 objSide =
                new Vector2(
                    objFwd.Y,
                    -objFwd.X
                );

            float parallelness =
                Vector2.Dot(
                    objFwd,
                    groupForward
                );

            float sign;

            if (parallelness < -HeadOnAngleThreshold)
            {
                sign =
                    Vector2.Dot(
                        toTargetPosition,
                        objSide
                    ) > 0.0f
                        ? -1.0f
                        : 1.0f;
            }
            else if (parallelness > HeadOnAngleThreshold)
            {
                sign = 0.0f;

                if (desiredMovement.LengthSquared()
                    > groupMovement.LengthSquared())
                {
                    sign =
                        Vector2.Dot(
                            groupForward,
                            objSide
                        ) > 0.0f
                            ? -1.0f
                            : 1.0f;
                }
            }
            else
            {
                sign =
                    Vector2.Dot(
                        toTargetPosition,
                        objSide
                    ) > 0.0f
                        ? -1.0f
                        : 1.0f;
            }

            float sideDotAbs =
                Math.Abs(
                    Vector2.Dot(
                        collisionNormal,
                        objSide
                    )
                );

            float collisionSideFactor =
                pushDistance;

            if (sideDotAbs > Epsilon)
            {
                collisionSideFactor =
                    pushDistance / sideDotAbs;
            }

            float maxSideStep = (float)Math.Sqrt(cachedMovementLengthSq) * 0.4f;
            collisionSideFactor = Math.Clamp(
                collisionSideFactor, Math.Min(0.01f, maxSideStep), maxSideStep);

            Vector2 movement =
                desiredMovement
                + objSide
                * sign
                * collisionSideFactor;

            movement = ClampSoftMovement(movement, cachedMovementLengthSq);
            return limitSpeed ? LimitSteeringDistance(self, movement, movementBudget) : movement;
        }

        return desiredMovement;
    }
    private static Vector2 LimitSteeringDistance(ObjAIBase self, Vector2 movement, float budget)
    {
        var limited = LimitSteeringDistance(movement, budget);
        if (self is LaneMinion)
        {
            self.NavigationState.SolverConstraintDelta += limited - movement;
        }
        return limited;
    }
    private static Vector2 LimitSteeringDistance(Vector2 movement, float budget)
    {
        float lengthSq = movement.LengthSquared();
        if (lengthSq > budget * budget && lengthSq > 0)
            return movement * (budget / MathF.Sqrt(lengthSq));
        return movement;
    }
    private static Vector2 ClampHardMovement(
    Vector2 movement,
    float originalLengthSq)
    {
        float movementLengthSq = movement.LengthSquared();

        if (movementLengthSq <= Epsilon)
        {
            return movement;
        }

        if (Math.Abs(movementLengthSq - originalLengthSq)
            <= originalLengthSq * 0.75f)
        {
            return movement;
        }

        float newLengthSq = Math.Clamp(
            movementLengthSq,
            originalLengthSq * 0.625f,
            originalLengthSq * 1.375f
        );

        return movement *
               (float)Math.Sqrt(newLengthSq / movementLengthSq);
    }

    private static Vector2 ClampSoftMovement(
        Vector2 movement,
        float originalLengthSq)
    {
        float movementLengthSq = movement.LengthSquared();

        if (movementLengthSq <= Epsilon)
        {
            return movement;
        }

        if (Math.Abs(movementLengthSq - originalLengthSq)
            <= originalLengthSq * 0.25f)
        {
            return movement;
        }

        float newLengthSq = Math.Clamp(
            movementLengthSq,
            originalLengthSq * 0.875f,
            originalLengthSq * 1.125f
        );

        return movement *
               (float)Math.Sqrt(newLengthSq / movementLengthSq);
    }
    private static Vector2 ResolveHardCollisionPass(
    ObjAIBase self,
    Vector2 movement,
    float maxDistance,
    float deltaTime,
    List<ObjAIBase> colliders,
    out bool isStuckWithRepulse)
    {
        isStuckWithRepulse = false;

        float cachedMovementLengthSq =
            movement.LengthSquared();

        Vector2 objFwd = Vector2.Zero;

        if (cachedMovementLengthSq > 0.0f)
        {
            objFwd =
                movement /
                (float)Math.Sqrt(cachedMovementLengthSq);
        }

        Vector2 baryCenter = Vector2.Zero;
        float inverseCount = 1.0f / colliders.Count;

 
        foreach (var actor in colliders)
        {
            baryCenter += actor.Position * inverseCount;
        }

        float groupRadius = 0.0f;

        foreach (var actor in colliders)
        {
            float radius =
                Vector2.Distance(
                    actor.Position,
                    baryCenter
                )
                + GetActorRadius(actor);

            groupRadius =
                Math.Max(groupRadius, radius);
        }

        float myHardRadius =
            GetHardRadius(self);

        float minDistanceThreshold = Math.Clamp(
            Math.Min(
                groupRadius,
                myHardRadius
            ) * 0.25f,
            12.0f,
            20.0f
        );

        float collisionRadiusThreshold =
            myHardRadius
            + groupRadius
            + minDistanceThreshold;

        Vector2 nextPosition =
            self.Position + movement;

        Vector2 relPosition =
            baryCenter - nextPosition;

        Vector2 toTargetPosition =
            baryCenter - self.Position;

        float distanceSq =
            relPosition.LengthSquared();

        Vector2 collisionNormal =
            distanceSq > Epsilon
                ? Vector2.Normalize(relPosition)
                : objFwd;

        float pushDistance = Math.Max(
            collisionRadiusThreshold -
            (float)Math.Sqrt(distanceSq),
            0.0f
        );

        float normalProj =
            Vector2.Dot(
                objFwd,
                collisionNormal
            );

        if (Math.Abs(normalProj) > Epsilon)
        {
            float collisionFactor =
                Math.Clamp(
                    pushDistance /
                    Math.Abs(normalProj),
                    0.01f,
                    collisionRadiusThreshold
                );

            if (normalProj > 0.0f)
            {
                if (normalProj >= HeadOnAngleThreshold)
                {
                    Vector2 objSide =
                        new Vector2(
                            objFwd.Y,
                            -objFwd.X
                        );

                    float sign =
                        Vector2.Dot(
                            toTargetPosition,
                            objSide
                        ) > 0.0f
                            ? -1.0f
                            : 1.0f;

                    movement +=
                        objSide
                        * pushDistance
                        * PushForceMultiplier
                        * sign;
                }
                else
                {
                    movement +=
                        (
                            (
                                (
                                    objFwd -
                                    collisionNormal * normalProj
                                ) * 2.0f
                            )
                            - objFwd
                        )
                        * PushForceMultiplier
                        * collisionFactor;
                }
            }
            else
            {
                movement +=
                    objFwd * collisionFactor;
            }
        }
        else
        {
            Vector2 objSide =
                new Vector2(
                    objFwd.Y,
                    -objFwd.X
                );

            float sideDotAbs =
                Math.Abs(
                    Vector2.Dot(
                        collisionNormal,
                        objSide
                    )
                );

            if (sideDotAbs > Epsilon)
            {
                float collisionSideFactor =
                    Math.Clamp(
                        pushDistance / sideDotAbs,
                        0.01f,
                        collisionRadiusThreshold
                    );

                float sign =
                    Vector2.Dot(
                        toTargetPosition,
                        objSide
                    ) > 0.0f
                        ? -1.0f
                        : 1.0f;

                movement +=
                    objSide
                    * collisionSideFactor
                    * PushForceMultiplier
                    * sign;
            }
            else
            {
                movement -=
                    collisionNormal
                    * PushForceMultiplier
                    * pushDistance;
            }
        }

        float stuckThreshold =
            StuckSpeedRatio * maxDistance;

        if (movement.LengthSquared()
            <= stuckThreshold * stuckThreshold)
        {
            isStuckWithRepulse = true;

            float stuckTimeModifier =
                self.StuckTime /
                TimeBetweenRepaths
                + 1.0f;

            Vector2 objectToGroup =
                baryCenter - self.Position;

            float groupDistance =
                objectToGroup.Length();

            if (groupDistance > Epsilon)
            {
                float separationDistance =
                    Math.Min(
                        95.0f,
                        Math.Min(
                            maxDistance * 1.5f,
                            ExtraSeparationSpeed
                            * stuckTimeModifier
                            * stuckTimeModifier
                            * deltaTime
                        )
                    );

                movement -=
                    objectToGroup
                    * (
                        separationDistance /
                        groupDistance
                    );
            }
        }

        float movementLengthSq =
            movement.LengthSquared();

        if (Math.Abs(movementLengthSq - cachedMovementLengthSq)
                > 0.75f * cachedMovementLengthSq
            && movementLengthSq > Epsilon)
        {
            float newMovementLengthSq =
                Math.Clamp(
                    movementLengthSq,
                    cachedMovementLengthSq * 0.625f,
                    cachedMovementLengthSq * 1.375f
                );

            movement *=
                (float)Math.Sqrt(
                    newMovementLengthSq /
                    movementLengthSq
                );
        }

        return movement;
    }
}
