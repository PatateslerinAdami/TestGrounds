using System;
using System.Collections;
using System.Linq;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace ItemPassives;

public class ItemID_3078 : IItemScript {
    private ObjAIBase     _owner = null!;
    private const int ItemId = 3078;
    
    public         StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        SpellbladeManager.Register(owner, ItemId);
        
        Enumerable.Range(0, 4)
            .Where(slot => _owner.Spells.ContainsKey((short)slot))
            .Select(slot => _owner.Spells[(short)slot])
            .ToList()
            .ForEach(spell => ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellsCast));
        
        ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHit);
        ApiEventManager.OnKill.AddListener(this, _owner, OnKill);
    }

    private void OnSpellsCast(Spell spell) {
        SpellbladeManager.TryArmSpellblade(_owner, spell);
    }

    private void OnHit(DamageData data) {
        if (_owner.HasBuff("ItemPhageSpeed")) return;
        AddBuff("ItemPhageMiniSpeed", 2f, 1, _owner.AutoAttackSpell, _owner, _owner);
    }

    private void OnKill(DeathData data) {
        AddBuff("ItemPhageSpeed", 2f, 1, _owner.AutoAttackSpell, _owner, _owner);
    }

    public void OnDeactivate(ObjAIBase owner) { 
        SpellbladeManager.Unregister(owner, ItemId);
        ApiEventManager.RemoveAllListenersForOwner(this);
        _owner = null!;
    }
}
