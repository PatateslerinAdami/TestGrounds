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

public class EvelynnRShield : IBuffGameScript {
    private float     _shieldHealth;
    private Buff      _buff;
    private ObjAIBase _evelynn;

    private Shield   _agonyShield;
    private Particle _particle1;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _evelynn = ownerspell.CastInfo.Owner;
        _buff = buff;
        _shieldHealth = buff.Variables.GetFloat("shieldAmount");
        _agonyShield  = new Shield(_evelynn, _evelynn, true, true, _shieldHealth);
        unit.AddShield(_agonyShield);
        _particle1 = AddParticleTarget(_evelynn,_evelynn, "Evelynn_R_shield",_evelynn,buff.Duration, size: _evelynn.Stats.Size.Total);
    }
    
    public void OnUpdate(float diff) {
        if (!_agonyShield.IsConsumed()) return;
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
        RemoveParticle(_particle1);
        unit.RemoveShield(_agonyShield);
    }
}