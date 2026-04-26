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

public class UdyrTurtleActivation : IBuffGameScript {
    private float     _shieldHealth;
    private Buff      _buff;
    private ObjAIBase _udyr;

    private Shield   _turtleShield;
    private Particle _particle1;
    private Particle _particle2;
    private Particle _particle3;
    private Particle _particle4;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell spell) {
        _buff = buff;
        _udyr = spell.CastInfo.Owner;
        var ap    = _udyr.Stats.AbilityPower.Total * 0.45f;
        _shieldHealth  = 60f + 30 * (_udyr.GetSpell("UdyrTurtleStance").CastInfo.SpellLevel - 1) + ap;
        _turtleShield = new Shield(_udyr, _udyr, true, true, _shieldHealth);
        unit.AddShield(_turtleShield);
        
        _particle3 = AddParticleTarget(_udyr, _udyr, "TurtleStance.troy", _udyr, 2f, size: _udyr.Stats.Size.Total, bone: "BUFFBONE_GLB_GROUND_LOC");
        _particle2 = AddParticleTarget(_udyr,_udyr,"Udyr_TurtleStance_buf", _udyr, 5f, size: _udyr.Stats.Size.Total, bone:"BUFFBONE_GLB_GROUND_LOC");
        _particle1 = AddParticleTarget(_udyr,_udyr, "TurtleStance_buf",_udyr,5f, size: _udyr.Stats.Size.Total, bone: "BUFFBONE_GLB_GROUND_LOC"); //,
        _particle4 = AddParticleTarget(_udyr,_udyr,"UdyrTurtleStance",_udyr);
    }
    
    public void OnUpdate(float diff) {
        if (!_turtleShield.IsConsumed()) return;
        RemoveParticle(_particle1);
        RemoveParticle(_particle2); 
        RemoveParticle(_particle3);
        RemoveParticle(_particle4);
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) { unit.RemoveShield(_turtleShield); RemoveParticle(_particle1); RemoveParticle(_particle2); RemoveParticle(_particle3); }
}