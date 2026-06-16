using System;
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

/// <summary>
/// Poppy E — Heroic Charge.
///
/// Pre-calculates the full push destination, then dashes Poppy AND pushes
/// the target to the SAME point simultaneously. Wall collision is detected
/// by the engine's FIRST_WALL_HIT (no custom pre-scan — that would prevent
/// OnCollisionTerrain from firing).
/// </summary>
public class PoppyHeroicCharge : ISpellScript {
    private const float ChargeSpeed = 2000f;
    private const float PushDistance = 400f;
    private const float WallStunDuration = 1.5f;

    private ObjAIBase _poppy;
    private AttackableUnit _target;
    private Spell _spell;
    private Vector2 _chargeDirection;

    private bool _wallSlamPending;
    private bool _wallSlamApplied;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        NotSingleTargetSpell = false,
        CastingBreaksStealth = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _poppy = owner;
        _spell = spell;
    }

    public void OnDeactivate(ObjAIBase owner, Spell spell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
        _wallSlamPending = false;
        _wallSlamApplied = false;

        if (target == null || target.IsDead) return;

        _chargeDirection = Vector2.Normalize(target.Position - owner.Position);
    }

    public void OnSpellPostCast(Spell spell) {
        if (_target == null) return;

        var owner = spell.CastInfo.Owner;

        // --- Dead target: Poppy flies past ---
        if (_target.IsDead) {
            ForceMovement(owner, "Spell1",
                owner.Position + _chargeDirection * PushDistance,
                ChargeSpeed, 0f, 0f, 0f);
            return;
        }

        // --- Initial contact damage ---
        float rank = spell.CastInfo.SpellLevel;
        float ap = owner.Stats.AbilityPower.Total * 0.4f;
        float initialDamage = rank switch {
            1 => 50f, 2 => 75f, 3 => 100f, 4 => 125f, 5 => 150f, _ => 50f
        } + ap;

        _target.TakeDamage(owner, initialDamage, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_SPELL, false);

        AddParticleTarget(owner, _target, "HeroicCharge_tar.troy", _target, 0.5f);

        // Target died from initial damage → Poppy flies past
        if (_target.IsDead) {
            ForceMovement(owner, "Spell1",
                owner.Position + _chargeDirection * PushDistance,
                ChargeSpeed, 0f, 0f, 0f);
            return;
        }

        // Destination: full push distance from the target
        var finalDestination = new Vector2(
            _target.Position.X + _chargeDirection.X * PushDistance,
            _target.Position.Y + _chargeDirection.Y * PushDistance
        );

        // Push the target (engine's FIRST_WALL_HIT handles wall clamping + OnCollisionTerrain)
        CancelDash(_target);
        ApiEventManager.OnCollisionTerrain.AddListener(this, _target, OnTargetCollisionTerrain, true);
        ApiEventManager.OnMoveSuccess.AddListener(this, _target, OnTargetMoveSuccess, true);
        ApiEventManager.OnMoveFailure.AddListener(this, _target, OnTargetMoveFailure, true);

        ForceMovement(_target, "RUN", finalDestination, ChargeSpeed, 0f, 0f, 0f,
            movementType: ForceMovementType.FIRST_WALL_HIT,
            movementOrdersType: ForceMovementOrdersType.CANCEL_ORDER,
            movementOrdersFacing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING);

        // Poppy charges to the SAME destination
        ForceMovement(owner, "Spell1", finalDestination, ChargeSpeed, 0f, 0f, 0f,
            movementType: ForceMovementType.FIRST_WALL_HIT,
            movementOrdersType: ForceMovementOrdersType.CANCEL_ORDER);
    }

    // ── Wall-slam handlers (Vayne E Condemn pattern) ──

    private void OnTargetCollisionTerrain(GameObject unitObj) {
        if (unitObj is AttackableUnit) {
            _wallSlamPending = true;
        }
    }

    private void OnTargetMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters) {
        if (!_wallSlamPending || _wallSlamApplied || unit.IsDead) return;
        _wallSlamApplied = true;

        float rank = _spell.CastInfo.SpellLevel;
        float ap = _poppy.Stats.AbilityPower.Total * 0.4f;
        float wallDamage = rank switch {
            1 => 75f, 2 => 125f, 3 => 175f, 4 => 225f, 5 => 275f, _ => 75f
        } + ap;

        unit.TakeDamage(_poppy, wallDamage, DamageType.DAMAGE_TYPE_MAGICAL,
            DamageSource.DAMAGE_SOURCE_SPELL, false);

        AddParticleTarget(_poppy, unit, "HeroicCharge_tar2.troy", unit, 0.5f);
        AddBuff("Stun", WallStunDuration, 1, _spell, unit, _poppy);
    }

    private void OnTargetMoveFailure(AttackableUnit unit, ForceMovementParameters parameters) {
        _wallSlamPending = false;
    }
}
