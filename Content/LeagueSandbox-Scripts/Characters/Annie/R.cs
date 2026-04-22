using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class InfernalGuardian : ISpellScript {
    private ObjAIBase _annie;
    private bool      _shouldStun = false;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        IsPetDurationBuff  = true,
        NotSingleTargetSpell  = true,
        SpellDamageRatio   = 0.5f,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _annie = owner; }

    public void OnSpellPostCast(Spell spell) {
        var tibbers = CreatePet
        (
            _annie as Champion,
            spell,
            new Vector2(spell.CastInfo.TargetPosition.X, spell.CastInfo.TargetPosition.Z),
            "Tibbers",
            "AnnieTibbers",
            "InfernalGuardian",
            45.0f,
            showMinimapIfClone: false,
            isClone: false
        );
        var guideSpell = SetSpell(_annie, "InfernalGuardianGuide", SpellSlotType.SpellSlots, 3);


        AddBuff("InfernalGuardianBurning", 45.0f, 1, spell, tibbers, _annie);
        AddBuff("InfernalGuardianTimer",   45.0f, 1, spell, _annie,  _annie);

        // Pyromania stuff here

        string particles;
        switch (_annie.SkinID) {
            case 2:
                particles = "Annie_skin02_R_cas";
                break;
            case 5:
                particles = "Annie_skin05_R_cas";
                break;
            case 9:
                particles = "Annie_skin09_R_cas";
                break;
            default:
                particles = "Annie_R_cas";
                break;
        }

        AddParticle(_annie, null, particles, tibbers.Position);

        if (_annie.HasBuff("Pyromania_particle")) {
            _shouldStun = true;
            RemoveBuff(_annie, "Pyromania_particle");
        } else {
            AddBuff("Pyromania", 250000f, 1, spell, _annie, _annie, true);
            if (_annie.GetBuffsWithName("Pyromania").Count == 4) {
                RemoveBuff(_annie, "Pyromania");
                AddBuff("Pyromania_particle", 25000f, 1, spell, _annie, _annie, true);
            }
        }

        var enemiesInRange = GetUnitsInRange(_annie, tibbers.Position, spell.SpellData.CastRadius[0], true,
                                             SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                             SpellDataFlags.AffectMinions |
                                             SpellDataFlags.AffectNeutral);
        var ap          = _annie.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var totalDamage = 175f + (125 * spell.CastInfo.SpellLevel - 1) + ap;
        foreach (var enemy in enemiesInRange) {
            switch (_annie.SkinID) {
                case 5:  AddParticleTarget(_annie, enemy, "infernalguardian_tar_frost", enemy); break;
                default: AddParticleTarget(_annie, enemy, "InfernalGuardian_tar",       enemy); break;
            }

            enemy.TakeDamage(spell.CastInfo.Owner, totalDamage, DamageType.DAMAGE_TYPE_MAGICAL,
                             DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            if (!_shouldStun) continue;
            var stunDuration = _annie.Stats.Level switch {
                < 6  => 1.25f,
                < 11 => 1.5f,
                _    => 1.75f
            };
            AddBuff("Stun", stunDuration, 1, spell, enemy, _annie);
        }
    }
}

public class InfernalGuardianGuide : BasePetController { }