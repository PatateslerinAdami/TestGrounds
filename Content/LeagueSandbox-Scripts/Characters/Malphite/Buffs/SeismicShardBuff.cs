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

internal class SeismicShardBuff : IBuffGameScript {
    ObjAIBase        _malphite;
    AttackableUnit _unit;
    private Particle _slow;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();
    public StatsModifier StatsModifier2 { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _malphite = ownerSpell.CastInfo.Owner;
        _unit     = unit;
        
        var slowPercentage = 0.14f + 0.03f * (ownerSpell.CastInfo.SpellLevel - 1);
        var stolenMovespeed      = _malphite.Stats.MoveSpeed.Total * slowPercentage;
        
        _slow  = AddParticleTarget(ownerSpell.CastInfo.Owner, null, "Global_Slow", unit, buff.Duration, bone: "BUFFBONE_GLB_GROUND_LOC");
        ApplyAssistMarker(unit, ownerSpell.CastInfo.Owner, 10.0f);
        
        StatsModifier.MoveSpeed.PercentBonus -= slowPercentage;
        unit.AddStatModifier(StatsModifier);
        StatsModifier2.MoveSpeed.FlatBonus += stolenMovespeed;
        _malphite.AddStatModifier(StatsModifier2);
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_slow);
        _malphite.RemoveStatModifier(StatsModifier2);
    }
}