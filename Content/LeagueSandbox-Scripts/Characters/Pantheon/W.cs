using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class PantheonW : ISpellScript
{
    private const float MaxLeapRange = 600.0f;
    private const float CloseRangeThreshold = 75.0f;
    private const float LandingBuffer = 125.0f;

    private ObjAIBase _pantheon;
    private Spell _spell;
    private AttackableUnit _target;
    private bool _leapInProgress;

    public SpellScriptMetadata ScriptMetadata { get; } = new()
    {
        TriggersSpellCasts = true,
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _spell = spell;
        _pantheon = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate);
        ApiEventManager.OnMoveSuccess.AddListener(this, owner, OnMoveEnd);
        ApiEventManager.OnMoveFailure.AddListener(this, owner, OnMoveFailure);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
        _leapInProgress = false;
    }

    public void OnSpellPostCast(Spell spell)
    {
        if (_target is not { IsDead: false })
        {
            _target = null;
            return;
        }

        var current = _pantheon.Position;
        var targetPos = _target.Position;
        var dist = Math.Min(Vector2.Distance(current, targetPos), MaxLeapRange);
        var (gravityVar, speedVar) = GetLeapParameters(dist);

        var landingDistance = dist;
        if (dist >= CloseRangeThreshold)
            landingDistance = Math.Max(0.0f, dist - _target.CharData.GameplayCollisionRadius - LandingBuffer);

        var animFactor = dist >= CloseRangeThreshold ? landingDistance / 750f : dist / 650f;
        animFactor = Math.Max(animFactor, 0.5f);
        animFactor = Math.Min(animFactor, 1.25f);

        FaceDirection(targetPos, _pantheon, true);
        PlayAnimation(_pantheon, "Spell2", animFactor);

        _leapInProgress = true;
        ForceMovement(_pantheon, _target, null, speedVar, 0f, gravityVar, 0f, -1f, true,
            ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersType.CANCEL_ORDER);
    }

    private void OnMoveEnd(AttackableUnit owner, ForceMovementParameters parameters)
    {
        if (owner != _pantheon || !_leapInProgress || _target == null) return;

        _leapInProgress = false;
        if (_target.IsDead || !_target.Status.HasFlag(StatusFlags.Targetable))
        {
            _target = null;
            return;
        }

        var ap = owner.Stats.AbilityPower.Total;
        var dmg = 60 + 25 * (_spell.CastInfo.SpellLevel - 1) + ap;

        AddBuff("Pantheon_AegisShield2", 15000f, 1, _spell, _pantheon, _pantheon, true);

        AddBuff("Stun", 1f, 1, _spell, _target, owner as ObjAIBase);
        AddParticleTarget(owner, _target, "Pantheon_Base_W_tar", _target, bone: "root");
        _target.TakeDamage(owner, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        if (_target.Team != owner.Team && owner is ObjAIBase ai && !_target.IsDead)
        {
            ai.SetTargetUnit(_target, true);
            ai.UpdateMoveOrder(OrderType.AttackTo);
        }

        _target = null;
    }

    private void OnMoveFailure(AttackableUnit owner, ForceMovementParameters parameters)
    {
        if (owner != _pantheon) return;

        _leapInProgress = false;
        _target = null;
    }

    private void OnStatsUpdate(AttackableUnit unit, float diff)
    {
        var ap = _pantheon.Stats.AbilityPower.Total;
        SetSpellToolTipVar(_pantheon, 0, ap, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }

    private static (float Gravity, float Speed) GetLeapParameters(float dist)
    {
        return dist switch
        {
            >= 600f => (120f, 2300f),
            >= 500f => (140f, 2150f),
            >= 375f => (160f, 2000f),
            >= 275f => (200f, 1900f),
            >= 175f => (240f, 1800f),
            >= 75f => (300f, 1750f),
            _ => (600f, 1700f)
        };
    }
}