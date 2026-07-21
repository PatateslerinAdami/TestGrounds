using System;
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
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace Buffs;

public class Focus : IBuffGameScript {
    private const int    MaxFocus       = 100;
    private const float  CombatWindowMs = 3000f;   // "not attacked in the last 3 seconds"
    private const float  TickMs         = 1000f;   // Focus gain is per-second
    private const string FocusTickKey   = "focusTick";

    private ObjAIBase _ashe;
    private Spell     _spell;
    private Buff      _buff;
    private Buff      _display;                     // AsheCritChance (visible counter)
    private int       _lastDisplayStacks = -1;
    private float     _lastAttackTime = -CombatWindowMs;   // spawn = out of combat

    // "In combat" = an auto attack launched within the window (GameTime()-timestamp pattern,
    // same shape Riot's TaskDefendStructure.lua uses with GetLastTookDamageTime).
    private bool InCombat => GameTime() - _lastAttackTime < CombatWindowMs;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType             = BuffType.AURA,
        BuffAddType          = BuffAddType.RENEW_EXISTING,
        IsHidden             = true,
        MaxStacks            = MaxFocus,
        PersistsThroughDeath = true,
        IsNonDispellable = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ashe  = buff.SourceUnit;
        _spell = ownerSpell;
        _buff  = buff;
        ApiEventManager.OnUpdateStats.AddListener(this, _ashe, OnUpdateStats);
        ApiEventManager.OnLaunchAttack.AddListener(this, _ashe, OnLaunchAttack);
        // Ashe spawns with Focus fully charged, so her first basic attack is a guaranteed crit.
        _buff.SetStacks(MaxFocus, false);
        if (!_ashe.HasBuff("AsheCritChanceReady")) {
            AddBuff("AsheCritChanceReady", 25000f, 1, _spell, _ashe, _ashe, true);
        }
        UpdateDisplay();
    }

    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        var level = _ashe.Stats.Level switch
        {
            >= 17 => 8,
            >= 13 => 7,
            >= 9 => 6,
            >= 5 => 5,
            _ => 4,
        };
        SetBuffToolTipVar(_buff, 0, level);
    }

    // Any auto attack puts Ashe "in combat": the ramp pauses for CombatWindowMs. Focus is NOT
    // reset by ordinary attacks — only the guaranteed crit resets it (AsheCritChanceReady).
    // Clearing the tick anchor here is load-bearing: ExecutePeriodically's anchor goes stale
    // while OnUpdate skips it (combat window / max stacks), and a stale anchor would fire
    // immediately plus once per frame until caught up. The reset makes the first gain land at
    // attack + 3s window + full 1s tick, matching the old countdown exactly. Max stacks only
    // ever drops via the guaranteed-crit auto, so every below-max transition passes through here.
    private void OnLaunchAttack(Spell spell) {
        if (!spell.CastInfo.IsAutoAttack) return;
        _lastAttackTime = GameTime();
        ExecutePeriodicallyReset(_buff.BuffVars, FocusTickKey);
    }

    public void OnUpdate(Buff buff, float diff) {
        UpdateDisplay();

        if (InCombat || _buff.StackCount >= MaxFocus) {
            return;
        }

        ExecutePeriodically(_buff.BuffVars, FocusTickKey, TickMs, false, 0, () => {
            var newStacks = Math.Min(MaxFocus, _buff.StackCount + FocusPerSecond());
            _buff.SetStacks(newStacks, false);

            if (newStacks >= MaxFocus && !_ashe.HasBuff("AsheCritChanceReady")) {
                AddBuff("AsheCritChanceReady", 25000f, 1, _spell, _ashe, _ashe, true);
            }
            UpdateDisplay();
        });
    }

    private int FocusPerSecond() => _ashe.Stats.Level switch {
        >= 17 => 8,
        >= 13 => 7,
        >= 9  => 6,
        >= 5  => 5,
        _     => 4,
    };

    // The visible counter. Replay-verified lifecycle:
    //  - At 100 Focus it is removed and AsheCritChanceReady replaces it.
    //  - After the guaranteed crit is consumed it stays GONE — it only reappears once Focus is
    //    actually building again: out of combat (3s after the last attack) with a showable value.
    //    A 0-crit Ashe therefore pops it back at 4 (the first per-second tick), never 0/1/2/3.
    //  - Once visible it survives brief combat (the ramp just pauses); only 100 removes it.
    private void UpdateDisplay() {
        if (_buff.StackCount >= MaxFocus) {
            if (_display != null || _ashe.HasBuff("AsheCritChance")) {
                (_display ?? _ashe.GetBuffWithName("AsheCritChance"))?.DeactivateBuff();
                _display = null;
                _lastDisplayStacks = -1;
            }
            return;
        }

        var stacks = Math.Clamp(_buff.StackCount, 1, MaxFocus);

        // Already visible: keep it in sync. It persists through combat (ramp merely pauses).
        if (_display != null && _ashe.HasBuff("AsheCritChance")) {
            if (stacks != _lastDisplayStacks) {
                _display.SetStacks(stacks);
                _lastDisplayStacks = stacks;
            }
            return;
        }

        // Hidden (fresh spawn or just consumed a crit). Reappear only when Focus is building again:
        // out of combat AND with something to show (StackCount > 0 — a 0 wire Count means "remove").
        _display = null;
        if (!InCombat && _buff.StackCount > 0) {
            _display = AddBuff("AsheCritChance", 25000f, stacks, _spell, _ashe, _ashe, true);
            _lastDisplayStacks = stacks;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
