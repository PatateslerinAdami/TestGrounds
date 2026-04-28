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

public class Incinerate : ISpellScript {
    private ObjAIBase _annie;
    private Vector2   _direction;
    private bool      _shouldStun = false;
    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        IsDamagingSpell      = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _annie = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _shouldStun = false;
    }

    public void OnSpellCast(Spell spell) {
        _direction = new Vector2(_annie.Direction.X, _annie.Direction.Z);
    }

    public void OnSpellPostCast(Spell spell) {
        //AddParticleTarget(_annie, _annie, "Incinerate_cas", _annie);
        
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
        
        var enemiesInRange = GetUnitsInCone(_annie, _annie.Position, _direction, 650f, 85f, true,
                                                 SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                                 SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        
        foreach (var enemy in enemiesInRange) {
            
            switch (_annie.SkinID) {
                case 5:  AddParticleTarget(_annie, enemy, "Annie_skin05_W_buf", enemy); break;
                default: AddParticleTarget(_annie, enemy, "Annie_W_buf",       enemy);break; 
            }
            var ap     = _annie.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
            var damage = 70 + 45f * (spell.CastInfo.SpellLevel - 1) + ap;
            enemy.TakeDamage(_annie, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
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