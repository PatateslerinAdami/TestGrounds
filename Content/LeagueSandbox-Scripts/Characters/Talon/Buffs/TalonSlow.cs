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

internal class TalonSlow : IBuffGameScript {
    private ObjAIBase        _talon;
    private AttackableUnit   _unit;
    private Particle _slow;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _talon = ownerSpell.CastInfo.Owner;
        _unit     = unit;
        
        // TalonRake.json Effect2: 20/25/30/35/40% by W level (duration 2s = Effect4).
        var wLevel = _talon.GetSpell("TalonRake")?.CastInfo.SpellLevel ?? 1;
        var slowPercentage = 0.15f + 0.05f * wLevel;
        
        _slow  = AddParticleTarget(ownerSpell.CastInfo.Owner, null, "Global_Slow", unit, buff.Duration, bone: "BUFFBONE_GLB_GROUND_LOC");
        
        StatsModifier.MoveSpeed.PercentBonus -= slowPercentage;
        unit.AddStatModifier(StatsModifier);
        ApplyAssistMarker(unit, ownerSpell.CastInfo.Owner, 10.0f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_slow);
    }
}