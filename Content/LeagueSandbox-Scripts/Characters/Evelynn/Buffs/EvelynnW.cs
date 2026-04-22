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

internal class EvelynnW : IBuffGameScript {
    ObjAIBase        _evelynn;
    private Particle _haste;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HASTE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _evelynn                          =  ownerSpell.CastInfo.Owner;
        _evelynn.SetStatus(StatusFlags.Ghosted, true);
        StatsModifier.MoveSpeed.PercentBonus += 0.3f + 0.1f *(_evelynn.Spells[1].CastInfo.SpellLevel -1);

        unit.AddStatModifier(StatsModifier);
        switch (_evelynn.SkinID) {
            default: _haste = 
                AddParticleTarget(ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, "Evelynn_W_active_buf", unit, buff.Duration); 
                AddParticleTarget(ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, "Evelynn_W_cas", unit, buff.Duration); 
                break;
        }
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _evelynn.SetStatus(StatusFlags.Ghosted, false);
        RemoveParticle(_haste);
    }
}