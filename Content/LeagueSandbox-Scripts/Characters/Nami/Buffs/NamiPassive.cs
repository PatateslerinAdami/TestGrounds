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

internal class NamiPassive : IBuffGameScript {
    private const float          DecayIntervalMs = 375.0f;
    private const float          DecayStep       = 0.25f;
    private const int            MaxDecaySteps   = 4;
    
    ObjAIBase              _nami;
    private AttackableUnit _unit;
    Particle               _surgingTidesParticle;
    private float          _baseValue;
    private float          _tickTime;
    private int            _decayStepsApplied;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HASTE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _nami                             = ownerSpell.CastInfo.Owner;
        _baseValue                        = 40 + _nami.Stats.AbilityPower.Total * 0.1f;
        _unit                 = unit;
        StatsModifier.MoveSpeed.FlatBonus = _baseValue;
        unit.AddStatModifier(StatsModifier);
        _tickTime          = 0.0f;
        _decayStepsApplied = 0;
        switch (_nami.SkinID) {
            default: _surgingTidesParticle = AddParticleTarget(_nami, unit,"Nami_Base_P_buf", unit,buff.Duration); break;
        }
        SetBuffToolTipVar(buff, 0, _nami.Stats.AbilityPower.Total * 0.1f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_surgingTidesParticle);
    }

    public void OnUpdate(float diff) {
        _tickTime += diff;
        while (_tickTime >= DecayIntervalMs && _decayStepsApplied < MaxDecaySteps) {
            _tickTime -= DecayIntervalMs;
            _decayStepsApplied++;

            var multiplier = 1.0f - (DecayStep * _decayStepsApplied);
            if (multiplier < 0.0f) multiplier = 0.0f;

            _unit.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.FlatBonus = _baseValue * multiplier;
            _unit.AddStatModifier(StatsModifier);
        }
    }
}
