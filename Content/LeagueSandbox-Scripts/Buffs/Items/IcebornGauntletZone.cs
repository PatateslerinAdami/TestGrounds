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

public class IcebornGauntletZone : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; } = new()
    {
        BuffType = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    private ObjAIBase _owner;
    private Vector2 _center;
    private float _radius;
    private float _slowPercent;
    private float _durationSeconds;
    private float _timerSeconds;
    private Particle _fieldParticle;
    private readonly HashSet<AttackableUnit> _unitsInRange = new HashSet<AttackableUnit>();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        if (unit is not ObjAIBase ai)
        {
            return;
        }

        _owner = ai;
        _center = new Vector2(buff.Variables.GetFloat("centerX", 0.0f), buff.Variables.GetFloat("centerY", 0.0f));
        _radius = buff.Variables.GetFloat("radius", 210.0f);
        _slowPercent = buff.Variables.GetFloat("slowPercent", 0.30f);
        _durationSeconds = buff.Duration;
        _timerSeconds = 0.0f;

        _fieldParticle = AddParticlePos(
            _owner,
            "item_frozen_gauntlet_green",
            _center,
            _center,
            buff.Duration,
            size: GetParticleScale(),
            enemyParticle: "item_frozen_gauntlet_red"
        );
    }

    public void OnUpdate(float diff)
    {
        if (_owner != null && _owner.IsDead)
        {
            KillParticle(ref _fieldParticle);
            return;
        }

        _timerSeconds += diff * 0.001f;

        var remainingDuration = Math.Max(0.0f, _durationSeconds - _timerSeconds);
        if (remainingDuration <= 0.0f)
        {
            return;
        }

        var enemiesInRange = GetUnitsInRange(
            _owner,
            _center,
            _radius,
            true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral
        );

        foreach (var enemy in _unitsInRange.Where(enemy => !enemiesInRange.Contains(enemy)).ToList())
        {
            _unitsInRange.Remove(enemy);
            RemoveBuff(enemy, "IcebornGauntletSlow");
        }

        foreach (var enemy in enemiesInRange)
        {
            if (!_unitsInRange.Contains(enemy))
            {
                _unitsInRange.Add(enemy);
            }

            if (!enemy.HasBuff("IcebornGauntletSlow"))
            {
                var variables = new BuffVariables();
                variables.Set("slowPercent", _slowPercent);
                AddBuff("IcebornGauntletSlow", remainingDuration, 1, null, enemy, _owner, buffVariables: variables);
            }
        }
    }

    private float GetParticleScale()
    {
        return Math.Max(1.0f, _radius / 210.0f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        foreach (var enemy in _unitsInRange.ToList())
        {
            RemoveBuff(enemy, "IcebornGauntletSlow");
        }
        _unitsInRange.Clear();

        KillParticle(ref _fieldParticle);
    }

    private static void KillParticle(ref Particle particle)
    {
        if (particle == null)
        {
            return;
        }

        if (!particle.IsToRemove())
        {
            particle.SetToRemove();
        }

        particle = null;
    }
}
