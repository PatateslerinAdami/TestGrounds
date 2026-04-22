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

internal class EvelynnE : IBuffGameScript {
    ObjAIBase        _evelynn;
    private Particle _haste;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _evelynn                          =  ownerSpell.CastInfo.Owner;
        _evelynn.SetStatus(StatusFlags.Ghosted, true);
        StatsModifier.AttackSpeed.PercentBonus += 0.6f + 0.15f *(ownerSpell.CastInfo.SpellLevel -1);

        unit.AddStatModifier(StatsModifier);
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _evelynn.SetStatus(StatusFlags.Ghosted, false);
        RemoveParticle(_haste);
    }
}