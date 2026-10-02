using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

// Shared acquisition policy, map scripts retain route and objective ownership.
// Events queue a reconsideration, never change targets recursively during damage/movement.
public sealed class LaneMinionTargetSelection
{
    private readonly LaneMinion _owner;
    private readonly Func<AttackableUnit, bool> _valid;
    private readonly Func<AttackableUnit, bool> _ignored;
    private readonly Func<AttackableUnit, int> _classify;
    private readonly Dictionary<AttackableUnit, (int Priority, float Expires)> _help = new();
    private readonly List<AttackableUnit> _expired = new();
    private float _now, _nextOverlap, _stickUntil;
    private AttackableUnit _stickyTarget, _observedTarget;
    private int _stickyPriority;
    private bool _overlapPending;
    private bool _pending;
    private float _retryAt = float.PositiveInfinity;
    public bool Pending { get => _pending || _now >= _retryAt; private set => _pending = value; }

    public LaneMinionTargetSelection(LaneMinion owner,
        Func<AttackableUnit, bool> valid, Func<AttackableUnit, bool> ignored,
        Func<AttackableUnit, int> classify)
    {
        _owner = owner;
        _valid = valid;
        _ignored = ignored;
        _classify = classify;
    }

    public void Advance(float milliseconds)
    {
        _now += milliseconds;
        if (_observedTarget != null && (_observedTarget.IsDead || _observedTarget.IsToRemove()
            || _owner.TargetUnit == null)) Pending = true;
        _observedTarget = _owner.TargetUnit;
        _expired.Clear();
        foreach (var entry in _help)
            if (entry.Value.Expires <= _now || entry.Key.IsDead || entry.Key.IsToRemove())
                _expired.Add(entry.Key);
        foreach (var unit in _expired) _help.Remove(unit);
        if (_stickyTarget != _owner.TargetUnit || _now >= _stickUntil)
            _stickyTarget = null;
    }

    public void RequestOverlap()
    {
        // Reuse collision solver neighbors, no extra neighborhood scan per event.
        if (_now < _nextOverlap) return;
        _overlapPending = true;
        Pending = true;
    }

    public float AcquisitionRange(AttackableUnit target, float ordinaryRange)
    {
        if (target != null && (_help.ContainsKey(target) || target == _stickyTarget))
        {
            var settings = GlobalData.CallForHelpVariables;
            float buffer = _owner.IsMelee ? settings.MeleeRadius : settings.RangedRadius;
            return Math.Max(ordinaryRange, _owner.Stats.Range.Total + target.CollisionRadius + buffer);
        }
        return ordinaryRange;
    }

    public void ReceiveHelp(AttackableUnit attacker, AttackableUnit victim)
    {
        if (attacker == null || victim == null || victim.Team != _owner.Team
            || attacker.Team == _owner.Team || attacker.IsDead || attacker.IsToRemove()
            || _owner.Status.HasFlag(StatusFlags.IgnoreCallForHelp)) return;
        int priority = (int)_owner.ClassifyTarget(attacker, victim);
        bool hadOld = _help.TryGetValue(attacker, out var old);
        // Lower priority damage must not keep an older, higher priority request alive forever.
        _help[attacker] = hadOld && old.Expires > _now && old.Priority < priority
            ? old : (priority, _now + Math.Max(0, GlobalData.CallForHelpVariables.Duration) * 1000f);
        if (!_valid(attacker) || _ignored(attacker))
        {
            if (hadOld) _help[attacker] = old;
            else _help.Remove(attacker);
            return;
        }
        // A continuing attack renews protection even if no target change is needed.
        if (attacker == _owner.TargetUnit) Protect(attacker, priority);
        Pending = true;
    }

    private void Protect(AttackableUnit target, int priority)
    {
        _stickyTarget = target;
        _stickyPriority = priority;
        _stickUntil = _now + Math.Max(0, GlobalData.CallForHelpVariables.Stick) * 1000f;
    }

    private int Priority(AttackableUnit unit)
    {
        int priority = _classify(unit);
        if (_help.TryGetValue(unit, out var request) && request.Expires > _now)
            priority = Math.Min(priority, request.Priority);
        if (unit == _stickyTarget && _now < _stickUntil)
            priority = Math.Min(priority, _stickyPriority);
        return priority;
    }

    private bool Better(AttackableUnit candidate, AttackableUnit best)
    {
        if (best == null) return true;
        int candidatePriority = Priority(candidate), bestPriority = Priority(best);
        if (candidatePriority != bestPriority) return candidatePriority < bestPriority;
        var current = _owner.TargetUnit;
        // Help of equal priority should not repeatedly cancel an existing attack.
        if (!_overlapPending || _owner.IsAttacking)
        {
            if (best == current) return false;
            if (candidate == current) return true;
        }
        float candidateDistance = Math.Max(0, Vector2.Distance(_owner.Position, candidate.Position)
            - candidate.CollisionRadius - _owner.Stats.Range.Total);
        float bestDistance = Math.Max(0, Vector2.Distance(_owner.Position, best.Position)
            - best.CollisionRadius - _owner.Stats.Range.Total);
        // An overlap may favor an easier approach, with hysteresis to avoid target churn.
        if (best == current) return candidateDistance + 40f < bestDistance;
        if (candidate == current) return candidateDistance <= bestDistance + 40f;
        return candidateDistance < bestDistance
            || (candidateDistance == bestDistance && candidate.NetId < best.NetId);
    }

    public AttackableUnit Select(IEnumerable<AttackableUnit> candidates)
    {
        AttackableUnit current = _owner.TargetUnit;
        AttackableUnit best = current != null && _valid(current) && !_ignored(current) ? current : null;
        foreach (var candidate in candidates)
            if (_valid(candidate) && !_ignored(candidate) && Better(candidate, best)) best = candidate;
        foreach (var candidate in _help.Keys)
            if (_valid(candidate) && !_ignored(candidate) && Better(candidate, best)) best = candidate;
        // Finish an explicitly uncancellable attack before changing its target. Retry without recursion.
        if (best != current && current != null && _valid(current) && _owner.IsAttacking
            && !_owner.HasAutoAttacked && _owner.AutoAttackSpell.SpellData.CantCancelWhileWindingUp)
        {
            Pending = false;
            _overlapPending = false;
            _retryAt = _now + 50f;
            _nextOverlap = _now + 200f;
            return current;
        }
        _retryAt = float.PositiveInfinity;
        if (best != null && _help.TryGetValue(best, out var request) && request.Expires > _now
            && best != current) Protect(best, request.Priority);
        Pending = false;
        _overlapPending = false;
        _nextOverlap = _now + 200f;
        return best;
    }
}
