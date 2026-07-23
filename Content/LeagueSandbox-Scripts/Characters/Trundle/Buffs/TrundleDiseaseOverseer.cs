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
    // King's Tribute nearby-death range. 1000 for our patch (S4/4.20); Riot raised it to 1400 only in
    // patch 5.5. The range is consumer-side, NOT an engine gate — different champions differ (e.g. Thresh
    // soul collection = 1900), which is why OnNearbyDeath itself does not range-filter.
    private const float DeathSearchRadius = 1000.0f;
    private const float MinHealPercent    = 0.018f;
    private const float MaxHealPercent    = 0.0594f;

    private ObjAIBase _trundle;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
            PersistsThroughDeath = true,
        BuffType    = BuffType.HEAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _trundle = buff.SourceUnit ?? unit as ObjAIBase;
        if (_trundle == null) return;

        // Riot CharOnNearbyDeath: register with Trundle's own King's Tribute range — the dispatcher
        // range-gates per-listener, so the handler only filters team/type. Auto-removed on buff deactivate.
        ApiEventManager.OnNearbyDeath.AddListener(this, _trundle, OnNearbyUnitDeath, DeathSearchRadius);
    }

    private void OnNearbyUnitDeath(DeathData deathData) {
        if (_trundle == null || _trundle.IsDead || deathData?.Unit == null) return;

        var deadUnit = deathData.Unit;
        if (deadUnit == _trundle) return;

        // Trundle passive should trigger on enemy/neutral deaths, not allied deaths.
        if (deadUnit.Team == _trundle.Team) return;

        // Ignore structures.
        if (deadUnit is BaseTurret or ObjBuilding or Inhibitor or Nexus) return;

        // Range is gated by the dispatcher (registered with DeathSearchRadius) — no distance check here.
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
