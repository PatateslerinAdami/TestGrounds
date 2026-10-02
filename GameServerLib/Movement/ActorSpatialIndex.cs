using System;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Movement;

// Center indexed buckets, query expansion covers the maximum 250 unit actor radius.
// Updates are sequential, so later actors see earlier accepted movement this tick.
public sealed class ActorSpatialIndex
{
    private const float CellSize = 256;
    private readonly Dictionary<(int, int), List<ObjAIBase>> _cells = new();
    private readonly Dictionary<ObjAIBase, (int, int)> _membership = new();
    public int LastCandidatesVisited { get; private set; }
    private static (int, int) Cell(Vector2 p) => ((int)MathF.Floor(p.X / CellSize), (int)MathF.Floor(p.Y / CellSize));
    public void Remove(ObjAIBase actor)
    {
        if (!_membership.Remove(actor, out var key)) return;
        var bucket = _cells[key];
        bucket.Remove(actor);
        if (bucket.Count == 0) _cells.Remove(key);
    }
    public void Update(ObjAIBase actor)
    {
        if (actor.IsDead || actor.IsToRemove()) { Remove(actor); return; }
        var key = Cell(actor.Position);
        if (_membership.TryGetValue(actor, out var old) && old == key)
        {
            return;
        }
        Remove(actor);
        if (!_cells.TryGetValue(key, out var bucket)) _cells[key] = bucket = new List<ObjAIBase>();
        bucket.Add(actor);
        _membership[actor] = key;
    }
    public void Query(Vector2 center, float radius, List<ObjAIBase> result)
    {
        result.Clear();
        LastCandidatesVisited = 0;
        var min = Cell(center - new Vector2(radius + 250));
        var max = Cell(center + new Vector2(radius + 250));
        for (int y = min.Item2; y <= max.Item2; y++)
            for (int x = min.Item1; x <= max.Item1; x++)
            {
                if (!_cells.TryGetValue((x, y), out var bucket)) continue;
                foreach (var actor in bucket)
                {
                    LastCandidatesVisited++;
                    float combined = radius + MinColl.GetActorRadius(actor);
                    var position = actor.Position;
                    if (!actor.IsDead && !actor.IsToRemove() && Vector2.DistanceSquared(center, position) <= combined * combined)
                        result.Add(actor);
                }
            }
        // Stable across bucket transfers, this ordering is emulator policy.
        result.Sort(static (a, b) => a.NetId.CompareTo(b.NetId));
    }
}
