using GameServerCore.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using LeagueSandbox.GameServer.GameObjects;


namespace ItemPassives;

public class ItemID_3057 : IItemScript {
    private ObjAIBase     _owner;
    
    public         StatsModifier StatsModifier { get; } = new();

    public void OnActivate(ObjAIBase owner) {
        _owner = owner;
        for (byte i = 0; i < 4; i++) {
            if (_owner.Spells.TryGetValue(i, out Spell spell))
            {
                ApiEventManager.OnSpellCast.AddListener(this, spell, OnSpellCast);
            }
        }
        
    }

    public void OnSpellCast(Spell spell) {
        if (!spell.Script.ScriptMetadata.TriggersSpellCasts || _owner.HasBuff("SheenDelay")) return;
        var variables = new BuffVariables();
        variables.Set("damageAmount", _owner.Stats.AttackDamage.BaseValue * 2f);
        AddBuff("Sheen", 10f, 1, spell, _owner, _owner, buffVariables: variables);
    }

    public void OnDeactivate(ObjAIBase owner) { for (byte i = 0; i < 4; i++) {
        ApiEventManager.OnSpellCast.RemoveListener(this, _owner.Spells[i], OnSpellCast);
    }}
    
}