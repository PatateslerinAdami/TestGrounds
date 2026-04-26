using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

internal sealed class PendingSpoilsTransfer {
    public uint  AllyNetId  { get; init; }
    public float GoldAmount { get; init; }
    public float HealAmount { get; init; }
}

public class TalentReaperVFX : ISpellScript {
    private static readonly Dictionary<uint, List<PendingSpoilsTransfer>> _pendingByCasterNetId = new();
    private static readonly Dictionary<uint, PendingSpoilsTransfer> _pendingBySpellNetId = new();

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };

    public static void QueueSpoilsTransfer(Champion caster, Champion allyChampion, float goldAmount, float healAmount) {
        if (caster == null || allyChampion == null) return;
        if (goldAmount <= 0f && healAmount <= 0f) return;

        if (!_pendingByCasterNetId.TryGetValue(caster.NetId, out var pendingList)) {
            pendingList = new List<PendingSpoilsTransfer>();
            _pendingByCasterNetId[caster.NetId] = pendingList;
        }

        pendingList.Add(new PendingSpoilsTransfer {
            AllyNetId  = allyChampion.NetId,
            GoldAmount = goldAmount,
            HealAmount = healAmount
        });
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        if (target == null) return;

        if (owner is Champion caster && target is Champion allyChampion) {
            var pending = DequeuePendingTransfer(caster.NetId, allyChampion.NetId);
            if (pending != null) {
                _pendingBySpellNetId[spell.CastInfo.SpellNetID] = pending;
            }
        }

        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit, true);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (spell.CastInfo.Owner is not Champion caster || target is not Champion allyChampion) {
            missile?.SetToRemove();
            return;
        }
        if (!_pendingBySpellNetId.Remove(spell.CastInfo.SpellNetID, out var pendingTransfer)) {
            missile?.SetToRemove();
            return;
        }
        if (pendingTransfer.AllyNetId != allyChampion.NetId) {
            missile?.SetToRemove();
            return;
        }

        if (pendingTransfer.GoldAmount > 0f) {
            allyChampion.AddGold(null, pendingTransfer.GoldAmount, true);
            caster.AddGold(null, -pendingTransfer.GoldAmount, false);
            ItemPassives.ItemID_3302.NotifySpoilsGoldLooted(caster, (int) pendingTransfer.GoldAmount);
        }

        if (pendingTransfer.HealAmount > 0f) {
            caster.TakeHeal(caster, pendingTransfer.HealAmount, HealType.SelfHeal, spell);
            allyChampion.TakeHeal(caster, pendingTransfer.HealAmount, HealType.OutgoingHeal, spell);
        }

        missile?.SetToRemove();
    }

    private static PendingSpoilsTransfer DequeuePendingTransfer(uint casterNetId, uint allyNetId) {
        if (!_pendingByCasterNetId.TryGetValue(casterNetId, out var pendingList) || pendingList.Count == 0) {
            return null;
        }

        for (var i = 0; i < pendingList.Count; i++) {
            var pendingTransfer = pendingList[i];
            if (pendingTransfer.AllyNetId != allyNetId) continue;

            pendingList.RemoveAt(i);
            if (pendingList.Count == 0) _pendingByCasterNetId.Remove(casterNetId);
            return pendingTransfer;
        }

        return null;
    }
}
