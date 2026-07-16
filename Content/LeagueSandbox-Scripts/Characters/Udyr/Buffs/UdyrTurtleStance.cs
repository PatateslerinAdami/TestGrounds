using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class UdyrTurtleStance : IBuffGameScript {
    private ObjAIBase _udyr;
    private Spell     _spell;
    private Buff      _buff;
    private Particle  _particle1;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
            PersistsThroughDeath = true,
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _udyr  = buff.SourceUnit;
        _spell = ownerSpell;
        _buff = buff;
        _udyr.ChangeModel("UdyrTurtle");
        OverrideAutoAttack(_udyr, "UdyrTurtleAttack", false);
        _particle1 = AddParticleTarget(_udyr,_udyr,"turtlepelt",_udyr, bone: "head", lifetime: buff.Duration);
        StatsModifier.LifeSteal.FlatBonus = 0.1f + 0.02f * (_spell.CastInfo.SpellLevel - 1); 
        _udyr.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveOverrideAutoAttack(_udyr);
        RemoveParticle(_particle1);
    }
}