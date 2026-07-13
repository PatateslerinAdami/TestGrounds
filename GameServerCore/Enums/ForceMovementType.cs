namespace GameServerCore.Enums
{
    /// <summary>
    /// How a forced line-movement (dash/knockback) resolves its actual END POINT, given an aim
    /// point + range. Riot's <c>MovementType</c> param on the native <c>BBMove</c>/<c>BBMoveAway</c>
    /// (resolver <c>Actor::ServerGetLinePathDestination</c>, server-side C++ — its body exists in
    /// no decomp we have; the client only replays the waypoints the server computed). Enum values
    /// 0–4 are DWARF-confirmed. The per-value semantics below are REPLAY-DERIVED from the 4.20
    /// corpus by comparing server-resolved dash endpoints against the Map11 navgrid — method and
    /// numbers in docs/FORCEMOVEMENTTYPE_REPLAY_DERIVATION.md. All resolution happens at SETUP
    /// time (the first WaypointGroupWithSpeed already carries the final, clamped path).
    /// </summary>
    public enum ForceMovementType
    {
        /// <summary>Fly the full intended vector; only the ENDPOINT is validated — snapped to the
        /// nearest pathable point with NO distance cap, never refused. Walls BETWEEN start and
        /// endpoint do not shorten the dash (wall-crossing jumps). The default for ~all dashes and
        /// most knockups/knockbacks. Replay-verified on Tristana W / Corki W: 269 full flights,
        /// 0 refusals; a W aimed deep into a wall (endpoint 255u inside) still flew the full
        /// distance and landed on the nearest pathable spot beside the wall.</summary>
        FURTHEST_WITHIN_RANGE,
        /// <summary>The only mode where the PATH matters: the segment start→endpoint is sampled
        /// every ~cellsize (50u) of arc length and the movement is clamped to the last walkable
        /// sample before a blocked one. A stop therefore lands 0..50u short of the wall, and
        /// slivers thinner than one step are flown over. Our engine publishes OnCollisionTerrain
        /// when it clamps (wall-stun trigger). Replay-verified on Vayne E Condemn (475u @ 2500:
        /// stops quantize to ~50u of push travel; a 6u wall nub was skipped mid-push); S1 lua uses
        /// it for Condemn, Poppy R carry, SweepingBlow and BBMoveAway collisions.</summary>
        FIRST_COLLISION_HIT,
        /// <summary>Endpoint snapped to the nearest pathable point only when that is CLOSE (cap
        /// ~70u, bracketed (66, 74) by replays); an endpoint deeper inside terrain REFUSES the
        /// movement — the dash degenerates to a zero-length path at the current position (the
        /// wire still shows a real 0x64 with a [pos,pos] path, and the spell casts normally).
        /// No S1 spell passes this value; in 4.20 this capped-snap-or-refuse behavior is exactly
        /// what the (S1-)FIRST_WALL_HIT spells exhibit — see there.</summary>
        GET_NEAREST_IN_RANGE,
        /// <summary>As <see cref="GET_NEAREST_IN_RANGE"/> but unit bodies also block (the
        /// "same query, different collision mask" sibling). Used by Udyr Bear Stance's lunge,
        /// which pre-computes the edge position and passes ignoreTerrain, so the unit-blocking
        /// variant has never been observable; we resolve it like GET_NEAREST_IN_RANGE.</summary>
        GET_NEAREST_IN_RANGE_INCLUDE_UNITS,
        /// <summary>S1 scripts use this for Vayne Q tumble, Riven E, Fizz E hops. 4.20 replays of
        /// those spells show NO ray-clamping at walls — they resolve as capped-snap-or-refuse
        /// (identical to <see cref="GET_NEAREST_IN_RANGE"/>, and that is how we implement it):
        /// tumble into a thick wall = tumble-in-place (travel ≈ 0, cooldown spent), endpoint just
        /// inside a wall edge = snapped/full, thin slivers crossed freely. Consistent with Riot
        /// dropping FIRST_WALL_HIT from the 4.20 editor enum (SummonerParameterDefinitions.xml
        /// lists only values 0–3): the value was appended last (DWARF order), kept for old
        /// scripts' raw ints, and by 4.20 resolves like the GET_NEAREST family. The S1-era
        /// "clamp to the last walkable cell along the ray" reading is NOT what 4.20 does.</summary>
        FIRST_WALL_HIT
    }
}
