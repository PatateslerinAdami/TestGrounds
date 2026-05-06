using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class Focus : IBuffGameScript {
    private const int MaxFocusStacks = 100;
    private ObjAIBase _ashe;
    private Spell     _spell;
    private Buff      _buff;
    private Buff      _critDisplay;
    private int       _lastDisplayStacks = -1;
    private float     _combatTimer = 0f;
    private float     _stackTimer = 1000f;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks   = MaxFocusStacks
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ashe  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff  = buff;
        ApiEventManager.OnLaunchAttack.AddListener(this, _ashe, OnLaunchAttack);
        EnsureCritDisplay();
        SyncCritDisplay();
        TryApplyCritReady();
    }

    private void OnLaunchAttack(Spell spell) {
        if (!spell.CastInfo.IsAutoAttack) return;
        _combatTimer = 3000f;
        _stackTimer = 1000f;
    }

    public void OnUpdate(float diff) {
        EnsureCritDisplay();
        SyncCritDisplay();
        _combatTimer -= diff;
        if (_combatTimer > 0 || _buff.StackCount >= MaxFocusStacks) return;
        _stackTimer -= diff;
        if (_stackTimer > 0 || _buff.StackCount >= MaxFocusStacks) return;
        var stacksPerSecond = 4 + _ashe.Stats.Level switch {
            >= 17 => 4,
            >= 13 => 3,
            >= 9  => 2,
            >= 5  => 1,
            _     => 0
        };
        var newStacks = Math.Min(MaxFocusStacks, _buff.StackCount + stacksPerSecond);
        _buff.SetStacks(newStacks);
        _stackTimer = 1000f;
        SyncCritDisplay();
        TryApplyCritReady();
    }

    private void EnsureCritDisplay() {
        if (_critDisplay != null && _ashe.HasBuff("AsheCritChance")) return;
        _critDisplay = _ashe.GetBuffWithName("AsheCritChance");
        if (_critDisplay == null)
            _critDisplay = AddBuff("AsheCritChance", 25000f, 1, _spell, _ashe, _ashe, true);
    }

    private void SyncCritDisplay() {
        if (_critDisplay == null) return;
        var stacks = Math.Clamp(_buff.StackCount, 0, _critDisplay.MaxStacks);
        if (stacks == _lastDisplayStacks) return;
        _critDisplay.SetStacks(stacks);
        _critDisplay.SetToolTipVar(1, stacks);
        _lastDisplayStacks = stacks;
    }

    private void TryApplyCritReady() {
        if (_buff.StackCount < MaxFocusStacks) return;
        if (_ashe.HasBuff("AsheCritChanceReady")) return;
        AddBuff("AsheCritChanceReady", 25000f, 1, _spell, _ashe, _ashe, true);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}
