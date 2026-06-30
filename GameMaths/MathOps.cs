using System;

namespace GameMaths
{
    /// <summary>
    /// Riot-faithful scalar math primitives mirroring the BuildingBlocks <c>MO_*</c> operators
    /// (BuildingBlocksBase.lua) where C#'s defaults diverge from Lua's. Use these when porting a Riot
    /// formula so behaviour matches at the edge cases the idiomatic C# operators get wrong:
    /// <list type="bullet">
    /// <item><see cref="RoundHalfUp"/> — Lua <c>MO_ROUND = floor(x + 0.5)</c> rounds halves UP, whereas
    /// <c>Math.Round</c>/<c>MathF.Round</c> use banker's rounding (half-to-even) and differ at <c>x.5</c>.</item>
    /// <item><see cref="FlooredMod"/> — Lua <c>MO_MODULO</c> (<c>%</c>) is floored (result takes the divisor's
    /// sign), whereas C# <c>%</c> is truncated (result takes the dividend's sign) for negative operands.</item>
    /// <item><see cref="RandIntRange"/> — Lua <c>MO_RAND_INT_RANGE = math.random(a, b)</c> is inclusive of
    /// BOTH ends, whereas <c>Random.Next(a, b)</c> excludes the upper bound.</item>
    /// </list>
    /// The remaining MO_* operators (ADD/SUB/MUL, MIN/MAX/ABS, POW/SQRT, ROUNDUP=ceil, ROUNDDOWN=floor,
    /// boolean ops) map 1:1 onto the standard C# operators / <c>Math</c> and need no wrapper.
    /// Trig (<c>MO_SIN/COS/TAN</c> take degrees, <c>MO_ASIN/ACOS/ATAN</c> return degrees) is handled by
    /// the degree/radian helpers in <c>GameServerCore.Extensions</c>; do not feed degrees to MathF.Sin.
    /// </summary>
    public static class MathOps
    {
        /// <summary>
        /// Round-half-up: <c>floor(x + 0.5)</c>. Faithful to Riot <c>MO_ROUND</c>. Returns a whole
        /// <see cref="float"/> (cast to int at the call site if needed). Differs from
        /// <c>MathF.Round</c> only at exact <c>.5</c> boundaries, where this always rounds toward +∞.
        /// </summary>
        public static float RoundHalfUp(float x)
        {
            return MathF.Floor(x + 0.5f);
        }

        /// <summary>
        /// Floored modulo: result always has the sign of <paramref name="divisor"/> (Lua <c>%</c> /
        /// Riot <c>MO_MODULO</c>). For non-negative operands this equals C#'s <c>%</c>.
        /// </summary>
        public static float FlooredMod(float value, float divisor)
        {
            return ((value % divisor) + divisor) % divisor;
        }

        /// <summary>
        /// Floored integer modulo: result always in <c>[0, divisor)</c> for a positive
        /// <paramref name="divisor"/> (Lua <c>%</c> / Riot <c>MO_MODULO</c>).
        /// </summary>
        public static int FlooredMod(int value, int divisor)
        {
            return ((value % divisor) + divisor) % divisor;
        }

        /// <summary>
        /// Inclusive integer range [<paramref name="min"/>, <paramref name="max"/>] — faithful to Riot
        /// <c>MO_RAND_INT_RANGE</c> / Lua <c>math.random(a, b)</c> (both ends inclusive). The caller
        /// supplies the RNG so determinism/seeding stays in its control.
        /// </summary>
        public static int RandIntRange(Random rng, int min, int max)
        {
            return rng.Next(min, max + 1);
        }
    }
}
