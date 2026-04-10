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
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class AatroxPassive : IBuffGameScript {
    private ObjAIBase _aatrox;
    private float     _delayTimer = 0f;
    private float     _decayTimer = 0f;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();
    public StatsModifier StatsModifier2 { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _aatrox = ownerspell.CastInfo.Owner;
        SpendPAR(_aatrox, GetPAR(_aatrox));
        UpdateAttackSpeedStat();
        ApiEventManager.OnTakeDamage.AddListener(this, _aatrox, EngagingInCombat);
        ApiEventManager.OnDealDamage.AddListener(this, _aatrox, EngagingInCombat);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) { }

    public void OnUpdate(float diff) {
        if (_aatrox.HasBuff("AatroxPassiveDeath")) return;
        _delayTimer -= diff;
        if (!(_delayTimer <= 0f)) return;
        _decayTimer -= diff;
        if (!(_decayTimer <= 0f)) return;
        if (_aatrox.GetPAR() <= 0) return;
        _aatrox.SpendPAR(_aatrox.GetMaxPAR() * 0.02f);
        UpdateAttackSpeedStat();
        _decayTimer = 1000f;
    }

    private void UpdateAttackSpeedStat() {
        _aatrox.RemoveStatModifier(StatsModifier2);
        var maxPar     = Math.Max(1f, _aatrox.GetMaxPAR());
        var bloodRatio = _aatrox.GetPAR() / maxPar;
        StatsModifier2.AttackSpeed.PercentBonus = bloodRatio              * 0.5f;
        _aatrox.AddStatModifier(StatsModifier2);
        
    }

    private void EngagingInCombat(DamageData data) { _delayTimer = 6000f; }

    public void AddBlood(float amount) {
        IncreasePAR(_aatrox, amount, PrimaryAbilityResourceType.BloodWell);
        UpdateAttackSpeedStat();
        if (_delayTimer <= 1500f) { _delayTimer = 1500f; }
    }
}