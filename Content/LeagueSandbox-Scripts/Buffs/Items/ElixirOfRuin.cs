using System;
using System.Collections.Generic;
using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ElixirOfRuin : IBuffGameScript {
    private const float SelfHealthBonus        = 250f;
    private const float TowerDamageBonusPct    = 0.15f;
    private const float SiegeAuraRange         = 1200f;
    private const float SiegeRefreshIntervalMs = 250f;
    private const float SiegeBuffDuration      = 0.75f;

    private readonly HashSet<LaneMinion> _siegeTargets = new();
    private ObjAIBase _owner;
    private Buff      _buff;
    private float     _refreshTimer = SiegeRefreshIntervalMs;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner                               = buff.SourceUnit;
        _buff                                = buff;
        StatsModifier.HealthPoints.FlatBonus = SelfHealthBonus;
        unit.AddStatModifier(StatsModifier);
        unit.Stats.CurrentHealth += SelfHealthBonus;
        ApiEventManager.OnDealDamage.AddListener(this, unit, OnDealDamage);
        RefreshSiegeCommanderAura();
    }

    public void OnUpdate(float diff) {
        if (_owner == null || _owner.IsDead || _buff == null) {
            ClearSiegeCommanderAura();
            return;
        }

        _refreshTimer += diff;
        if (_refreshTimer < SiegeRefreshIntervalMs) return;

        _refreshTimer = 0f;
        RefreshSiegeCommanderAura();
    }

    private void OnDealDamage(DamageData data) {
        if (!IsValidTarget(_owner, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectTurrets)) return;
        data.PostMitigationDamage +=
            data.Target.Stats.GetPostMitigationDamage(data.Damage * TowerDamageBonusPct, data.DamageType, data.Attacker);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        ClearSiegeCommanderAura();
    }

    private void RefreshSiegeCommanderAura() {
        var minionsInRange = GetUnitsInRange(
                _owner,
                _owner.Position,
                SiegeAuraRange,
                true,
                SpellDataFlags.AffectFriends | SpellDataFlags.AffectMinions | SpellDataFlags.NotAffectSelf
            )
            .OfType<LaneMinion>()
            .Where(minion => minion.Team == _owner.Team && !minion.IsDead)
            .ToHashSet();

        foreach (var minion in _siegeTargets.Where(target => !minionsInRange.Contains(target) || target.IsDead).ToList()) {
            RemoveBuff(minion, "SiegeCommander");
            _siegeTargets.Remove(minion);
        }

        var remainingDuration = Math.Max(0.1f, _buff.Duration - _buff.TimeElapsed);
        var auraDuration      = Math.Min(SiegeBuffDuration, remainingDuration);

        foreach (var minion in minionsInRange) {
            AddBuff("SiegeCommander", auraDuration, 1, _buff.OriginSpell, minion, _owner);
            _siegeTargets.Add(minion);
        }
    }

    private void ClearSiegeCommanderAura() {
        foreach (var minion in _siegeTargets.ToList()) {
            RemoveBuff(minion, "SiegeCommander");
        }

        _siegeTargets.Clear();
    }
}
