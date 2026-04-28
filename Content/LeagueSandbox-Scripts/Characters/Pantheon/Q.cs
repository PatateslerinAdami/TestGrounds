using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class PantheonQ : ISpellScript
{
    private ObjAIBase _pantheon;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _pantheon = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate);
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        FaceDirection(target.Position, owner, true);
    }

    public void OnSpellCast(Spell spell)
    {
        var owner = spell.CastInfo.Owner;
        AddParticle(owner, owner, "pantheon_spearShot_cas", owner.Position);
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell)
    {
        ApiEventManager.OnSpellHit.RemoveListener(this, _spell, TargetExecute);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector)
    {
        var owner = spell.CastInfo.Owner;

        var ad = owner.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        var dmg = 75 + 40 * (spell.CastInfo.SpellLevel - 1) + ad;
        var critDmg = dmg * 1.25f;

        AddParticleTarget(owner, target, "pantheon_spearShot_tar", target, bone: "C_BUFFBONE_GLB_HEAD_LOC");
        AddParticleTarget(owner, target, "pantheon_spearShot_tar_02", target, bone: "BUFFBONE_GLB_GROUND_LOC");
        var crit = owner.HasBuff("PantheonEPassive") &&
                   target.Stats.CurrentHealth < target.Stats.HealthPoints.Total * 0.15f;
        target.TakeDamage(
            _pantheon,
            crit ? critDmg : dmg,
            DamageType.DAMAGE_TYPE_PHYSICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE,
            crit
        );
    }

    private void OnStatsUpdate(AttackableUnit unit, float diff)
    {
        var bonusAd = _pantheon.Stats.AttackDamage.Total - _pantheon.Stats.AttackDamage.BaseValue;
        bonusAd *= 1.4f;
        SetSpellToolTipVar(_pantheon, 0, bonusAd, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}