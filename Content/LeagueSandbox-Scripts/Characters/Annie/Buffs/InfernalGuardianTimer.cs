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

internal class InfernalGuardianTimer : IBuffGameScript {
    private ObjAIBase _annie;
    private Buff _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        PersistsThroughDeath = true,
        IsNonDispellable = true
    };

    public StatsModifier StatsModifier { get; }
    
    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _annie = buff.SourceUnit;
        _buff = buff;
        ownerSpell.SetCooldown(0f, true);
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        SetSpell(buff.SourceUnit, "InfernalGuardian", SpellSlotType.SpellSlots, 3);
        var baseSpell = SetSpell(_annie, "InfernalGuardian", SpellSlotType.SpellSlots, 3);
        _annie.Spells[3].SetCooldown(baseSpell.GetCooldown(), false);
    }

    
}
