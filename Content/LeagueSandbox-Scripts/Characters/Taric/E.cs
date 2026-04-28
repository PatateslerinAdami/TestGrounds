using System;
using System.Numerics;
using CharScripts;
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

public class Dazzle : ISpellScript {
    private ObjAIBase _taric;
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        MissileParameters = new MissileParameters() {
            Type = MissileType.Target,
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _taric = spell.CastInfo.Owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, ApplyEffects);
    }

    public void OnSpellPostCast(Spell spell) {
    }
    
    private void ApplyEffects(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        const float fullDamageRange = 250f;

        var level = spell.CastInfo.SpellLevel;
        var stunDuration = 1.1f + 0.1f * level;
        var ap = _taric.Stats.AbilityPower.Total;
        var minDamage = 40f + 30f * (level - 1) + 0.2f * ap;
        var distance = Vector2.Distance(_taric.Position, target.Position);
        var maxRange = spell.GetCurrentCastRange();
        var damageMultiplier = distance <= fullDamageRange
            ? 2f
            : Math.Clamp(2f - (distance - fullDamageRange) / (maxRange - fullDamageRange), 1f, 2f);
        var damage = minDamage * damageMultiplier;

        
        target.TakeDamage(_taric, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        AddBuff("Stun",       stunDuration, 1, spell, target, _taric);
        AddParticleTarget(_taric, target, "Dazzle_tar", target);
        AddParticleTarget(_taric, target, "Taric_HammerFlare", target, lifetime: stunDuration);

        missile.SetToRemove();
    }
}
