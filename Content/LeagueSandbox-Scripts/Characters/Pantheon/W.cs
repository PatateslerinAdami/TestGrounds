using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeaguePackets.Game.Common;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
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
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        
        AddBuff("Stun", 1f, 1, spell, target, _pantheon);
        AddBuff("PantheonPassiveShield", 25000f, 1, spell, _pantheon, _pantheon, true);
        AddParticleTarget(_pantheon, target, "Pantheon_Base_W_tar", target, flags: FXFlags.SimulateWhileOffScreen);
        var ap = _pantheon.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
        target.TakeDamage(_pantheon, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        // Pantheon_LeapBash.lua: BBIf(Target IS_TYPE_HERO) -> BBIssueOrder(WhomToOrder=Owner,
        // Order=AI_ATTACKTO, TargetOfOrder=victim) — after the hit Pantheon gets a real attack
        // order on the victim (same pipeline as a player right-click), so he immediately starts
        // basic-attacking out of the stun. Hero-gated like the lua.
        if (target.Team != _pantheon.Team && !target.IsDead && target is Champion)
        {
            IssueOrder(_pantheon, OrderType.AttackTo, target);
        }
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell)
    {
        if (_target.IsDead)
        {
            return;
        }

        var current = _pantheon.Position;
        var targetPos = GetMovePositionByCollisionOffset(_pantheon, _target);
        var dist = Math.Min(Vector2.Distance(current, targetPos), MaxLeapRange);
        var (gravityVar, speedVar) = GetLeapParameters(dist);

        var landingDistance = dist;
        if (dist >= CloseRangeThreshold)
            landingDistance = Math.Max(0.0f, dist - _target.CharData.GameplayCollisionRadius - LandingBuffer);

        var animFactor = dist >= CloseRangeThreshold ? landingDistance / 750f : dist / 650f;
        animFactor = Math.Max(animFactor, 0.5f);
        animFactor = Math.Min(animFactor, 1.25f);

        FaceDirection(targetPos, _pantheon, true);
        PlayAnimation(_pantheon, "Spell2", animFactor, 0, 1f, AnimationFlags.Lock | AnimationFlags.NoBlend);
        
        ApiEventManager.OnMoveSuccess.AddListener(this, _pantheon, OnMoveEnd);
        ForceMove(_pantheon, targetPos, speedVar, gravityVar, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION, true, true, ForceMovementOrdersType.CANCEL_ORDER, "PantheonW");
    }

    private void OnMoveEnd(AttackableUnit owner, ForceMovementParameters parameters)
    {
        if (parameters.MovementName != "PantheonW") return;
        StopAnimation(_pantheon, "Spell2", StopAnimationFlags.IgnoreLock);
        if (_target.IsDead)
        {
            return;
        }

        _spell.ApplyEffects(_target);
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