using System.Collections.Generic;
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

internal class SionPassiveZombie : IBuffGameScript
{
    private ObjAIBase _sion;
    private DamageData _defferedDamageData;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _sion = buff.SourceUnit;
        _defferedDamageData = _sion.CharVars.Get("deferredDamageData", _defferedDamageData);
        ApiEventManager.OnDeath.AddListener(this, _sion, OnDeath);
        unit.TakeDamage(_defferedDamageData.Attacker, _defferedDamageData.Damage, _defferedDamageData.DamageType,
            _defferedDamageData.DamageSource,
            _defferedDamageData.DamageResultType);
        unit.TakeHeal(_sion, _sion.Stats.HealthPoints.Total, HealType.SelfHeal);

        var bonusAs = 1.75f;
        OverrideUnitAttackSpeedCap(_sion, true,1.75f, true, 1.75f);

        StatsModifier.AttackSpeed.BaseBonus = bonusAs;
        StatsModifier.LifeSteal.FlatBonus = 1f;
        unit.AddStatModifier(StatsModifier);

        _sion.SetStatus(StatusFlags.Ghosted, true);

        ApiEventManager.OnHeal.AddListener(this, _sion, OnHeal);
        ApiEventManager.OnHitUnit.AddListener(this, _sion, OnHit);
        OverrideAutoAttacks(_sion, false, "SionBasicAttackPassive2", "SionBasicAttackPassive");
        unit.SetAnimStates(new Dictionary<string, string>
        {
            { "IDLE1", "PASSIVE_IDLE1" },
            { "IDLE1_BASE", "PASSIVE_IDLE1" },
            { "IDLE2_BASE", "PASSIVE_IDLE1" },
            { "IDLE_IN", "PASSIVE_IDLE1" },
            { "RUN", "Passive_Run_Raw" },
            { "RUN_HASTE", "PASSIVE_RUN" },
            { "LAUGH", "PASSIVE_DANCE" },
            { "DANCE", "PASSIVE_DANCE" },
            { "TAUNT", "PASSIVE_DANCE" },
            { "JOKE", "PASSIVE_DANCE" },
        });
        SpellEffectCreate("Sion_Base_Passive_Hand.troy", _sion, _sion, _sion, lifetime: buff.Duration, scale: 2.5f,
            boneName: "L_Buffbone_Glb_Hand_Loc",
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("Sion_Base_Passive_Hand.troy", _sion, _sion, _sion, lifetime: buff.Duration, scale: 2.5f,
            boneName: "R_Buffbone_Glb_Hand_Loc",
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("Sion_Base_Passive_Skin.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("Sion_Base_Passive_Cas.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("Sion_Base_Passive_Smoke.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        for (var i = 0; i < 4; i++)
        {
            SetSpell(_sion, "SionPassiveSpeed", SpellSlotType.SpellSlots, i);
        }
    }

    private void OnHeal(HealData data)
    {
        if (data.HealType == HealType.SelfHeal)
        {
            
        }
    }

    private void OnHit(DamageData data)
    {
        var dmg = data.Target.Stats.HealthPoints.Total * 0.1f;
        if (!IsValidTarget(_sion, data.Target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes))
        {
            dmg = System.Math.Min(dmg, 75f);
        }

        data.Target.TakeDamage(_sion, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_PROC,
            DamageResultType.RESULT_NORMAL);
    }

    private void OnDeath(DeathData data)
    {
        data.BecomeZombie = true;
        ApiEventManager.OnDeath.RemoveListener(this);
    }


    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.OnHitUnit.RemoveListener(this, _sion, OnHit);
        _sion.SetStatus(StatusFlags.Ghosted, false);
        ResetCharacterVoiceOverride(buff.SourceUnit);
        _sion.RemoveOverrideAutoAttack();
        unit.SetAnimStates(new Dictionary<string, string>
        {
            { "ATTACK1", "" },
            { "IDLE1", "" },
            { "IDLE1_BASE", "" },
            { "IDLE2_BASE", "" },
            { "IDLE_IN", "" },
            { "RUN", "" },
            { "RUN_HASTE", "" },
            { "CRIT", "" },
            { "LAUGH", "" },
            { "DANCE", "" },
            { "TAUNT", "" },
            { "JOKE", "" },
        });

        SetSpell(_sion, "SionQ", SpellSlotType.SpellSlots, 0);
        SetSpell(_sion, "SionW", SpellSlotType.SpellSlots, 1);
        SetSpell(_sion, "SionE", SpellSlotType.SpellSlots, 2);
        SetSpell(_sion, "SionR", SpellSlotType.SpellSlots, 3);
        _sion.EndZombie();
    }
}