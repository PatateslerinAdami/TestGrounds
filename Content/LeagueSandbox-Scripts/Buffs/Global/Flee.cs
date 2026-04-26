using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class Flee : IBuffGameScript {
    private const float FleeMoveSpeedPenalty = 0.9f;
    private const float FleeDistance = 900f;
    private const float StuckRepathDelayMilliseconds = 250f;
    private const float StuckDistanceThreshold = 8f;

    private ObjAIBase _owner;
    private AttackableUnit _unit;
    private Particle _fleeParticle;
    private float _stuckTimer;
    private Vector2 _lastPosition;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.FLEE,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    private bool IsMovementOverriddenByHardCc() {
        if (_unit == null) {
            return false;
        }

        return _unit.Status.HasFlag(StatusFlags.Stunned)
               || _unit.Status.HasFlag(StatusFlags.Rooted)
               || _unit.Status.HasFlag(StatusFlags.Netted)
               || _unit.HasBuffType(BuffType.KNOCKUP)
               || _unit.MovementParameters != null;
    }

    private void ForceFleeMovement() {
        if (_unit == null || _owner == null || _unit.IsDead) {
            return;
        }

        if (IsMovementOverriddenByHardCc()) {
            return;
        }

        if (_unit is ObjAIBase aiUnit) {
            if (aiUnit.IsAttacking) {
                aiUnit.CancelAutoAttack(true, true);
            }

            aiUnit.SetTargetUnit(null, true);
            aiUnit.UpdateMoveOrder(OrderType.MoveTo, false);
        }

        var fleeVector = _unit.Position - _owner.Position;
        if (fleeVector.LengthSquared() <= float.Epsilon) {
            fleeVector = new Vector2(_unit.Direction.X, _unit.Direction.Z);
        }

        if (fleeVector.LengthSquared() <= float.Epsilon) {
            fleeVector = new Vector2(1f, 0f);
        }

        var fleeDirection = Vector2.Normalize(fleeVector);
        var fleeTarget = _unit.Position + fleeDirection * FleeDistance;
        var fleePath = GetPath(_unit.Position, fleeTarget, _unit.PathfindingRadius);

        if (fleePath != null && fleePath.Count > 1) {
            _unit.SetWaypoints(fleePath);
        } else {
            _unit.SetWaypoints(new List<Vector2> {
                _unit.Position,
                fleeTarget
            });
        }
    }

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit = unit;
        _owner = ownerSpell?.CastInfo.Owner ?? buff.SourceUnit;
        _stuckTimer = 0f;
        _lastPosition = unit.Position;

        var particleOwner = ownerSpell?.CastInfo.Owner ?? buff.SourceUnit ?? unit;
        _fleeParticle = AddParticleTarget(particleOwner, unit, "Global_Fear.troy", unit, buff.Duration);

        StatsModifier.MoveSpeed.PercentBonus -= FleeMoveSpeedPenalty;
        _unit.AddStatModifier(StatsModifier);

        buff.SetStatusEffect(StatusFlags.Feared, true);
        ApplyAssistMarker(buff.SourceUnit, unit, 10.0f);

        ForceFleeMovement();
    }

    public void OnUpdate(float diff) {
        if (_unit == null || _owner == null || _unit.IsDead) {
            return;
        }

        if (IsMovementOverriddenByHardCc()) {
            _stuckTimer = 0f;
            _lastPosition = _unit.Position;
            return;
        }

        if (_unit.IsPathEnded()) {
            ForceFleeMovement();
            _stuckTimer = 0f;
            _lastPosition = _unit.Position;
            return;
        }

        var distanceMovedSquared = Vector2.DistanceSquared(_unit.Position, _lastPosition);
        if (distanceMovedSquared > StuckDistanceThreshold * StuckDistanceThreshold) {
            _lastPosition = _unit.Position;
            _stuckTimer = 0f;
            return;
        }

        _stuckTimer += diff;
        if (_stuckTimer < StuckRepathDelayMilliseconds) {
            return;
        }

        _stuckTimer = 0f;
        _lastPosition = _unit.Position;
        ForceFleeMovement();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (!IsMovementOverriddenByHardCc()) {
            unit.StopMovement();
        }

        RemoveParticle(_fleeParticle);
    }
}
