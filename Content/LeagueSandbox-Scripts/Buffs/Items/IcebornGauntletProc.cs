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

    private ObjAIBase _owner;
    private Buff _buff;

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

    public void OnUpdate(float diff)
    {
    }

    private void OnHitUnit(DamageData damageData)
    {
        if (_owner == null || _buff == null)
        {
            return;
        }

        if (!damageData.IsAutoAttack)
        {
            return;
        }

        if (damageData.Attacker != _owner)
        {
            return;
        }

        if (damageData.Target == null || damageData.Target.IsDead)
        {
            return;
        }

        var target = damageData.Target;
        var damageMultiplier = _buff.Variables.GetFloat("damageMultiplier", 1.25f);
        var zoneDuration = _buff.Variables.GetFloat("zoneDuration", 2.0f);
        var slowPercent = Math.Abs(_buff.Variables.GetFloat("slowPercent", 0.30f));
        var itemCooldown = _buff.Variables.GetFloat("itemCooldown", 1.5f);
        var zoneRadius = IsMeleeOwner() ? MeleeZoneRadius : RangedZoneRadius;

        var visibleBuff = _owner.GetBuffWithName("ItemFrozenFist");
        if (visibleBuff != null)
        {
            RemoveBuff(visibleBuff);
        }

        RemoveBuff(_buff);

        var bonusDamage = _owner.Stats.AttackDamage.BaseValue * damageMultiplier;

        target.TakeDamage(
            _owner,
            bonusDamage,
            DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_PROC,
            false
        );

        var itemSpell = GetIcebornSpell();
        itemSpell?.SetCooldown(itemCooldown, true);

        var variables = new BuffVariables();
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
            buffVariables: variables
        );
    }

    private bool IsMeleeOwner()
    {
        return _owner.IsMelee;
    }

    private Spell GetIcebornSpell()
    {
        for (byte i = 0; i < 7; i++)
        {
            var item = _owner.Inventory.GetItem(i);
            if (item != null && item.ItemData.ItemId == 3025)
            {
                short spellSlot = (short)(i + (byte)SpellSlotType.InventorySlots);
                if (_owner.Spells.TryGetValue(spellSlot, out Spell s))
                {
                    return s;
                }
            }
        }
        return null;
    }
}
