using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class SwainShadowGraspRoot : IBuffGameScript {
    private Particle       _root, _root2;
    private AttackableUnit _unit;
    private Buff           _buff;
    private PeriodicTicker _periodicTimer;
    private bool _requestDeactivate = false;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SNARE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit  = unit;
        _buff  = buff;
        unit.StopMovement();
        SetStatus(unit, StatusFlags.CanMove, false);
        SetStatus(unit, StatusFlags.Rooted, true);
        _root  = AddParticleTarget(ownerSpell.CastInfo.Owner, null, "SwainShadowGraspRootTemp", unit, buff.Duration);
        _root2 = AddParticleTarget(ownerSpell.CastInfo.Owner, null, "swain_shadowGrasp_magic",  unit, buff.Duration);
        ApiEventManager.OnDeath.AddListener(this, unit, OnDie);
        
        
    }

    public void OnUpdate(float diff) {
        if (!_requestDeactivate) return;
        var ticks = _periodicTimer.ConsumeTicks(diff, 700f, false, 1);
        if (ticks != 1) return;
        _buff.DeactivateBuff();
        _requestDeactivate = false;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_root); 
        RemoveParticle(_root2);
        SetStatus(unit, StatusFlags.CanMove, true);
        SetStatus(unit, StatusFlags.Rooted, false);
    }

    private void OnDie(DeathData data) {
        _requestDeactivate = true;
    }

}