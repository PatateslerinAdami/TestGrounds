using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Movement;

/// <summary>
/// Local response to an authoritative waypoint path. Route planning stays in
/// PathingHandler, this stage does not choose a separate crowd velocity goal.
/// </summary>
public sealed class AiPathMotionSolver
{
    private readonly Game _game;
    private readonly List<ObjAIBase> _neighbors = new(32);

    public AiPathMotionSolver(Game game) => _game = game;

    public Vector2 Solve(ObjAIBase actor, Vector2 requested, float seconds)
    {
        actor.NavigationState.SolverConstraintDelta = Vector2.Zero;
        if (!float.IsFinite(seconds) || seconds <= 0
            || !float.IsFinite(requested.X) || !float.IsFinite(requested.Y))
            return Vector2.Zero;

        float budget = Math.Min(requested.Length(), Math.Max(0, actor.GetMoveSpeed()) * seconds);
        if (!float.IsFinite(budget) || budget <= 0) return Vector2.Zero;
        requested = Limit(requested, budget);

        _game.Map.CollisionHandler.GetAIUnitsInRadius(actor.Position + requested,
            MinColl.GetNeighborQueryRadius(actor), _neighbors);
        if (actor is LaneMinion)
        {
            foreach (var neighbor in _neighbors)
            {
                if (neighbor is not LaneMinion || !MinColl.CanCollide(actor, neighbor)) continue;
                float radius = MinColl.GetHardRadius(actor) + MinColl.GetHardRadius(neighbor);
                if (Vector2.DistanceSquared(actor.Position, neighbor.Position) < radius * radius)
                {
                    // The script only records a pending event, target/path changes happen in its update.
                    actor.AIScript.OnMovementOverlap();
                    break;
                }
            }
        }
        Vector2 movement = MinColl.CalculateSteeredMovement(actor, requested,
            budget, seconds, _neighbors, out _, limitSpeed: true);
        if (!float.IsFinite(movement.X) || !float.IsFinite(movement.Y))
            return Vector2.Zero;
        var raw = movement;
        movement = Limit(movement, budget);
        actor.NavigationState.SolverConstraintDelta += movement - raw;

        // Never commit an avoidance response through terrain. Recovery owns
        // finding a different route, no post integration penetration push.
        var accepted = _game.Map.NavigationGrid.CastCircle(actor.Position,
            actor.Position + movement, actor.PathfindingRadius) ? Vector2.Zero : movement;
        actor.NavigationState.SolverConstraintDelta += accepted - movement;
        return accepted;
    }

    private static Vector2 Limit(Vector2 movement, float budget)
    {
        float length = movement.Length();
        return length > budget ? movement * (budget / length) : movement;
    }
}
