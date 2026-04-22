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

internal class EvelynnWPassive : IBuffGameScript {
    ObjAIBase        _evelynn;
    private Particle _haste;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HASTE,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks = 4
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _evelynn = ownerSpell.CastInfo.Owner;
        StatsModifier.MoveSpeed.FlatBonus += 4 + 4 *(_evelynn.Spells[1].CastInfo.SpellLevel -1);

        unit.AddStatModifier(StatsModifier);
        _haste = _evelynn.SkinID switch {
            _ => buff.StackCount switch {
                2 => AddParticleTarget(ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, "Evelynn_W_passive_02",
                                       unit,                      buff.Duration),
                3 => AddParticleTarget(ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, "Evelynn_W_passive_03",
                                       unit,                      buff.Duration),
                4 => AddParticleTarget(ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, "Evelynn_W_passive_04",
                                       unit,                      buff.Duration),
                _ => AddParticleTarget(ownerSpell.CastInfo.Owner, ownerSpell.CastInfo.Owner, "Evelynn_W_passive_01",
                                       unit,                      buff.Duration)
            }
        };
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_haste);
    }
}