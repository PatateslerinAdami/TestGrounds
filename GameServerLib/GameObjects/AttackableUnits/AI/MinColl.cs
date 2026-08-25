using GameServerCore.Enums;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public static class MinColl
    {
        const float PushForceMultiplier = 5.1f;
        const float HeadOnAngleThreshold = 0.707f;
        const float Epsilon = 1e-9f;
        const float StuckSpeedRatio = 0.1f;
        const float SeparationSpeed = 25.0f;

        public static Vector2 CalculateSteeredMovement(ObjAIBase self, Vector2 desiredMovement, float maxDistance, float deltaTime, IEnumerable<ObjAIBase> neighbors, out bool isStuck)
        {
            isStuck = false;

            if (desiredMovement.LengthSquared() <= Epsilon)
                return desiredMovement;

            Vector2 nextPosition = self.Position + desiredMovement;
            Vector2 objFwd = Vector2.Normalize(desiredMovement);
            float cachedMovementLengthSq = desiredMovement.LengthSquared();

            List<ObjAIBase> colliders = new List<ObjAIBase>();
            List<ObjAIBase> avoidance = new List<ObjAIBase>();

            float myHardRadius = self.CollisionRadius;
            float mySoftRadius = myHardRadius * 2.0f;

            foreach (var testActor in neighbors)
            {
                if (testActor == self || testActor.IsDead || testActor.Status.HasFlag(StatusFlags.Ghosted))
                    continue;

                float testHardRadius = testActor.CollisionRadius;
                float dx = testActor.Position.X - nextPosition.X;
                float dy = testActor.Position.Y - nextPosition.Y;
                float distanceSq = dx * dx + dy * dy;

                float minDistanceThreshold = Math.Clamp(Math.Min(myHardRadius, testHardRadius) * 0.25f, 12.0f, 20.0f);
                float collisionRadiusThresholdSq = (testHardRadius + myHardRadius + minDistanceThreshold) * (testHardRadius + myHardRadius + minDistanceThreshold);

                if (distanceSq < collisionRadiusThresholdSq)
                {
                    colliders.Add(testActor);
                }
                else if (distanceSq < (testHardRadius + mySoftRadius) * (testHardRadius + mySoftRadius))
                {
                    if (Math.Abs(self.Velocity.X) > Epsilon || Math.Abs(self.Velocity.Y) > Epsilon)
                    {
                        float dmx = self.Velocity.X - testActor.Velocity.X;
                        float dmy = self.Velocity.Y - testActor.Velocity.Y;
                        if (Math.Abs(dmx) > Epsilon || Math.Abs(dmy) > Epsilon)
                        {
                            avoidance.Add(testActor);
                        }
                    }
                }
            }

            Vector2 finalMovement = desiredMovement;

            if (colliders.Count > 0)
            {
                Vector2 baryCenter = Vector2.Zero;
                foreach (var c in colliders) baryCenter += c.Position;
                baryCenter /= colliders.Count;

                float collisionRadiusThreshold = 0.0f;
                foreach (var c in colliders)
                {
                    float distToBary = Vector2.Distance(c.Position, baryCenter);
                    collisionRadiusThreshold = Math.Max(collisionRadiusThreshold, distToBary + c.CollisionRadius);
                }

                float minDistanceThreshold = Math.Clamp(Math.Min(collisionRadiusThreshold, myHardRadius) * 0.25f, 12.0f, 20.0f);
                collisionRadiusThreshold += myHardRadius + minDistanceThreshold;

                Vector2 relPosition = baryCenter - nextPosition;
                float distanceSq = relPosition.LengthSquared();
                Vector2 collisionNormal = distanceSq > Epsilon ? Vector2.Normalize(relPosition) : objFwd;

                float pushDistance = Math.Max(collisionRadiusThreshold - (float)Math.Sqrt(distanceSq), 0.0f);
                float normalProj = Vector2.Dot(objFwd, collisionNormal);

                if (Math.Abs(normalProj) > Epsilon)
                {
                    float factor = Math.Clamp(pushDistance / Math.Abs(normalProj), 0.01f, collisionRadiusThreshold);
                    if (normalProj > 0.0f)
                    {
                        if (normalProj >= HeadOnAngleThreshold)
                        {
                            Vector2 objSide = new Vector2(objFwd.Y, -objFwd.X);
                            Vector2 toTgPosition = baryCenter - self.Position;
                            float sign = Vector2.Dot(toTgPosition, objSide) > 0.0f ? -1.0f : 1.0f;
                            finalMovement += objSide * (pushDistance * PushForceMultiplier * sign);
                        }
                        else
                        {
                            finalMovement += (((objFwd - collisionNormal * normalProj) * 2.0f) - objFwd) * PushForceMultiplier * factor;
                        }
                    }
                    else
                    {
                        finalMovement += objFwd * factor;
                    }
                }
                else
                {
                    Vector2 objSide = new Vector2(objFwd.Y, -objFwd.X);
                    float sideDotAbs = Math.Abs(Vector2.Dot(collisionNormal, objSide));
                    if (sideDotAbs > Epsilon)
                    {
                        float collisionSideFactor = Math.Clamp(pushDistance / sideDotAbs, 0.01f, collisionRadiusThreshold);
                        Vector2 toTgPosition = baryCenter - self.Position;
                        float sign = Vector2.Dot(toTgPosition, objSide) > 0.0f ? -1.0f : 1.0f;
                        finalMovement += objSide * (collisionSideFactor * PushForceMultiplier * sign);
                    }
                    else
                    {
                        finalMovement -= collisionNormal * PushForceMultiplier * pushDistance;
                    }
                }

                float speedThreshForStuckSq = (StuckSpeedRatio * maxDistance) * (StuckSpeedRatio * maxDistance);
                if (finalMovement.LengthSquared() <= speedThreshForStuckSq)
                {
                    isStuck = true;
                    Vector2 objToTestActor = baryCenter - self.Position;
                    float sepScale = Math.Min(95.0f, Math.Min(maxDistance * 1.5f, SeparationSpeed * deltaTime)) / Math.Max(objToTestActor.Length(), 0.01f);
                    finalMovement -= objToTestActor * sepScale;
                }
            }
            else if (avoidance.Count > 0)
            {
                Vector2 baryCenter = Vector2.Zero;
                Vector2 groupVelocity = Vector2.Zero;
                foreach (var a in avoidance)
                {
                    baryCenter += a.Position;
                    groupVelocity += a.Velocity;
                }
                baryCenter /= avoidance.Count;
                groupVelocity /= avoidance.Count;

                Vector2 toTgPosition = baryCenter - self.Position;
                if (Vector2.Dot(objFwd, toTgPosition) >= 0.0f) 
                {
                    float collisionRadiusThreshold = 0.0f;
                    foreach (var a in avoidance)
                    {
                        float distToBary = Vector2.Distance(a.Position, baryCenter);
                        collisionRadiusThreshold = Math.Max(collisionRadiusThreshold, distToBary + a.CollisionRadius);
                    }

                    float minDistanceThreshold = Math.Clamp(Math.Min(collisionRadiusThreshold, myHardRadius) * 0.25f, 12.0f, 15.0f);
                    float softCollisionRadiusThreshold = collisionRadiusThreshold + mySoftRadius + minDistanceThreshold;

                    Vector2 relPosition = baryCenter - nextPosition;
                    float distanceSq = relPosition.LengthSquared();
                    Vector2 collisionNormal = distanceSq > Epsilon ? Vector2.Normalize(relPosition) : objFwd;
                    float pushDistance = Math.Max(softCollisionRadiusThreshold - (float)Math.Sqrt(distanceSq), 0.0f);

                    Vector2 inObjFwd = groupVelocity.LengthSquared() > Epsilon ? Vector2.Normalize(groupVelocity) : Vector2.Normalize(toTgPosition);
                    Vector2 objSide = new Vector2(objFwd.Y, -objFwd.X);
                    float parallelness = Vector2.Dot(objFwd, inObjFwd);

                    float sign;
                    if (parallelness < -HeadOnAngleThreshold)
                    {
                        sign = Vector2.Dot(toTgPosition, objSide) > 0.0f ? -1.0f : 1.0f;
                    }
                    else if (parallelness > HeadOnAngleThreshold)
                    {
                        sign = 0.0f;
                        if (desiredMovement.LengthSquared() > groupVelocity.LengthSquared())
                        {
                            sign = Vector2.Dot(inObjFwd, objSide) > 0.0f ? -1.0f : 1.0f;
                        }
                    }
                    else
                    {
                        sign = Vector2.Dot(toTgPosition, objSide) > 0.0f ? -1.0f : 1.0f;
                    }

                    float sideDotAbs = Math.Abs(Vector2.Dot(collisionNormal, objSide));
                    float collisionSideFactor = sideDotAbs > Epsilon ? pushDistance / sideDotAbs : pushDistance;

                    collisionSideFactor = Math.Clamp(collisionSideFactor, 0.01f, (float)Math.Sqrt(cachedMovementLengthSq) * 0.4f);
                    finalMovement += objSide * (sign * collisionSideFactor);
                }
            }

            if (Math.Abs(finalMovement.X) > Epsilon || Math.Abs(finalMovement.Y) > Epsilon)
            {
                float fMovementLengthSq = finalMovement.LengthSquared();
                if (Math.Abs(fMovementLengthSq - cachedMovementLengthSq) > 0.25f * cachedMovementLengthSq && fMovementLengthSq > Epsilon)
                {
                    float fNewMovementLengthSq = Math.Clamp(fMovementLengthSq, cachedMovementLengthSq * 0.875f, cachedMovementLengthSq * 1.125f);
                    finalMovement *= (float)Math.Sqrt(fNewMovementLengthSq / fMovementLengthSq);
                }
            }

            return finalMovement;
        }
    }
}