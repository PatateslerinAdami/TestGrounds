using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

// ============================================================================
// HAMMER W — JayceStaticField: AoE aura + mana per hit
// ============================================================================
public class JayceStaticField : ISpellScript
{
    private ObjAIBase _owner;
    private Particle  _particle;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell)
    {
        RemoveParticle(_particle);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell)
    {
        int rank = spell.CastInfo.SpellLevel;
        float[] tick = { 0, 25, 40, 55, 70, 85 };
        float ap = _owner.Stats.AbilityPower.Total * 0.25f;
        float tickDmg = tick[rank] + ap;

        _particle = AddParticleTarget(_owner, _owner, "Jayce_Base_W_StaticField.troy", _owner, lifetime: 4f);

        // AoE tick damage around Jayce for 4 seconds
        // Simplified: single tick at cast, full implementation would need timer
        var enemies = GetUnitsInRange(_owner, _owner.Position, 350f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        foreach (var u in enemies)
            u.TakeDamage(_owner, tickDmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);

        // Mana restore per auto-attack handled by passive — deferred
    }
    public void OnUpdate(float diff) { }
}

// ============================================================================
// CANNON W — JayceHyperCharge: AS steroid + 3 rapid attacks
// ============================================================================
public class JayceHyperCharge : ISpellScript
{
    private ObjAIBase _owner;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) { }
    public void OnSpellCast(Spell spell)
    {
        int rank = spell.CastInfo.SpellLevel;
        float maxAS = rank switch { 1 => 1.3f, 2 => 1.5f, 3 => 1.7f, 4 => 1.9f, 5 => 2.1f, _ => 1.3f };

        AddParticleTarget(_owner, _owner, "Jayce_Base_W_HyperCharge_Cas.troy", _owner, 1f);
        // Max out attack speed for 3 attacks — simplified to a flat buff
        AddBuff("JayceHyperCharge", 4f, 1, spell, _owner, _owner);
    }
    public void OnSpellPostCast(Spell spell) { }
    public void OnUpdate(float diff) { }
}
