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

public class UdyrTigerStance : IBuffGameScript {
    private ObjAIBase _udyr;
    private Spell     _spell;
    private Particle  _particle1, _particle2, _particle3;
    private bool _firstHit = true;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _udyr  = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _udyr.ChangeModel("UdyrTiger");
        _udyr.SetAutoAttackSpell("UdyrTigerAttack", false);
        ApiEventManager.OnHitUnit.AddListener(this, _udyr, OnHit);
        ApiEventManager.OnPreDealDamage.AddListener(this, _udyr, OnPreDealDamage);
        _particle1 = AddParticleTarget(_udyr,_udyr,"tigerpelt",_udyr, bone: "head", lifetime: buff.Duration);
        //_particle1 = AddParticleTarget(_udyr,_udyr, "TurtleStance_buf",_udyr,5f, size: _udyr.Stats.Size.Total, bone: "BUFFBONE_GLB_GROUND_LOC"); //,Udyr_Tiger_buf_R_max
    }

    private void OnPreDealDamage(DamageData data) {
        if (data.DamageSource != DamageSource.DAMAGE_SOURCE_ATTACK) return;
        var bonusDmg = _udyr.Stats.AttackDamage.Total * 0.15f;
        data.PostMitigationDamage += data.Target.Stats.GetPostMitigationDamage(bonusDmg, DamageType.DAMAGE_TYPE_PHYSICAL, _udyr);
    }

    private void OnHit(DamageData data) {
        if (!_firstHit) return;
        _particle2 = AddParticleTarget(_udyr, data.Target, "udyr_tiger_tar",      data.Target, 1);
        _particle3 = AddParticleTarget(_udyr, data.Target, "udyr_tiger_claw_tar", data.Target, 1);
        AddBuff("UdyrTigerPunchBleed", 2f, 1, _spell, data.Target, _udyr);
        _firstHit = false;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _udyr.ResetAutoAttackSpell();
        RemoveParticle(_particle1);
        RemoveParticle(_particle2);
        RemoveParticle(_particle3);
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}