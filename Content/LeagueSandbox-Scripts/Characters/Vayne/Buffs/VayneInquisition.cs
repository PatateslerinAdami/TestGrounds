using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class VayneInquisition : IBuffGameScript {
    private ObjAIBase        _vayne; 
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _vayne = ownerSpell.CastInfo.Owner;
        _vayne.SetAnimStates(new Dictionary<string, string> {
            { "Idle1", "Idle_Ult" },
            { "Idle2", "Idle_Ult" },
            { "Idle3", "Idle_Ult" },
            { "Idle4", "Idle_Ult" },
            { "Run", "Run_Ult" },
            { "Attack1", "Attack_Ult" },
            { "Attack2", "Attack_Ult" }
        });
        _vayne.SetAutoAttackSpell("VayneUltAttack", false);
        
        var spellLevel = ownerSpell.CastInfo.SpellLevel;
        StatsModifier.AttackDamage.FlatBonus = spellLevel switch {
            3 => 70f,
            2 => 50f,
            _ => 30f,
        };
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _vayne.SetAnimStates(new Dictionary<string, string> {
            { "Idle1", "" },
            { "Idle2", "" },
            { "Idle3", "" },
            { "Idle4", "" },
            { "Run", "" },
            { "Attack1", "" },
            { "Attack2", "" }
        }); 
        _vayne.ResetAutoAttackSpell();
    }
}
