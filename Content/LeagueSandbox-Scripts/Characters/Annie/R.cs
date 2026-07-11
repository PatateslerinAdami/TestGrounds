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

public class InfernalGuardian : ISpellScript
{
    private ObjAIBase _annie;
    private bool _shouldStun = false;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        IsPetDurationBuff = true,
        NotSingleTargetSpell = true,
        SpellDamageRatio = 0.5f,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _annie = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var ap = _annie.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var totalDamage = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
        target.TakeDamage(spell.CastInfo.Owner, totalDamage, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
        if (!_shouldStun) return;
        var stunDuration = _annie.Stats.Level switch
        {
            < 6 => 1.25f,
            < 11 => 1.5f,
            _ => 1.75f
        };
        AddBuff("Stun", stunDuration, 1, spell, target, _annie);
    }

    public void OnSpellPostCast(Spell spell)
    {
        var tibbers = CreatePet
        (
            _annie as Champion,
            spell,
            new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z),
            "Tibbers",
            "AnnieTibbers",
            "InfernalGuardianBurning",
            45.0f,
            showMinimapIfClone: false,
            isClone: false
        );
        var guideSpell = SetSpell(_annie, "InfernalGuardianGuide", SpellSlotType.SpellSlots, 3);
        AddBuff("InfernalGuardianTimer", 45.0f, 1, guideSpell, _annie, _annie);

        var particleName = _annie.SkinID switch
        {
            1 => "Annie_skin02_R_cas.troy",
            5 => "Annie_skin05_R_cas.troy",
            8 => "Annie_skin09_R_cas.troy",
            _ => "Annie_R_cas.troy"
        };
        SpellEffectCreate(particleName, _annie, null, null, tibbers.Position, targetPos: tibbers.Position, scale: 1f,
            flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);

        if (_annie.HasBuff("Pyromania_particle"))
        {
            _shouldStun = true;
            RemoveBuff(_annie, "Pyromania_particle");
        }
        else
        {
            AddBuff("Pyromania", 25000f, 1, spell, _annie, _annie, true);
        }

        var enemiesInRange = GetUnitsInRange(_annie, tibbers.Position, spell.SpellData.CastRadius[0], true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectNeutral);
        foreach (var enemy in enemiesInRange)
        {
            spell.ApplyEffects(enemy);
        }
    }
}

public class InfernalGuardianGuide : BasePetController
{
}