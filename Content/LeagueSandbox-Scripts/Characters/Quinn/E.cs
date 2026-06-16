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
// HUMAN E — QuinnE: Dash to target + pushback + mark
// ============================================================================
public class QuinnE : ISpellScript
{
    private ObjAIBase       _owner;
    private AttackableUnit  _target;
    private Vector2         _startPos;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _owner = owner;
        ApiEventManager.OnMoveEnd.AddListener(this, _owner, OnMoveEnd);
    }
    public void OnDeactivate(ObjAIBase owner, Spell spell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _startPos = owner.Position;
    }
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell)
    {
        if (_target == null || _target.IsDead) return;

        int rank = spell.CastInfo.SpellLevel;
        float[] dmg = { 0, 40, 70, 100, 130, 160 };
        float bonusAd = _owner.Stats.AttackDamage.FlatBonus * 0.2f;

        _target.TakeDamage(_owner, dmg[rank] + bonusAd, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        // Dash toward target, then push back
        var dirToTarget = Vector2.Normalize(_target.Position - _startPos);
        var leapPoint = _target.Position - dirToTarget * 525f;
        _owner.DashToLocation(leapPoint, 1000f, "Spell3", 0f, false, false);

        AddParticleTarget(_owner, _target, "Quinn_Base_E_Tar.troy", _target, 1f);
    }

    public void OnMoveEnd(AttackableUnit unit, ForceMovementParameters forceMovementParameters)
    {
        // After dash completes, push back and mark
        if (_target == null || _target.IsDead) return;
        ForceMovement(_target, "RUN", _startPos, 500f, 0, 0, 0);

        // Mark target as Vulnerable (passive synergy)
        AddBuff("QuinnWPassive", 4f, 1, null, _target, _owner);
    }

    public void OnUpdate(float diff) { }
}

// ============================================================================
// VALOR E — QuinnValorE: Dash to target
// ============================================================================
public class QuinnValorE : ISpellScript
{
    private ObjAIBase      _owner;
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
    public void OnSpellCast(Spell spell) { }
    public void OnSpellPostCast(Spell spell)
    {
        if (_target == null || _target.IsDead) return;

        int rank = spell.CastInfo.SpellLevel;
        float[] dmg = { 0, 40, 70, 100, 130, 160 };
        float bonusAd = _owner.Stats.AttackDamage.FlatBonus * 0.2f;

        _target.TakeDamage(_owner, dmg[rank] + bonusAd, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        _owner.DashToLocation(_target.Position, 2000f, "Spell3", 0f, false, false);
        AddParticleTarget(_owner, _target, "Quinn_Base_E_Tar.troy", _target, 1f);
    }
    public void OnUpdate(float diff) { }
}
