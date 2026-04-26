using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace ItemPassives;

internal sealed class PendingSpoilsExecute {
    public Champion AllyChampion { get; init; }
    public float    Gold         { get; init; }
}

public class ItemId3302 : IItemScript {
    private const int   ItemId                       = 3302;
    private const float ExecuteHealthThresholdFactor = 0.5f;
    private const float ExecuteDamagePadding         = 1.0f;
    private const float SpoilsRange                  = 1100f;
    private const float SpoilsHealAmountFlat         = 40f;

    private static readonly Dictionary<uint, ItemId3302> _activeScriptsByOwnerNetId = new();
    private readonly Dictionary<uint, PendingSpoilsExecute> _pendingExecutes = new();

    private ObjAIBase _owner;
    private Champion  _ownerChampion;
    private int       _favorGoldLooted;

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner         = owner;
        _ownerChampion = owner as Champion;
        _pendingExecutes.Clear();

        if (_owner == null || _ownerChampion == null) return;

        _activeScriptsByOwnerNetId[_owner.NetId] = this;
        AddBuff("TalentReaper", 60f, 1, _owner.AutoAttackSpell, _owner, _owner, true);
        ApiEventManager.OnPreDealDamage.AddListener(this, _owner, OnPreDealDamage);
        ApiEventManager.OnDealDamage.AddListener(this, _owner, OnDealDamage);
        ApiEventManager.OnMinionKill.AddListener(this, _owner, OnMinionKill);
    }

    public void OnDeactivate(ObjAIBase owner) {
        if (_owner != null) _activeScriptsByOwnerNetId.Remove(_owner.NetId);
        _pendingExecutes.Clear();
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void OnPreDealDamage(DamageData data) {
        if (!CanExecuteMinion(data, out var minion, out var allyChampion, out var goldToShare)) return;

        _pendingExecutes[minion.NetId] = new PendingSpoilsExecute {
            AllyChampion = allyChampion,
            Gold         = goldToShare
        };

        // Force this basic attack to execute the minion now that all passive requirements passed.
        data.PostMitigationDamage = Math.Max(data.PostMitigationDamage, minion.Stats.CurrentHealth + ExecuteDamagePadding);
    }

    private void OnDealDamage(DamageData data) {
        if (data?.Target is not LaneMinion minion) return;
        if (!_pendingExecutes.ContainsKey(minion.NetId)) return;

        // If the minion survived the hit, this execute attempt is invalid.
        if (minion.Stats.CurrentHealth > 0f) {
            _pendingExecutes.Remove(minion.NetId);
        }
    }

    private void OnMinionKill(DeathData data) {
        if (_owner == null || _ownerChampion == null || data?.Unit is not LaneMinion minion) return;
        if (!_pendingExecutes.Remove(minion.NetId, out var pendingExecute)) return;

        var counterBuff = _owner.GetBuffWithName("TalentReaper");
        if (counterBuff == null || counterBuff.StackCount <= 0) return;

        var allyChampion = pendingExecute.AllyChampion;
        if (allyChampion == null || allyChampion.IsDead) return;

        var goldToShare = pendingExecute.Gold;
        Spells.TalentReaperVFX.QueueSpoilsTransfer(_ownerChampion, allyChampion, goldToShare, SpoilsHealAmountFlat);
        SpellCastItem(_ownerChampion, "TalentReaperVFX", true, allyChampion, Vector2.Zero);
        var newStacks = Math.Max(0, counterBuff.StackCount - 1);
        EditBuff(counterBuff, (byte) newStacks);
    }

    public static void NotifySpoilsGoldLooted(Champion owner, int goldAmount) {
        if (owner == null || goldAmount <= 0) return;
        if (!_activeScriptsByOwnerNetId.TryGetValue(owner.NetId, out var script)) return;

        script._favorGoldLooted += goldAmount;
        script.UpdateFavorTooltip();
    }

    private bool CanExecuteMinion(
        DamageData data,
        out LaneMinion minion,
        out Champion allyChampion,
        out float goldToShare
    ) {
        minion       = null;
        allyChampion = null;
        goldToShare  = 0f;

        if (_owner == null || _ownerChampion == null || data == null) return false;
        if (!_owner.IsMelee || data.Attacker != _ownerChampion) return false;
        if (!data.IsAutoAttack || data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return false;
        if (data.Target is not LaneMinion laneMinion) return false;

        if (!IsValidTarget(
                _owner,
                laneMinion,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectMinions
            )) return false;

        var counterBuff = _owner.GetBuffWithName("TalentReaper");
        if (counterBuff == null || counterBuff.StackCount <= 0) return false;

        var maxHealth = laneMinion.Stats.HealthPoints.Total;
        if (maxHealth <= 0f) return false;
        if (laneMinion.Stats.CurrentHealth > maxHealth * ExecuteHealthThresholdFactor) return false;

        allyChampion = FindNearestAllyChampion();
        if (allyChampion == null) return false;

        goldToShare = laneMinion.Stats.GoldGivenOnDeath.Total;
        if (goldToShare <= 0f) return false;

        minion = laneMinion;
        return true;
    }

    private Champion? FindNearestAllyChampion() {
        if (_owner == null) return null;

        return GetUnitsInRange(
                _owner,
                _owner.Position,
                SpoilsRange,
                true,
                SpellDataFlags.AffectFriends | SpellDataFlags.AffectHeroes | SpellDataFlags.NotAffectSelf
            )
            .OfType<Champion>()
            .Where(champion => !champion.IsDead)
            .OrderBy(champion => Vector2.DistanceSquared(champion.Position, _owner.Position))
            .FirstOrDefault();
    }

    private void UpdateFavorTooltip() {
        if (_ownerChampion == null || _owner == null) return;

        foreach (var item in _owner.Inventory.GetAllItems()) {
            if (item?.ItemData.ItemId != ItemId) continue;
            var slot = _owner.Inventory.GetItemSlot(item);

            SetSpellToolTipVar(
                _owner,
                1,
                _favorGoldLooted,
                SpellbookType.SPELLBOOK_CHAMPION,
                slot,
                SpellSlotType.InventorySlots
            );
            return;
        }
    }
}
