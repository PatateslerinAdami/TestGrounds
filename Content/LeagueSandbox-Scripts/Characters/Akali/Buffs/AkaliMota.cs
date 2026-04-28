using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class AkaliMota : IBuffGameScript {
    private ObjAIBase _akali;
    private Spell     _spell;
    private Buff      _buff;
    private Particle  _p, _p1;


    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _akali = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        _buff  = buff;
        _p = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "akali_markOftheAssasin_marker_tar", unit, buff.Duration, bone: "C_BUFFBONE_GLB_OVERHEAD_LOC");
        _p1 = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "akali_markOftheAssasin_marker_tar_02", unit, buff.Duration);
        ApiEventManager.OnHitUnit.AddListener(this, _akali, OnHitUnit);
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p);
        RemoveParticle(_p1);
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void OnHitUnit(DamageData data) {
        var markApRatio = _spell.CastInfo.Owner.Stats.AbilityPower.Total * 0.5f;
        var markDamage  = 45 + 25 * (_spell.CastInfo.SpellLevel - 1) + markApRatio;

        if (!data.Target.HasBuff("AkaliMota")) return;
        AddParticleTarget(_akali, data.Target, "akali_mark_impact_tar", data.Target);
        data.Target.TakeDamage(_akali, markDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                               false);
        var energyReturn = 20f + 5f * (_spell.CastInfo.SpellLevel - 1);
        _akali.Stats.CurrentMana += energyReturn;
        _buff.DeactivateBuff();
    }
}