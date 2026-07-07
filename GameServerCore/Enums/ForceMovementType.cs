namespace GameServerCore.Enums
{
    /// <summary>
    /// How a forced line-movement (dash/knockback) resolves its actual END POINT, given an aim
    /// point + range. Riot's <c>MovementType</c> param on the native <c>BBMove</c>/<c>BBMoveAway</c>
    /// (resolver <c>Actor::ServerGetLinePathDestination</c>, C++-only — its body is not in the decomp,
    /// and neither the 4.20 nor S1 Lua implement it). Semantics below are grounded in the real S1
    /// spell-data usage (s1 lua/GameClient/DATA/Spells), not a byte-faithful port.
    /// </summary>
    public enum ForceMovementType
    {
        /// <summary>Travel the full intended vector toward the aim point, clamped to range + terrain.
        /// The default for essentially all dashes and most knockups/knockbacks (Riven Q, Renekton,
        /// Lee Sin R, Jarvan R, Graves/Leblanc/Shen, Fizz Jump, Fling/Headbutt/BusterShot…).</summary>
        FURTHEST_WITHIN_RANGE,
        /// <summary>Movement ends/registers at the first hit from the COLLISION SYSTEM along the path —
        /// which includes DYNAMIC collision objects (champion-created walls: Anivia W, Jarvan R, Trundle
        /// pillar) on top of static terrain. This is why Vayne Condemn / Poppy Heroic Charge stun a target
        /// slammed into a Jarvan/Anivia/Trundle wall, not just real terrain. Distinct from (not synonymous
        /// with) <see cref="FIRST_WALL_HIT"/>, which only sees static navgrid geometry. Unimplemented here
        /// — our Vayne E currently substitutes FIRST_WALL_HIT, so it misses collision-object walls (e.g. the
        /// Trundle pillar, which is a collision Minion, not a navgrid cell). Faithful impl: raycast stopping
        /// at the first movement-blocking collision object (via CollisionHandler / IsWalkable checkObjects),
        /// NOT at pass-through units.</summary>
        FIRST_COLLISION_HIT,
        /// <summary>Snap the endpoint to the nearest reachable point in range (terrain-aware). No S1
        /// spell uses it; in our engine the caller already positions the endpoint, so this resolves
        /// like FURTHEST_WITHIN_RANGE (terrain-clamped).</summary>
        GET_NEAREST_IN_RANGE,
        /// <summary>As <see cref="GET_NEAREST_IN_RANGE"/> but unit bodies also block. Used by Udyr
        /// Bear Stance lunge; the caster script pre-computes the edge position, so no extra engine
        /// resolution is applied.</summary>
        GET_NEAREST_IN_RANGE_INCLUDE_UNITS,
        /// <summary>Dash until the first terrain/wall hit and stop there (enables wall-stuns/pins).
        /// S1: Vayne Q tumble, Riven E (Feint), Fizz hop/knockup.</summary>
        FIRST_WALL_HIT
    }
}
