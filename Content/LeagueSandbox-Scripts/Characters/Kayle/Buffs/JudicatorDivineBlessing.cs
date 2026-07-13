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
    private Particle _p1, _p2;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _kayle = buff.SourceUnit;

        var ap        = _kayle.Stats.AbilityPower.Total / 100f * ownerSpell.SpellData.Coefficient2;
        var moveSpeed = ownerSpell.SpellData.EffectLevelAmount[2][ownerSpell.CastInfo.SpellLevel]/100 + ap;

        var healAp     = _kayle.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
        var healAmount = ownerSpell.SpellData.EffectLevelAmount[1][ownerSpell.CastInfo.SpellLevel] + healAp;
        
        _p1 = SpellEffectCreate("Intervention_tar.troy",_kayle, unit,  keywordObject: _kayle, flags: FXFlags.UpdateOrientation, orientTowards: _kayle.GetPosition3D());
        
        StatsModifier.MoveSpeed.PercentBonus = moveSpeed;
        unit.AddStatModifier(StatsModifier);
        unit.TakeHeal(_kayle, healAmount, unit == _kayle ? HealType.SelfHeal : HealType.OutgoingHeal);
        ApplyAssistMarker(unit, _kayle, 10.0f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
    }
}