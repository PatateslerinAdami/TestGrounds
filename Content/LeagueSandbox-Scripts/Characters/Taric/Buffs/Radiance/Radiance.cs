using System;
using System.Collections.Generic;
using System.Linq;
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

internal class Radiance : IBuffGameScript {
    private const float AuraRange = 1100f;
    private const float AuraRefreshIntervalMs = 250f;

    private readonly HashSet<AttackableUnit> _auraTargets = new();
    private ObjAIBase _taric;
    private Buff _buff;
    private Particle _auraParticle;
    private Particle _auraParticle1;
    private Particle _shoulderParticle;
    private float _auraRefreshTimer = AuraRefreshIntervalMs;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _taric = ownerSpell.CastInfo.Owner;
        _buff = buff;

        var selfBonus = 30f + 20f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.AttackDamage.FlatBonus = selfBonus;
        StatsModifier.AbilityPower.FlatBonus = selfBonus;
        unit.AddStatModifier(StatsModifier);

        _auraParticle = AddParticleTarget(_taric, unit, "Taric_GemStorm_Aura", unit, buff.Duration, size: 1.25f);
        _auraParticle1 = AddParticleTarget(_taric, unit, "taricgemstorm", unit, buff.Duration, size: 1.25f);
        RefreshAura();
    }

    public void OnUpdate(float diff) {
        if (_taric.IsDead) {
            _buff.DeactivateBuff();
            return;
        }

        _auraRefreshTimer += diff;
        if (_auraRefreshTimer < AuraRefreshIntervalMs) return;

        _auraRefreshTimer = 0f;
        RefreshAura();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        foreach (var target in _auraTargets.ToList()) {
            RemoveBuff(target, "RadianceAura");
        }

        _auraTargets.Clear();
        RemoveParticle(_auraParticle);
        RemoveParticle(_auraParticle1);
        RemoveParticle(_shoulderParticle);
    }

    private void RefreshAura() {
        var currentTargets = GetChampionsInRange(_taric.Position, AuraRange, true)
                             .Where(ally => ally.Team == _taric.Team && ally != _taric && !ally.IsDead)
                             .Cast<AttackableUnit>()
                             .ToHashSet();

        foreach (var target in _auraTargets.Where(target => !currentTargets.Contains(target)).ToList()) {
            RemoveBuff(target, "RadianceAura");
            _auraTargets.Remove(target);
        }

        var remainingDuration = Math.Max(0.1f, _buff.Duration - _buff.TimeElapsed);
        foreach (var target in currentTargets.Where(target => !_auraTargets.Contains(target) || !HasBuff(target, "RadianceAura"))) {
            AddBuff("RadianceAura", remainingDuration, 1, _buff.OriginSpell, target, _taric);
            _auraTargets.Add(target);
        }
    }
}
