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

public class KarmaSolKimShieldLocket : IBuffGameScript {
    private       float          _shieldHealth;
    private       Buff           _buff;
    private       ObjAIBase      _karma;
    private       AttackableUnit _unit;
    private const float          DecayIntervalMs    = 500.0f;
    private const int            MaxDecaySteps      = 3;
    private       float          _tickTime          = 0f;
    private       int            _decayStepsApplied = 0;
    private       float          _baseValue;
    
    private Shield   _solKimShieldLocket;
    private Particle _particle1;
    private Particle _particle2;
    private Particle _particle3;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _buff  = buff;
        _karma = ownerspell.CastInfo.Owner;
        _unit  = unit;
        var ap    = _karma.Stats.AbilityPower.Total * 0.45f;
        _shieldHealth  = 60f + 30 * (_karma.GetSpell("KarmaSolKimShield").CastInfo.SpellLevel - 1) + ap;
        _solKimShieldLocket = new Shield(_karma, _karma, true, true, _shieldHealth);
        _particle3 = AddParticleTarget(_karma,_karma, "Karma_Base_E_shield_R_01",_karma,buff.Duration, size: _karma.Stats.Size.Total, bone: "C_BUFFBONE_GLB_CENTER_LOC");
        _particle2 = AddParticleTarget(_karma,_karma,"Karma_Base_E_speed_buf", _karma, buff.Duration, size: _karma.Stats.Size.Total);
        _particle1 = AddParticleTarget(_karma,_unit, "Karma_Base_E_shield_01",_unit,buff.Duration, size: _karma.Stats.Size.Total, "C_BUFFBONE_GLB_CENTER_LOC");
        unit.AddShield(_solKimShieldLocket);
        _baseValue                           = 0.4f + 0.05f * (ownerspell.CastInfo.SpellLevel - 1) + 0.15f;
        StatsModifier.MoveSpeed.PercentBonus = _baseValue;
        unit.AddStatModifier(StatsModifier);
    }
    
    public void OnUpdate(float diff) {
        if (_solKimShieldLocket.IsConsumed()) {
            RemoveParticle(_particle1);
            RemoveParticle(_particle2); 
            _buff.DeactivateBuff();
        }

        _tickTime += diff;
        while (_tickTime >= DecayIntervalMs && _decayStepsApplied < MaxDecaySteps) {
            _tickTime -= DecayIntervalMs;
            _decayStepsApplied++;
            _unit.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.PercentBonus = _baseValue - (_baseValue / MaxDecaySteps * _decayStepsApplied);
            _unit.AddStatModifier(StatsModifier);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        unit.RemoveShield(_solKimShieldLocket);
        RemoveParticle(_particle1);
        RemoveParticle(_particle2);
        RemoveParticle(_particle3);
    }
}
