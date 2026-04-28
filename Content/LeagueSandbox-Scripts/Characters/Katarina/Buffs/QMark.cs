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

internal class KatarinaQMark : IBuffGameScript {
    //find blade particle
    public Particle        P;
    ObjAIBase              _katarina;
    private AttackableUnit _unit;
    private Buff           _buff;
    

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _katarina = ownerSpell.CastInfo.Owner;
        _unit = unit;
        _buff = buff;
        P = _katarina.SkinID switch {
            9  => AddParticleTarget(_katarina, unit, "Katarina_Skin09_daggered", unit, buff.Duration),
            7 => AddParticleTarget(_katarina, unit, "Katarina_XMas_daggered", unit, buff.Duration),
            _ => AddParticleTarget(_katarina, unit, "katarina_daggered",      unit, buff.Duration)
        };
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaW"),    OnSpellHit);
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaE"),    OnSpellHit);
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaRMis"), OnSpellHit);
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaQMis"), OnSpellHit);
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaQ"),    OnSpellHit);
        ApiEventManager.OnHitUnit.AddListener(this, _katarina, OnHitUnit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (target != _unit) return;
        var markApRatio = _katarina.Stats.AbilityPower.Total * 0.2f;
        var markDamage  = 15 + 15 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1) + markApRatio;
        switch (_katarina.SkinID) {
            case 9: AddParticleTarget(_katarina, target, "Katarina_Skin09_Q_tar_enhanced", target, bone:"BUFFBONE_GLB_GROUND_LOC"); break;
            case 6: AddParticleTarget(_katarina, target, "Katarina_enhanced2_sand", target, bone:"BUFFBONE_GLB_GROUND_LOC"); break;
            default: AddParticleTarget(_katarina, target, "katarina_enhanced2", target, bone:"BUFFBONE_GLB_GROUND_LOC"); break;
        }
        target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
        ApiEventManager.RemoveAllListenersForOwner(this);
        _buff.DeactivateBuff(); 
    }

    private void OnHitUnit(DamageData data) {
        if (data.Target != _unit) return;
        var markApRatio = _katarina.Stats.AbilityPower.Total * 0.2f;
        var markDamage  = 15 + 15 * (_katarina.GetSpell("KatarinaQ").CastInfo.SpellLevel - 1) + markApRatio;
        switch (_katarina.SkinID) {
            case 9: AddParticleTarget(_katarina, _unit, "Katarina_Skin09_Q_tar_enhanced", _unit, bone:"BUFFBONE_GLB_GROUND_LOC"); break;
            case 6: AddParticleTarget(_katarina, _unit, "Katarina_enhanced2_sand", _unit, bone:"BUFFBONE_GLB_GROUND_LOC"); break;
            default: AddParticleTarget(_katarina, _unit, "katarina_enhanced2", _unit, bone:"BUFFBONE_GLB_GROUND_LOC"); break;
        }
        data.Target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC, DamageResultType.RESULT_NORMAL);
        ApiEventManager.RemoveAllListenersForOwner(this);
        _buff.DeactivateBuff(); 
    }
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(P); 
    }
}
