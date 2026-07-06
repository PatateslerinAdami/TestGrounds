using System;
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

namespace Buffs;

// "Ready" state of Ashe's Focus passive: applied at 100 Focus, guarantees her next basic
// attack crits, then resets Focus to a value equal to her crit chance %.
//
// Guarantees the crit by topping crit chance up to 100% (captured base crit is restored via
// the reset value). Wire type MUST be AURA (BuffType=1, replay-verified) — a COUNTER-type
// crashes the 4.20 client on the consuming attack (see AsheCritChance.cs).
public class AsheCritChanceReady : IBuffGameScript {
    private ObjAIBase _ashe;
    private Buff      _buff;
    private float     _baseCrit;
    private bool      _consumed;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.RENEW_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ashe = ownerSpell.CastInfo.Owner;
        _buff = buff;
        // Capture crit chance BEFORE topping it up — this is the value Focus resets to.
        _baseCrit = _ashe.Stats.CriticalChance.Total;
        StatsModifier.CriticalChance.FlatBonus = Math.Max(0f, 1f - _baseCrit);
        unit.AddStatModifier(StatsModifier);
        ApiEventManager.OnHitUnit.AddListener(this, _ashe, OnHitUnit);
    }

    private void OnHitUnit(DamageData data) {
        if (_consumed) return;
        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
        _consumed = true;

        var focus = _ashe.GetBuffWithName("Focus");
        if (focus != null) {
            // Reset to her crit chance % (Riot MO_ROUND = floor(x + 0.5), NOT banker's rounding).
            // Focus.cs keeps the counter hidden until it ramps back out of combat, so a 0-crit Ashe
            // shows it again at 4 (first tick), never 0/1/2/3.
            var resetStacks = (int) GameMaths.MathOps.RoundHalfUp(_baseCrit * 100f);
            resetStacks = Math.Clamp(resetStacks, 0, focus.MaxStacks);
            // Silent: the visible counter (AsheCritChance) is re-synced by Focus.OnUpdate.
            focus.SetStacks(resetStacks, false);
        }
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}
