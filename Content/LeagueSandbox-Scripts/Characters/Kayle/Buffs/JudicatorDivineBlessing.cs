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

internal class JudicatorDivineBlessing : IBuffGameScript {
    private ObjAIBase        _kayle;
    private AttackableUnit   _unit;
    private Particle _p1, _p2, _p3;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HEAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _kayle = ownerSpell.CastInfo.Owner;
        _unit  = unit;

        var ap        = _kayle.Stats.AbilityPower.Total / 100f * ownerSpell.SpellData.Coefficient2;
        var moveSpeed = 0.18f + 0.03f * (ownerSpell.CastInfo.SpellLevel - 1) + ap;

        var healAp     = _kayle.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
        var healAmount = 60f + 45 * (ownerSpell.CastInfo.SpellLevel - 1) + healAp;
        
        _p1 = AddParticleTarget(_kayle, unit, "InterventionHeal_buf",  unit, buff.Duration);
        _p2 = AddParticleTarget(_kayle, unit, "Interventionspeed_buf", unit, buff.Duration);
        _p3 = AddParticleTarget(_kayle, unit, "Intervention_tar", unit, buff.Duration);
        
        StatsModifier.MoveSpeed.PercentBonus = moveSpeed;
        unit.AddStatModifier(StatsModifier);
        _unit.TakeHeal(_kayle, healAmount, unit == _kayle ? HealType.SelfHeal : HealType.OutgoingHeal);
        ApplyAssistMarker(unit, _kayle, 10.0f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
    }
}