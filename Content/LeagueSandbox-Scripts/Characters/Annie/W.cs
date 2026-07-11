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

public class Incinerate : ISpellScript {
    private ObjAIBase _annie;
    private bool      _shouldStun = false;
    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        IsDamagingSpell      = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _annie = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var particleName = (_annie.SkinID) switch
        {
            5 => "Annie_skin05_W_buf.troy",
            _ => "Annie_W_buf.troy"
        };
        SpellEffectCreate(particleName, _annie, target, target, scale: 1f, flags: FXFlags.SimulateWhileOffScreen, keywordObject: _annie, fowVisibilityRadius: 10f);
        var ap     = _annie.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var damage = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
        target.TakeDamage(_annie, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        if (!_shouldStun) return;
        var stunDuration = _annie.Stats.Level switch {
            < 6  => 1.25f,
            < 11 => 1.5f,
            _    => 1.75f
        };
        AddBuff("Stun", stunDuration, 1, spell, target, _annie);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _shouldStun = false;
    }

    public void OnSpellPostCast(Spell spell) {
        
        if (_annie.HasBuff("Pyromania_particle")) {
            _shouldStun = true;
            RemoveBuff(_annie, "Pyromania_particle");
        } else {
            AddBuff("Pyromania", 25000f, 1, spell, _annie, _annie, true);
        }
        
        // Cone + target flags from SpellData (Incinerate: 24.76° half, 625u, LockConeToPlayer=1,
        // AffectEnemies|Neutral|Minions|Heroes).
        var enemiesInRange = GetUnitsHitBySpell(spell);
        
        foreach (var enemy in enemiesInRange) {
            spell.ApplyEffects(enemy);
        }
    }
}