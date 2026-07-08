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

public class DianaShield : IBuffGameScript {
    private float     _shieldHealth;
    private Buff      _buff;
    private ObjAIBase _diana;

    private Shield   _orbShield;
    private Particle _shieldParticle;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell spell) {
        _buff = buff;
        _diana = spell.CastInfo.Owner;
        var ap    = _diana.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        _shieldHealth  = 40f + 15f * (spell.CastInfo.SpellLevel - 1) + ap;
        var isRefresh = buff.BuffVars.GetBool("isRefresh");
        if (isRefresh)
        {
            _shieldHealth *= 2;
            AddParticleTarget(_diana, _diana, "Diana_Base_W_Refreshed", _diana);
        }
        _orbShield = new Shield(_diana, _diana, true, true, _shieldHealth, buff);
        unit.AddShield(_orbShield);
        
        _shieldParticle = AddParticleTarget(_diana, _diana, "Diana_Base_W_Shield.troy", _diana, buff.Duration);
    }
    
    public void OnUpdate(Buff buff, float diff) {
        if (!_orbShield.IsConsumed()) return;
        RemoveParticle(_shieldParticle);
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell)
    {
        unit.RemoveShield(_orbShield); RemoveParticle(_shieldParticle);
    }
}