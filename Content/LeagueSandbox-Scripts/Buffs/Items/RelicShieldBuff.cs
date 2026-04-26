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

public class TalentReaper : IBuffGameScript
{
    private const float SPOILS_RANGE                  = 1100f;
    private const float SPOILS_HEAL_AMOUNT_FLAT       = 40f;
    private const float EXECUTE_HEALTH_THRESHOLD      = 200f;
    private float _sharedGold;
    private float _rechargePeriodMs;
    private float _rechargePeriodSeconds;
    private ObjAIBase _owner;
    private Particle _particle;
    private Buff _buff;
    private PeriodicTicker _rechargeTicker;
    bool _wasAtMaxStacks;
    private bool _timerPausedVisual;
    private int _itemID;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COUNTER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 2,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell)
    {
        _itemID = buff.Variables.GetInt("itemid");

        _owner = ownerspell.CastInfo.Owner;
        _buff = buff;
        _rechargePeriodMs = _buff.Duration * 1000f;
        _rechargePeriodSeconds = _buff.Duration;

        EditBuff(_buff, (byte)_buff.MaxStacks);
        _wasAtMaxStacks = true;
        UpdateParticles();

        _rechargeTicker.Reset();
        _timerPausedVisual = false;
        SetTimerVisualPaused(force: true);

        ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHit);
        //SetSpellToolTipVar(_owner, 2, 200, SpellbookType.SPELLBOOK_CHAMPION, GetItemSlot(), SpellSlotType.InventorySlots);
    }


    public void OnUpdate(float diff)
    {
        if (_buff == null) return;

        var currentStacks = Math.Clamp(_buff.StackCount, 0, _buff.MaxStacks);

        if (currentStacks >= _buff.MaxStacks)
        {
            _rechargeTicker.Reset();
            SetTimerVisualPaused();
            _wasAtMaxStacks = true;
            return;
        }

        if (_wasAtMaxStacks)
        {
            // Started recharging after dropping from full charges.
            _rechargeTicker.Reset();
            SetTimerVisualRunning(reset: true);
        }
        else
        {
            SetTimerVisualRunning();
        }

        var ticks = _rechargeTicker.ConsumeTicks(diff, _rechargePeriodMs, false, 1);
        if (ticks > 0)
        {
            var newStacks = Math.Min(_buff.MaxStacks, currentStacks + ticks);
            if (newStacks != currentStacks)
            {
                EditBuff(_buff, (byte)newStacks);
                currentStacks = newStacks;
            }

            if (currentStacks >= _buff.MaxStacks)
            {
                _rechargeTicker.Reset();
                SetTimerVisualPaused();
                _wasAtMaxStacks = true;
                return;
            }

            UpdateParticles();
            // 0->1 stack: keep displaying recharge for the next stack and restart UI timer.
            SetTimerVisualRunning(reset: true);
        }

        _wasAtMaxStacks = false;
    }

    private void SetTimerVisualPaused(bool force = false)
    {
        if (_buff == null) return;
        if (!force && _timerPausedVisual) return;

        _timerPausedVisual = true;
        SetBuffClientTimer(_buff, 0f, 0f);
    }

    private void SetTimerVisualRunning(bool reset = false)
    {
        if (_buff == null) return;
        if (!reset && !_timerPausedVisual) return;

        _timerPausedVisual = false;
        SetBuffClientTimer(_buff, _rechargePeriodSeconds, 0f);
    }

    private void OnHit(DamageData data)
    {
        bool targetIsMinion = data.Target is LaneMinion;

        if ((_buff.StackCount > 0) && (_owner.IsMelee) && targetIsMinion)
        {
            float currentMinionHealth = data.Target.Stats.CurrentHealth;
            AttackableUnit closestAllyHero = GetClosestUnitInRange(_owner, _owner.Position, SPOILS_RANGE, true,
                SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes | SpellDataFlags.NotAffectSelf);


            if ((currentMinionHealth < EXECUTE_HEALTH_THRESHOLD) && (closestAllyHero != null))
            {
                EditBuff(_buff, (byte)(_buff.StackCount - 1));
                UpdateParticles();

                data.Target.TakeDamage(_owner, currentMinionHealth, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_PROC, false);
                UpdateGoldSharedTooltip(); // TODO: find out why this doesn't always update
                _owner.TakeHeal(_owner, SPOILS_HEAL_AMOUNT_FLAT, HealType.IncomingHeal);

                var minion = data.Target;
                var allyChamp = closestAllyHero as Champion;
                SpellCastItem(_owner, "TalentReaperVFX", true, closestAllyHero, minion.Position);
                if (allyChamp != null)
                {
                    allyChamp.AddGold(minion, minion.Stats.GoldGivenOnDeath.Total, true);
                    allyChamp.AddAmountToCreepScore(1, data.Target);
                    _sharedGold += minion.Stats.GoldGivenOnDeath.Total;
                }
                
            }
        }
    }


    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        EditBuff(_buff, 0);
        UpdateParticles();
    }

    private void UpdateParticles()
    {
        if (_particle != null)
        {
            RemoveParticle(_particle);
        }

        switch (_buff.StackCount)
        {
            case 0:
                break;
            case 1:
                _particle = AddParticleTarget(_owner, _owner, "GLOBAL_Item_FoM_Charge01", _owner, -1);
                break;
            case 2:
                _particle = AddParticleTarget(_owner, _owner, "GLOBAL_Item_FoM_Charge02", _owner, -1);
                break;
            case 3:
                _particle = AddParticleTarget(_owner, _owner, "GLOBAL_Item_FoM_Charge03", _owner, -1);
                break;
            case 4:
                _particle = AddParticleTarget(_owner, _owner, "GLOBAL_Item_FoM_Charge04", _owner, -1);
                break;
        }
    }

    private void UpdateGoldSharedTooltip()
    {
        // 0 here is F1 from fontconfig, it has to be F-1, so it's 0
        SetSpellToolTipVar(_owner, 0, _sharedGold, SpellbookType.SPELLBOOK_CHAMPION, GetItemSlot(), SpellSlotType.InventorySlots);
        SetSpellToolTipVar(_owner, 2, EXECUTE_HEALTH_THRESHOLD, SpellbookType.SPELLBOOK_CHAMPION, GetItemSlot(), SpellSlotType.InventorySlots);
    }

    private byte GetItemSlot()
    {
        foreach (var item in _owner.Inventory.GetAllItems())
        {
            if (item?.ItemData.ItemId != _itemID)
            {
                continue;
            }
            
            return _owner.Inventory.GetItemSlot(item);
            
        }

        return 255;
    }

}