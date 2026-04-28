using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class TrundleCircle : IBuffGameScript {
    private const float PillarDurationSeconds = 6.0f;
    private const float KnockbackDistance     = 225.0f;
    private const float KnockbackSpeed        = 600.0f;
    private const float SlowRadius            = 360.0f;

    private readonly HashSet<AttackableUnit> _unitsInSlowRange = [];

    private ObjAIBase      _trundle;
    private AttackableUnit _pillar;
    private Spell          _spell;
    private Particle       _p1, _p2;
    private float          _timerSeconds;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _trundle      = ownerSpell.CastInfo.Owner;
        _pillar       = unit;
        _spell        = ownerSpell;
        _timerSeconds = 0.0f;

        var unitsInRange = GetUnitsInRange(
            _trundle,
            _pillar.Position,
            KnockbackDistance,
            true,
            SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectNeutral |
            SpellDataFlags.AffectEnemies |
            SpellDataFlags.AffectFriends
        );
        foreach (var target in unitsInRange.Where(target => target != _pillar))
        {
            CancelDash(target);

            var away = target.Position - _pillar.Position;
            if (!IsFiniteNonZero(away))
                away = target.Position - _trundle.Position;
            away = NormalizeOrDefault(away, Vector2.UnitX);

            var targetPos = _pillar.Position + away * KnockbackDistance;
            ForceMovement(
                target,
                "RUN",
                targetPos,
                KnockbackSpeed,
                0f,
                0f,
                0f,
                true,
                ForceMovementType.FURTHEST_WITHIN_RANGE,
                ForceMovementOrdersType.CANCEL_ORDER,
                ForceMovementOrdersFacing.KEEP_CURRENT_FACING
            );
        }

        _p1 = AddParticleTarget(_trundle, unit, "Trundle_E_green",            unit, buff.Duration, enemyParticle: "Trundle_E_red");
        _p2 = AddParticleTarget(_trundle, unit, "Trundle_Base_E_PlagueBlock", unit, buff.Duration);
    }

    public void OnUpdate(float diff) {
        _timerSeconds += diff * 0.001f;

        var enemiesInRange = GetUnitsInRange(
            _trundle,
            _pillar.Position,
            SlowRadius,
            true,
            SpellDataFlags.AffectEnemies |
            SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectNeutral
        );

        foreach (var enemy in _unitsInSlowRange.Where(enemy => !enemiesInRange.Contains(enemy)).ToList()) {
            _unitsInSlowRange.Remove(enemy);
            RemoveBuff(enemy, "TrundleCircleSlow");
        }

        var remainingDuration = Math.Max(0.0f, PillarDurationSeconds - _timerSeconds);
        if (remainingDuration <= 0.0f) return;

        foreach (var enemy in enemiesInRange) {
            _unitsInSlowRange.Add(enemy);
            if (!enemy.HasBuff("TrundleCircleSlow"))
                AddBuff("TrundleCircleSlow", remainingDuration, 1, _spell, enemy, _trundle);
        }
    }
    
    private static Vector2 NormalizeOrDefault(Vector2 value, Vector2 fallback) {
        if (IsFiniteNonZero(value)) {
            var normalized = Vector2.Normalize(value);
            if (IsFiniteNonZero(normalized)) {
                return normalized;
            }
        }

        if (!IsFiniteNonZero(fallback)) return Vector2.UnitX;
        var normalizedFallback = Vector2.Normalize(fallback);
        return IsFiniteNonZero(normalizedFallback) ? normalizedFallback : Vector2.UnitX;
    }
    
    private static bool IsFiniteNonZero(Vector2 value) {
        return float.IsFinite(value.X) &&
               float.IsFinite(value.Y) &&
               value.LengthSquared() > 0.0001f;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        foreach (var slowed in _unitsInSlowRange.ToList().OfType<AttackableUnit>().Where(slowed => slowed.HasBuff("TrundleCircleSlow")))
            RemoveBuff(slowed, "TrundleCircleSlow");
        _unitsInSlowRange.Clear();

        AddBuff("PillarExpirationTimer", 0.25f, 1, _spell, unit, _trundle);
        RemoveParticle(_p1);
        RemoveParticle(_p2);
    }
}
