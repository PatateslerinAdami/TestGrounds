using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Movement;

public sealed class ChampionMotionSolver
{
    private readonly Game _game;
    private readonly List<ObjAIBase> _neighbors = new(16);
    public ChampionMotionSolver(Game game) => _game = game;

    public Vector2 Solve(Champion actor, Vector2 requested, float seconds)
    {
        if (seconds <= 0 || requested.LengthSquared() <= .00000001f) return Vector2.Zero;
        float budget = Math.Min(requested.Length(), Math.Max(0, actor.GetMoveSpeed()) * seconds);
        requested = ClampLength(requested, budget);
        _game.Map.CollisionHandler.GetAIUnitsInRadius(actor.Position + requested,
            MinColl.GetNeighborQueryRadius(actor), _neighbors);
        var movement = MinColl.CalculateSteeredMovement(actor, requested, budget, seconds,
            _neighbors, out _, limitSpeed: true);
        movement = ClampLength(movement, budget);
        if (!float.IsFinite(movement.X) || !float.IsFinite(movement.Y)) return Vector2.Zero;
        return _game.Map.NavigationGrid.CastCircle(actor.Position, actor.Position + movement, actor.PathfindingRadius)
            ? Vector2.Zero : movement;
    }
    private static Vector2 ClampLength(Vector2 value, float maximum)
    {
        float length = value.Length();
        return length > maximum && length > 0 ? value * (maximum / length) : value;
    }
}
