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

internal class AkaliTwilightShroudBuff : IBuffGameScript {
    private const float          DecayIntervalMs = 250.0f;
    private const int            MaxDecaySteps   = 4;
    
    private ObjAIBase      _akali;
    private AttackableUnit _unit;
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
         
        _baseValue                        = 0.2f + 0.2f *(ownerSpell.CastInfo.SpellLevel -1);
        _unit                 = unit;
        StatsModifier.MoveSpeed.PercentBonus = _baseValue;
        unit.AddStatModifier(StatsModifier);
        _tickTime          = 0.0f;
        _decayStepsApplied = 0;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }

    public void OnUpdate(float diff) {
        _tickTime += diff;
        while (_tickTime >= DecayIntervalMs && _decayStepsApplied < MaxDecaySteps) {
            _tickTime -= DecayIntervalMs;
            _decayStepsApplied++;

            _unit.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.PercentBonus = _baseValue - (_baseValue / MaxDecaySteps * _decayStepsApplied);
            _unit.AddStatModifier(StatsModifier);
        }
    }
}
