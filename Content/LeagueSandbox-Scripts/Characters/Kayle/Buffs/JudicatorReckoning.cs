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

internal class JudicatorReckoning : IBuffGameScript {
    private ObjAIBase        _kayle;
    private Particle _slow;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _kayle = buff.SourceUnit;
        _slow  = SpellEffectCreate("reckoning_tar.troy",_kayle, unit,  null, boneName: "C_Buffbone_Glb_Center_Loc", flags: FXFlags.UpdateOrientation, keywordObject: _kayle);
        
        var slowPercentage  = ownerSpell.SpellData.EffectLevelAmount[2][ownerSpell.CastInfo.SpellLevel]/100f;
        StatsModifier.MoveSpeed.PercentBonus -= slowPercentage;
        unit.AddStatModifier(StatsModifier);
        ApplyAssistMarker(unit, _kayle, 10.0f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_slow);
    }
}