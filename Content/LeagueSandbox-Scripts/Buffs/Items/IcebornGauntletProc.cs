using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using ItemPassives;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class IcebornGauntletProc : IBuffGameScript
{
    private const float RangedZoneRadius = 210.0f;
    private const float MeleeZoneRadius = 285.0f;

    public BuffScriptMetaData BuffMetaData { get; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    private ObjAIBase _owner = null!;
    private Buff _buff = null!;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        if (unit is ObjAIBase ai)
        {
            _owner = ai;
            _buff = buff;
            ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHitUnit);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void OnHitUnit(DamageData damageData)
    {
        if (_owner == null || _buff == null)
        {
            return;
        }

        if (damageData.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK)
        {
            return;
        }

        if (damageData.Attacker != _owner)
        {
            return;
        }

        if (damageData.Target == null)
        {
            return;
        }

        var target = damageData.Target;
        var dealDamage = _buff.BuffVars.GetBool("dealDamage", true);
        var damageAmount = _buff.BuffVars.GetFloat("damageAmount", 0.0f);
        var damageMultiplier = _buff.BuffVars.GetFloat("damageMultiplier", 1.25f);
        var zoneDuration = _buff.BuffVars.GetFloat("zoneDuration", 2.0f);
        var slowPercent = Math.Abs(_buff.BuffVars.GetFloat("slowPercent", 0.30f));
        var itemCooldown = _buff.BuffVars.GetFloat("itemCooldown", 1.5f);
        var zoneRadius = IsMeleeOwner() ? MeleeZoneRadius : RangedZoneRadius;

        var visibleBuff = _owner.GetBuffWithName("ItemFrozenFist");
        if (visibleBuff != null)
        {
            RemoveBuff(visibleBuff);
        }

        RemoveBuff(_buff);

        if (dealDamage && !target.IsDead)
        {
            if (damageAmount <= 0.0f)
            {
                damageAmount = _owner.Stats.AttackDamage.BaseValue * damageMultiplier;
            }

            target.TakeDamage(
                _owner,
                damageAmount,
                DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource.DAMAGE_SOURCE_PROC,
                false
            );
        }

        var itemSpell = SpellbladeManager.GetItemSpell(_owner, 3025);
        itemSpell?.SetCooldown(itemCooldown, true);
        AddBuff("SheenDelay", itemCooldown, 1, _buff.OriginSpell, _owner, _owner);

        var variables = new VariableTable();
        variables.Set("centerX", target.Position.X);
        variables.Set("centerY", target.Position.Y);
        variables.Set("zoneDuration", zoneDuration);
        variables.Set("slowPercent", slowPercent);
        variables.Set("radius", zoneRadius);
        variables.Set("sourceItemId", 3025);

        AddBuff(
            "IcebornGauntletZone",
            zoneDuration,
            1,
            _buff.OriginSpell,
            _owner,
            _owner,
            variableTable: variables
        );
    }

    private bool IsMeleeOwner()
    {
        return _owner.IsMelee;
    }
}
