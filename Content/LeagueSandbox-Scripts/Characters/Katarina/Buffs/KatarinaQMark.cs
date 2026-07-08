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

internal class KatarinaQMark : IBuffGameScript
{
    private Particle P;
    private ObjAIBase _katarina;
    private AttackableUnit _unit;
    private Buff _buff;
    private Spell _spell;


    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _katarina = buff.SourceUnit;
        _unit = unit;
        _buff = buff;
        _spell = ownerSpell;
        P = _katarina.SkinID switch
        {
            9 => AddParticleTarget(_katarina, unit, "Katarina_Skin09_daggered", unit, buff.Duration),
            7 => AddParticleTarget(_katarina, unit, "Katarina_XMas_daggered", unit, buff.Duration),
            _ => AddParticleTarget(_katarina, unit, "katarina_daggered", unit, buff.Duration)
        };
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaW"), OnSpellHit);
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaE"), OnSpellHit);
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaRMis"), OnSpellHit);
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaQMis"), OnSpellHit);
        ApiEventManager.OnSpellHit.AddListener(this, _katarina.GetSpell("KatarinaQ"), OnSpellHit);
        ApiEventManager.OnHitUnit.AddListener(this, _katarina, OnHitUnit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        if (target != _unit) return;
        var markApRatio = _katarina.Stats.AbilityPower.Total * _spell.SpellData.Coefficient2;
        var markDamage = _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel] + markApRatio;
        switch (_katarina.SkinID)
        {
            case 9: AddParticleTarget(_katarina, target, "Katarina_Skin09_Q_tar_enhanced", target); break;
            case 6: AddParticleTarget(_katarina, target, "Katarina_enhanced2_sand", target); break;
            default: AddParticleTarget(_katarina, target, "katarina_enhanced2", target); break;
        }

        target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
            DamageResultType.RESULT_NORMAL);
        _buff.DeactivateBuff();
    }

    private void OnHitUnit(DamageData data)
    {
        if (data.Target != _unit) return;
        var markApRatio = _katarina.Stats.AbilityPower.Total * _spell.SpellData.Coefficient2;
        var markDamage = _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel] + markApRatio;
        switch (_katarina.SkinID)
        {
            case 9: AddParticleTarget(_katarina, _unit, "Katarina_Skin09_Q_tar_enhanced", _unit); break;
            case 6: AddParticleTarget(_katarina, _unit, "Katarina_enhanced2_sand", _unit); break;
            default: AddParticleTarget(_katarina, _unit, "katarina_enhanced2", _unit); break;
        }

        data.Target.TakeDamage(_katarina, markDamage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PROC,
            DamageResultType.RESULT_NORMAL);
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(P);
    }
}