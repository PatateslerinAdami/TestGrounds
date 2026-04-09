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
    private ObjAIBase     _owner;
    
    public         StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        
        for (byte i = 0; i < 4; i++) {
            if (_owner.Spells.TryGetValue(i, out Spell spell)) {
                ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellsCast);
            }
        }
        
        ApiEventManager.OnHitUnit.AddListener(this, _owner, OnHit);
        ApiEventManager.OnKill.AddListener(this, _owner, OnKill);
    }

    private void OnSpellsCast(Spell spell) {
        if (!spell.Script.ScriptMetadata.TriggersSpellCasts || _owner.HasBuff("SheenDelay")) return;
        var variables = new BuffVariables();
        variables.Set("damageAmount", _owner.Stats.AttackDamage.BaseValue * 2f);
        AddBuff("Sheen", 10f, 1, spell, _owner, _owner, buffVariables: variables);
    }

    private void OnHit(DamageData data) {
        if (_owner.HasBuff("ItemPhageSpeed")) return;
        AddBuff("ItemPhageMiniSpeed", 2f, 1, _owner.AutoAttackSpell, _owner, _owner);
    }

    private void OnKill(DeathData data) {
        AddBuff("ItemPhageSpeed", 2f, 1, _owner.AutoAttackSpell, _owner, _owner);
    }

    public void OnDeactivate(ObjAIBase owner) { 
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}