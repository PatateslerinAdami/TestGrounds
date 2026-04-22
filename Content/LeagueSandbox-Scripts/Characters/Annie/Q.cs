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

public class Disintegrate : ISpellScript {
    private ObjAIBase _annie;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _annie = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ap                = _annie.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var damage            = 80f + 35f * (spell.CastInfo.SpellLevel - 1) + ap;
        var wasAliveBeforeHit = !target.IsDead;
        target.TakeDamage(_annie, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        if (_annie.HasBuff("Pyromania_particle")) {
            var stunDuration = _annie.Stats.Level switch {
                < 6  => 1.25f,
                < 11 => 1.5f,
                _    => 1.75f
            };
            AddBuff("Stun", stunDuration, 1, spell, target, _annie);
            RemoveBuff(_annie, "Pyromania_particle");
        } else {
            AddBuff("Pyromania", 250000f, 1, spell, _annie, _annie, true);
            if (_annie.GetBuffsWithName("Pyromania").Count == 4) {
                RemoveBuff(_annie, "Pyromania");
                AddBuff("Pyromania_particle", 25000f, 1, spell, _annie, _annie, true);
            }
        }

        switch (_annie.SkinID) {
            case 5:
                AddParticleTarget(_annie, target, "Annie_skin05_Q_tar", target);
                //AddParticleTarget(_annie, target, "Disintegrate_hit_frost",    target);
                break;
            default:
                AddParticleTarget(_annie, target, "Annie_Q_tar", target);
                AddParticleTarget(_annie, target, "Annie_Q_tar_02",    target);
                break;
        }

        if (!wasAliveBeforeHit || !target.IsDead) return;
        _annie.IncreasePAR(_annie, spell.CastInfo.ManaCost);
        spell.LowerCooldown(spell.CastInfo.Cooldown * 0.5f);
    }
}