using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
namespace Buffs;

public class TrundleDiseaseOverseer : IBuffGameScript {
    private const float DeathSearchRadius = 1400.0f;
    private const float MinHealPercent    = 0.018f;
    private const float MaxHealPercent    = 0.0594f;

    private readonly HashSet<uint> _subscribedUnits = [];

    private ObjAIBase _trundle;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HEAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _trundle = buff.SourceUnit ?? unit as ObjAIBase;
        if (_trundle == null) return;

        RegisterNearbyDeathListeners();
    }

    public void OnUpdate(float diff) {
        if (_trundle == null || _trundle.IsDead) return;
        RegisterNearbyDeathListeners();
    }

    private void RegisterNearbyDeathListeners() {
        var units = GetUnitsInRange(_trundle, _trundle.Position, DeathSearchRadius, true,
                                    SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                                    SpellDataFlags.AffectNeutral | SpellDataFlags.AffectEnemies);
        foreach (var candidate in units) {
            if (candidate == _trundle || _subscribedUnits.Contains(candidate.NetId)) continue;
            ApiEventManager.OnDeath.AddListener(this, candidate, OnNearbyUnitDeath);
            _subscribedUnits.Add(candidate.NetId);
        }
    }

    private void OnNearbyUnitDeath(DeathData deathData) {
        if (_trundle == null || _trundle.IsDead || deathData?.Unit == null) return;

        var deadUnit = deathData.Unit;
        if (deadUnit == _trundle) return;

        // Trundle passive should trigger on enemy/neutral deaths, not allied deaths.
        if (deadUnit.Team == _trundle.Team) return;

        // Ignore structures.
        if (deadUnit is BaseTurret or ObjBuilding or Inhibitor or Nexus) return;

        if (Vector2.DistanceSquared(deadUnit.Position, _trundle.Position) > DeathSearchRadius * DeathSearchRadius) return;

        var healPercent = GetHealPercentForLevel(_trundle.Stats.Level);
        var healAmount  = deadUnit.Stats.HealthPoints.Total * healPercent;
        if (healAmount > 0.0f)
            _trundle.TakeHeal(_trundle, healAmount, HealType.SelfHeal);
    }

    private static float GetHealPercentForLevel(byte level) {
        var clampedLevel = Math.Clamp(level, (byte) 1, (byte) 18);
        var t            = (clampedLevel - 1) / 17.0f;
        return MinHealPercent + (MaxHealPercent - MinHealPercent) * t;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}
