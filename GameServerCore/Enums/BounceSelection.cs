namespace GameServerCore.Enums;

/// <summary>
/// How a chain missile picks its next bounce target among the valid candidates.
/// Replay-verified per-spell split: the S1-era chain framework picks uniformly at
/// random (Fiddle E + Ryze E segment lengths both at 0.60·BounceRadius median, self/
/// revisit rates ≈ 1/N; MF Q's original Lua uses BBForEachUnitInTargetAreaRandom),
/// while Katarina Q (2012 rework) picks the closest (0.37·R median, max exactly at
/// the radius cap; the 4.20 tooltip literally says "bounces to the 4 closest
/// enemies").
/// </summary>
public enum BounceSelection
{
    Random,
    Nearest
}