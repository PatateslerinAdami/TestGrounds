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
// HAMMER E — JayceThunderingBlow: Targeted knockback + %maxHP magic damage
// ============================================================================
public class JayceThunderingBlow : ISpellScript
{
    private ObjAIBase _owner;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
    }
    public void OnSpellCast(Spell spell)
    {
        AddParticleTarget(_owner, _owner, "Jayce_Base_E_ThunderingBlow_Cas.troy", _owner, 1f);
    }
    public void OnSpellPostCast(Spell spell)
    {
        if (_target == null || _target.IsDead) return;

        int rank = spell.CastInfo.SpellLevel;
        float[] pct  = { 0, 0.08f, 0.11f, 0.14f, 0.17f, 0.20f };
        float[] flat = { 0, 20, 60, 100, 140, 180 };
        float ad  = _owner.Stats.AttackDamage.FlatBonus * 1.0f;
        float maxHpDmg = _target.Stats.HealthPoints.Total * pct[rank];
        float total = maxHpDmg + flat[rank] + ad;

        _target.TakeDamage(_owner, total, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        AddParticleTarget(_owner, _target, "Jayce_Base_E_ThunderingBlow_Tar.troy", _target, 1f);

        // Knockback
        ForceMovement(_target, "RUN", _owner.Position, 500f, 0, 0, 0);
    }
    public void OnUpdate(float diff) { }
}

// ============================================================================
// CANNON E — JayceAccelerationGate: Ground gate + speed boost + Q empower
// ============================================================================
public class JayceAccelerationGate : ISpellScript
{
    private ObjAIBase _owner;
    private Vector2   _targetPos;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = false,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) { _owner = owner; }
    public void OnDeactivate(ObjAIBase owner, Spell spell) { }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _targetPos = new Vector2(end.X, end.Y);
    }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell)
    {
        // Ground gate particle
        var gatePos = _owner.Position + Vector2.Normalize(_targetPos - _owner.Position) * 100f;
        AddParticle(_owner, null, "Jayce_Base_E_AccelGate_Gate.troy", gatePos, 4f);

        // Speed boost allies passing through — simplified: buff self
        AddBuff("JayceAccelGateSpeed", 3f, 1, spell, _owner, _owner);
    }
    public void OnUpdate(float diff) { }
}
