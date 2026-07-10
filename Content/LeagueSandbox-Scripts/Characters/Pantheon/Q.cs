using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class PantheonQ : ISpellScript
{
    private ObjAIBase _pantheon;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        },
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _pantheon = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        FaceDirection(target.Position, owner, true);
    }

    public void OnSpellCast(Spell spell)
    {
        AddParticle(_pantheon, _pantheon, "pantheon_spearShot_cas", _pantheon.Position);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {

        var ad = _pantheon.Stats.AttackDamage.FlatBonus * spell.SpellData.Coefficient;
        var dmg = spell.SpellData.EffectLevelAmount[2][spell.CastInfo.SpellLevel] + ad;
        var critDmg = dmg * _pantheon.Stats.CriticalDamage.Total;

        AddParticleTarget(_pantheon, target, "Pantheon_Base_Q_tar.troy", target, bone: "C_Buffbone_Glb_Chest_Loc", flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
        var crit = _pantheon.HasBuff("PantheonEPassive") &&
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