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

internal class MorderkaiserIronMan : IBuffGameScript {
    private ObjAIBase _mordekaiser;
    private float     _delayTimer = 0f;
    private float      _decayTimer = 0f;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _mordekaiser = ownerSpell.CastInfo.Owner;
        _mordekaiser.SpendPAR(100.0f);
        ApiEventManager.OnPreTakeDamage.AddListener(this, _mordekaiser, OnPreTakeDamage);
        ApiEventManager.OnDealDamage.AddListener(this, _mordekaiser, OnDealDamage);
    }

    private void OnDealDamage(DamageData data) {
        var shieldMod = 0.175f;
        if (data.Target is Champion) {
            shieldMod = 0.35f;
        }
        _mordekaiser.IncreasePAR(_mordekaiser, data.PostMitigationDamage * shieldMod);
        _delayTimer = 1500f;
    }

    private void OnPreTakeDamage(DamageData data) {
        if (_mordekaiser.GetPAR() <= 0.0f) return;
        var absorbed = _mordekaiser.SpendPAR(data.PostMitigationDamage);
        data.PostMitigationDamage = Math.Max(0.0f, data.PostMitigationDamage - absorbed);
    }

    public void OnUpdate(float diff) {
        _delayTimer -= diff;
        if (!(_delayTimer <= 0f)) return;
        _decayTimer -= diff;
        if (!(_decayTimer <= 0f)) return;
        if (_mordekaiser.GetPAR() <= _mordekaiser.Stats.HealthPoints.Total * 0.0625f) return;
        _mordekaiser.SpendPAR(_mordekaiser.Stats.HealthPoints.BaseValue * 0.03f);
        _decayTimer                    =  1000f;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
