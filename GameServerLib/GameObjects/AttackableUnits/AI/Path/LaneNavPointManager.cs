using System;
using System.Collections.Generic;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Path
{
    /// <summary>
    /// Lane forward-navigation: given a minion's position and its lane polyline, returns the next
    /// forward-nav point to move toward. Modelled on Riot's <c>NavPointManager::GetNextNavLocIter</c>
    /// (mac decomp 4.17 — <c>AINavPointManager.cpp:414-609</c>) but using a MONOTONIC progress index
    /// rather than Riot's stateless plane-projection scan.
    ///
    /// Why monotonic: Riot's nav points are dense, so its stateless plane gates are stable. Our polyline
    /// is coarse (~700u between points) and a stateless "closest segment" pick flip-flops between the
    /// point behind and the point ahead when the minion sits between two nav points — that issues
    /// alternating forward/backward MoveTo orders every tick and the wave freezes in place. A
    /// never-decreasing index is stable (no oscillation) and still resumes correctly from the furthest
    /// reached point after an off-lane chase. The advance radius is Riot's
    /// <c>pullback = min(segmentLength / 2, threshold)</c>.
    ///
    /// Enemy turrets gate the push via <paramref name="maxAllowedIndex"/>
    /// (= <see cref="LaneMinion.GetMaxAllowedWaypointIndex"/>); the minion walks all the way to that cap
    /// point (into the turret's acquisition range to attack it) and never advances past a live turret.
    /// </summary>
    public static class LaneNavPointManager
    {
        /// <summary>
        /// Returns the lane point to head toward (or <c>null</c> for an empty/invalid lane), advancing
        /// the caller-owned <paramref name="navIndex"/> monotonically.
        /// </summary>
        /// <param name="home">Segment origin behind lane[0] (the minion's spawn) for the first segment.</param>
        /// <param name="lane">The ordered lane polyline (forward direction for this minion's team).</param>
        /// <param name="maxAllowedIndex">Inclusive cap (turret gate), typically GetMaxAllowedWaypointIndex().</param>
        /// <param name="position">The minion's current position.</param>
        /// <param name="threshold">sNearPointThreshold (4.20 = 500; 4.17 decomp used 150).</param>
        /// <param name="navIndex">Caller-owned monotonic progress index; advanced in place.</param>
        public static Vector2? GetNextNavTarget(
            Vector2 home,
            IReadOnlyList<Vector2> lane,
            int maxAllowedIndex,
            Vector2 position,
            float threshold,
            ref int navIndex)
        {
            if (lane == null || lane.Count == 0)
            {
                return null;
            }

            int cap = Math.Min(maxAllowedIndex, lane.Count - 1);
            if (cap < 0)
            {
                return null;
            }

            if (navIndex < 0)
            {
                navIndex = 0;
            }

            // Advance forward (only) once we are within the current point's pullback radius OR have
            // projected past it along the incoming segment direction (handles overshooting a point
            // laterally/at speed without ever entering its pullback). Both checks are stable, so the
            // index only ever climbs. May step several points in one call when they are close together.
            while (navIndex < cap)
            {
                Vector2 segStart = navIndex == 0 ? home : lane[navIndex - 1];
                Vector2 point = lane[navIndex];
                float segLen = Vector2.Distance(segStart, point);
                float pullback = Math.Min(segLen * 0.5f, threshold);
                bool withinPullback = (point - position).Length() < pullback;
                bool pastPoint = Vector2.Dot(point - segStart, position - point) > 0f;
                if (withinPullback || pastPoint)
                {
                    navIndex++;
                }
                else
                {
                    break;
                }
            }

            if (navIndex > cap)
            {
                navIndex = cap;
            }

            return lane[navIndex];
        }
    }
}
