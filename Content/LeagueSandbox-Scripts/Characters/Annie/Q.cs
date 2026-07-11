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

public class Disintegrate : ISpellScript
{
    private ObjAIBase _annie;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _annie = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var wasAliveBeforeHit = !target.IsDead;
        switch (_annie.SkinID)
        {
            case 5:
                AddParticleTarget(_annie, target, "Annie_skin05_Q_tar.troy", target);
                break;
            default:
                SpellEffectCreate("Annie_Q_tar_02.troy", _annie, target, target, scale: 1f,
                    flags: FXFlags.SimulateWhileOffScreen, keywordObject: _annie, fowVisibilityRadius: 10f);
                SpellEffectCreate("Annie_Q_tar.troy", _annie, target, target, scale: 1f,
                    flags: FXFlags.SimulateWhileOffScreen, keywordObject: _annie, fowVisibilityRadius: 10f);
                break;
        }

        if (_annie.HasBuff("Pyromania_particle"))
        {
            var stunDuration = _annie.Stats.Level switch
            {
                < 6 => 1.25f,
                < 11 => 1.5f,
                _ => 1.75f
            };
            AddBuff("Stun", stunDuration, 1, spell, target, _annie);
            RemoveBuff(_annie, "Pyromania_particle");
        }
        else
        {
            AddBuff("Pyromania", 25000f, 1, spell, _annie, _annie, true);
        }

        var ap = _annie.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var damage = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
        target.TakeDamage(_annie, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        if (!wasAliveBeforeHit || !target.IsDead) return;
        _annie.IncreasePAR(_annie, spell.CastInfo.ManaCost);
        spell.LowerCooldown(spell.CastInfo.Cooldown * 0.5f);
    }
}