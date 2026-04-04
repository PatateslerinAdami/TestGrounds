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

internal class JudicatorIntervention : IBuffGameScript {
    private ObjAIBase        _kayle;
    private AttackableUnit   _unit;
    private Particle _p1, _p2, _p3;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INVULNERABILITY,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _kayle = ownerSpell.CastInfo.Owner;
        _unit  = unit;
        SetStatus(unit, StatusFlags.Invulnerable, true);
        PlayAnimation( _kayle,"Spell4");
        _p1 = AddParticleTarget(_kayle, unit, _unit == _kayle ? "eyeforaneye_self" : "eyeforaneye_cas", unit, buff.Duration, bone: "C_BUFFBONE_GLB_CENTER_LOC");
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p1);
        RemoveParticle(_p2);
        RemoveParticle(_p3);
        SetStatus(unit, StatusFlags.Invulnerable, false);
    }
}