using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class SionPassiveSpeed : IBuffGameScript {
    private ObjAIBase _sion;
    private Buff _buff;
    private Particle _p1;
    private const float MaxDecaySteps = 10f;
    private  float _baseValue;
    private float _decayStepsApplied = 0;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier  { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _sion = buff.SourceUnit;
        _buff = buff;
        _p1 = SpellEffectCreate("Sion_Base_Passive_Speed.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        _baseValue = 0.66f;
        _decayStepsApplied = 0;
        ApiEventManager.OnResurrect.AddListener(this, _sion, OnResurrect);
    }

    private void OnResurrect(ObjAIBase sion)
    {
        RemoveBuff(_buff);
    }

    public void OnUpdate(Buff buff, float diff)
    {
        ExecutePeriodically(buff.BuffVars, "SionPassiveSpeed", 500f, true, 7, () =>
        {
            _decayStepsApplied++;
            buff.TargetUnit.RemoveStatModifier(StatsModifier);
            StatsModifier.MoveSpeed.PercentBonus = _baseValue - (_baseValue / MaxDecaySteps * _decayStepsApplied);
            buff.TargetUnit.AddStatModifier(StatsModifier);
        });
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.OnResurrect.RemoveListener(this, _sion, OnResurrect);
        RemoveParticle(_p1);
    }
}