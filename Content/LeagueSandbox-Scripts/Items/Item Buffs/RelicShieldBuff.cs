using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class TalentReaper : IBuffGameScript {
    private const float RechargePeriodMs      = 60000f;
    private const float RechargePeriodSeconds = RechargePeriodMs / 1000f;

    private Buff           _buff;
    private PeriodicTicker _rechargeTicker;
    private int            _lastStacks;
    private bool           _timerPausedVisual;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COUNTER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 2
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff = buff;
        _rechargeTicker.Reset();
        _lastStacks       = -1;
        _timerPausedVisual = false;

        EditBuff(_buff, (byte) _buff.MaxStacks);
        _lastStacks = _buff.StackCount;
        SetTimerVisualPaused(force: true);
    }

    public void OnUpdate(float diff) {
        if (_buff == null) return;

        var currentStacks = Math.Clamp(_buff.StackCount, 0, _buff.MaxStacks);

        if (currentStacks >= _buff.MaxStacks) {
            _rechargeTicker.Reset();
            SetTimerVisualPaused();
            _lastStacks = currentStacks;
            return;
        }

        if (_lastStacks >= _buff.MaxStacks) {
            // Started recharging after dropping from full charges.
            _rechargeTicker.Reset();
            SetTimerVisualRunning(reset: true);
        } else {
            SetTimerVisualRunning();
        }

        var ticks = _rechargeTicker.ConsumeTicks(diff, RechargePeriodMs, false, 1);
        if (ticks > 0) {
            var newStacks = Math.Min(_buff.MaxStacks, currentStacks + ticks);
            if (newStacks != currentStacks) {
                EditBuff(_buff, (byte) newStacks);
                currentStacks = newStacks;
            }

            if (currentStacks >= _buff.MaxStacks) {
                _rechargeTicker.Reset();
                SetTimerVisualPaused();
                _lastStacks = currentStacks;
                return;
            }

            // 0->1 stack: keep displaying recharge for the next stack and restart UI timer.
            SetTimerVisualRunning(reset: true);
        }

        _lastStacks = currentStacks;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }

    private void SetTimerVisualPaused(bool force = false) {
        if (_buff == null) return;
        if (!force && _timerPausedVisual) return;

        _timerPausedVisual = true;
        SetBuffClientTimer(_buff, 0f, 0f);
    }

    private void SetTimerVisualRunning(bool reset = false) {
        if (_buff == null) return;
        if (!reset && !_timerPausedVisual) return;

        _timerPausedVisual = false;
        SetBuffClientTimer(_buff, RechargePeriodSeconds, 0f);
    }
}
