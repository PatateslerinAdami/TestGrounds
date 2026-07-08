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

namespace Buffs;

// Ashe's Focus passive (patch 4.20). Out of combat (no auto attack for 3s), Ashe gains
// 4/5/6/7/8 Focus per second (levels 1/5/9/13/17 — replay-verified against her level-up
// timeline). At 100 Focus her next basic attack is a guaranteed crit (AsheCritChanceReady);
// after that crit, Focus resets to a value equal to her crit chance %.
//
// This buff is the hidden driver (replay: NPC_BuffAdd2 BuffType=1 AURA, IsHidden=1). The
// visible counter is the separate AsheCritChance buff; the ready state is AsheCritChanceReady.
// All three MUST be AURA on the wire — a COUNTER-type crashes the 4.20 client (see AsheCritChance.cs).
public class Focus : IBuffGameScript {
    private const int   MaxFocus        = 100;
    private const float CombatWindowMs  = 3000f;   // "not attacked in the last 3 seconds"
    private const float TickMs          = 1000f;   // Focus gain is per-second

    private ObjAIBase _ashe;
    private Spell     _spell;
    private Buff      _buff;
    private Buff      _display;                     // AsheCritChance (visible counter)
    private int       _lastDisplayStacks = -1;
    private float     _combatTimer;
    private float     _tickTimer;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType             = BuffType.AURA,
        BuffAddType          = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true,
        IsHidden             = true,
        MaxStacks            = MaxFocus,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ashe  = buff.SourceUnit;
        _spell = ownerSpell;
        _buff  = buff;
        _combatTimer = 0f;
        _tickTimer   = TickMs;
        ApiEventManager.OnLaunchAttack.AddListener(this, _ashe, OnLaunchAttack);
        // Ashe spawns with Focus fully charged, so her first basic attack is a guaranteed crit.
        _buff.SetStacks(MaxFocus, false);
        if (!_ashe.HasBuff("AsheCritChanceReady")) {
            AddBuff("AsheCritChanceReady", 25000f, 1, _spell, _ashe, _ashe, true);
        }
        UpdateDisplay();
    }

    // Any auto attack puts Ashe "in combat": the ramp pauses for CombatWindowMs. Focus is NOT
    // reset by ordinary attacks — only the guaranteed crit resets it (AsheCritChanceReady).
    private void OnLaunchAttack(Spell spell) {
        if (!spell.CastInfo.IsAutoAttack) return;
        _combatTimer = CombatWindowMs;
        _tickTimer   = TickMs;
    }

    public void OnUpdate(Buff buff, float diff) {
        UpdateDisplay();

        if (_combatTimer > 0f) {
            _combatTimer -= diff;
            return;
        }
        if (_buff.StackCount >= MaxFocus) {
            return;
        }

        _tickTimer -= diff;
        if (_tickTimer > 0f) {
            return;
        }
        _tickTimer += TickMs;

        var newStacks = Math.Min(MaxFocus, _buff.StackCount + FocusPerSecond());
        _buff.SetStacks(newStacks, false);

        if (newStacks >= MaxFocus && !_ashe.HasBuff("AsheCritChanceReady")) {
            AddBuff("AsheCritChanceReady", 25000f, 1, _spell, _ashe, _ashe, true);
        }
        UpdateDisplay();
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
        if (_combatTimer <= 0f && _buff.StackCount > 0) {
            _display = AddBuff("AsheCritChance", 25000f, (byte)stacks, _spell, _ashe, _ashe, true);
            _lastDisplayStacks = stacks;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
