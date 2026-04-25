using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using System;
using System.Linq;
using LeagueSandbox.GameServer.API;

namespace Buffs;

internal class ZedUltDash : IBuffGameScript {
    private const float DashStartupDelayMs      = 600f;
    private const float DashSpeed               = 1000f;
    private const float ZedDashForwardDistance  = 200f;
    private const float TargetTrackBreakDistance = 2200f;
    private const short MaxStepCount = 1;

    private ObjAIBase      _zed;
    private AttackableUnit _target;
    private Spell          _spell;
    private ZedRHandler    _zedRShadowHandler;
    private Buff           _buff;
    private float          _timer     = 0f;
    private short          _stepCount = 0;
    private Vector2        _castPosition;
    private Vector2        _trackedTargetPosition;
    private bool           _cancelledBeforeDash;
    private bool           _cancelledByOwnerDeath;
    private bool           _dashStarted;
    private bool           _dashResolved;
    private bool           _dashInterrupted;
    private bool           _targetBecameZombieBeforeDash;
    private bool           _forcedDashToTrackedPosition;
    private bool           _ignoreNextMoveFailure;
    private bool           _postTargetDashStarted;
    private bool           _pendingPostTargetDash;
    private Vector2        _pendingPostTargetDashEndPoint;
    private bool           _shadowPostDashQueued;
    private bool           _shadowPostDashStarted;
    private Minion         _ultMinion1;
    private Minion         _ultMinion2;
    private Minion         _ultMinion3;
    private Vector2        _ultMinion1DashCastPosition;
    private Vector2        _ultMinion2DashCastPosition;
    private Vector2        _ultMinion3DashCastPosition;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _zed                        = ownerSpell.CastInfo.Owner;
        _target                     = unit;
        _spell                      = ownerSpell;
        _buff                       = buff;
        _castPosition               = _zed.Position;
        _trackedTargetPosition      = _target?.Position ?? Vector2.Zero;
        _cancelledBeforeDash        = false;
        _cancelledByOwnerDeath      = false;
        _dashStarted                = false;
        _dashResolved               = false;
        _dashInterrupted            = false;
        _targetBecameZombieBeforeDash = false;
        _forcedDashToTrackedPosition = false;
        _ignoreNextMoveFailure       = false;
        _postTargetDashStarted       = false;
        _pendingPostTargetDash       = false;
        _pendingPostTargetDashEndPoint = Vector2.Zero;
        _shadowPostDashQueued          = false;
        _shadowPostDashStarted         = false;
        _ultMinion1                    = null;
        _ultMinion2                    = null;
        _ultMinion3                    = null;
        _ultMinion1DashCastPosition    = Vector2.Zero;
        _ultMinion2DashCastPosition    = Vector2.Zero;
        _ultMinion3DashCastPosition    = Vector2.Zero;

        AddParticleTarget(_zed, _target, "Zed_Ult_TargetMarker_tar", _target);
        PlayAnimation(_zed, "Spell4");
        AddParticleTarget(_zed, _zed,    "Zed_UltSink",              _zed);
        var rHandlerBuff = AddBuff("ZedRHandler", 6.5f, 1, _spell, _zed, _zed, false);
        _zedRShadowHandler = rHandlerBuff?.BuffScript as ZedRHandler;
        _zedRShadowHandler?.SetSpellUltShadowSwap();
        _zedRShadowHandler?.SpawnShadow(_zed.Position, _target.Position);
        HideHealthBar(_zed, -1, true);
        _zed.UpdateMoveOrder(OrderType.Stop);
        _zed.SetStatus(StatusFlags.Rooted,     true);
        _zed.SetStatus(StatusFlags.Ghosted,    true);
        _zed.SetStatus(StatusFlags.Targetable, false);
        _zed.SetStatus(StatusFlags.CanAttack, false);
        _zed.SetStatus(StatusFlags.CanCast,   false);
        SetAllCastSlotsSealed(true);

        ApiEventManager.OnDeath.AddListener(this, _zed, OnOwnerDeath, true);
        ApiEventManager.OnDeath.AddListener(this, _target, OnTargetDeath);
        ApiEventManager.OnResurrect.AddListener(this, _zed, OnOwnerResurrect);
    }

    public void OnUpdate(float diff) {
        _timer += diff;
        SetAllCastSlotsSealed(true);
        if (_dashStarted && !_dashResolved && !_forcedDashToTrackedPosition && _target is { IsDead: false }) {
            if (Vector2.Distance(_zed.Position, _target.Position) >= TargetTrackBreakDistance) {
                ForceDashToTrackedPosition();
            } else {
                _trackedTargetPosition = _target.Position;
            }
        }

        if (_pendingPostTargetDash && !_dashResolved && !_cancelledByOwnerDeath && _zed.MovementParameters == null) {
            _pendingPostTargetDash = false;
            _postTargetDashStarted = true;
            StartShadowPostTargetDash();
            _zed.DashToLocation(_pendingPostTargetDashEndPoint, DashSpeed, "zed_spell4_strike");
        }

        if (_stepCount >= MaxStepCount || _dashResolved) return;

        if (ShouldCancelBeforeDash()) {
            CancelBeforeDash();
            return;
        }

        if (!(_timer >= DashStartupDelayMs)) return;
        _dashStarted = true;
        _zed.SetStatus(StatusFlags.NoRender, true);
        foreach (var knockup in _zed.GetBuffs().Where(buff => buff.BuffType is BuffType.KNOCKBACK or BuffType.KNOCKUP)) {
            RemoveBuff(knockup); // Airborne effects are cleared immediately on dash start.
        }

        var shadowAnimationBuff =
            AddBuff("ZedDashCloneMaker", 1f, 1, _spell, _zed, _zed).BuffScript as ZedDashCloneMaker;

        var ultMinion1 = AddMinion(_zed, "ZedShadow", "ZedRShadow", GetPositionByOffset(60f, 600f), _zed.Team, _zed.SkinID,
                                   true, false);
        _ultMinion1 = ultMinion1;
        _ultMinion1DashCastPosition = ultMinion1.Position;
        ultMinion1.SetStatus(StatusFlags.CanAttack, false);
        shadowAnimationBuff.AddShadow1(ultMinion1);
        FaceDirection(_target.Position, ultMinion1, true);
        ultMinion1.DashToTarget(_target, DashSpeed, "zed_spell4_strike", followTargetMaxDistance: TargetTrackBreakDistance);
        AddParticleTarget(_zed, ultMinion1, "Zed_R_Dash", ultMinion1);

        var ultMinion2 = AddMinion(_zed, "ZedShadow", "ZedRShadow", GetPositionByOffset(-60f, 600f), _zed.Team, _zed.SkinID,
                                   true, false);
        _ultMinion2 = ultMinion2;
        _ultMinion2DashCastPosition = ultMinion2.Position;
        ultMinion2.SetStatus(StatusFlags.CanAttack, false);
        shadowAnimationBuff.AddShadow2(ultMinion2);
        FaceDirection(_target.Position, ultMinion2, true);
        ultMinion2.DashToTarget(_target, DashSpeed, "zed_spell4_strike", followTargetMaxDistance: TargetTrackBreakDistance);
        AddParticleTarget(_zed, ultMinion2, "Zed_R_Dash", ultMinion2);

        var ultMinion3 = AddMinion(_zed, "ZedShadow", "ZedRShadow", GetPositionByOffset(-180f, 600f), _zed.Team, _zed.SkinID,
                                   true, false);
        _ultMinion3 = ultMinion3;
        _ultMinion3DashCastPosition = ultMinion3.Position;
        ultMinion3.SetStatus(StatusFlags.CanAttack, false);
        shadowAnimationBuff.AddShadow3(ultMinion3);
        FaceDirection(_target.Position, ultMinion3, true);
        ultMinion3.DashToTarget(_target, DashSpeed, "zed_spell4_strike", followTargetMaxDistance: TargetTrackBreakDistance);
        AddParticleTarget(_zed, ultMinion3, "Zed_R_Dash", ultMinion3);

        _zed.DashToTarget(_target, DashSpeed, "zed_spell4_strike", followTargetMaxDistance: TargetTrackBreakDistance);
        ApiEventManager.OnMoveSuccess.AddListener(this, _zed, OnMoveSuccess);
        ApiEventManager.OnMoveFailure.AddListener(this, _zed, OnMoveFailure);
        _stepCount++;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveBuff(_zed, "ZedDashCloneMaker");
        _zed.SetStatus(StatusFlags.NoRender, false);
        HideHealthBar(_zed, -1, false);
        _zed.SetStatus(StatusFlags.Rooted, false);
        if (!_zed.IsDead) {
            _zed.SetStatus(StatusFlags.Targetable, true);
            _zed.SetStatus(StatusFlags.CanAttack, true);
            _zed.SetStatus(StatusFlags.CanCast,   true);
        }
        SetAllCastSlotsSealed(false);

        if (_cancelledByOwnerDeath) {
            ApiEventManager.RemoveAllListenersForOwner(this);
            return;
        }

        if (_cancelledBeforeDash) {
            _zed.SetStatus(StatusFlags.Ghosted, false);
            TeleportTo(_zed, _castPosition.X, _castPosition.Y);
            RemoveRShadow();
        } else {
            if (_target == null) {
                _zed.SetStatus(StatusFlags.Ghosted, false);
                ApiEventManager.RemoveAllListenersForOwner(this);
                return;
            }

            AddParticleTarget(_zed, _target, "Zed_Ult_DashEnd", _target);
            FaceDirection(_target.Position, _zed, true);

            var applyMark = _dashInterrupted || CanApplyMarkAtDashEnd();
            if (applyMark) {
                var executeBuff = AddBuff("ZedUltExecute", 3f, 1, _spell, _target, _zed);
                if (executeBuff == null) _zed.SetStatus(StatusFlags.Ghosted, false);
            } else {
                _zed.SetStatus(StatusFlags.Ghosted, false);
            }

            OrderBasicAttackAfterReappear();
        }

        ApiEventManager.RemoveAllListenersForOwner(this);
    }

    private void OnMoveSuccess(AttackableUnit unit, ForceMovementParameters parameters) {
        if (unit != _zed || _dashResolved || _cancelledByOwnerDeath) return;

        if (!_forcedDashToTrackedPosition && !_postTargetDashStarted && !_pendingPostTargetDash &&
            _target is { IsDead: false }) {
            var finalDashEndPoint = GetFinalDashEndPoint(_zed, _castPosition);
            if (Vector2.DistanceSquared(finalDashEndPoint, _zed.Position) > 1f) {
                _pendingPostTargetDash         = true;
                _pendingPostTargetDashEndPoint = finalDashEndPoint;
                _shadowPostDashQueued          = true;
                return;
            }
        }

        _dashResolved = true;
        _buff.DeactivateBuff();
    }

    private void OnMoveFailure(AttackableUnit unit, ForceMovementParameters parameters) {
        if (unit != _zed || _dashResolved || _cancelledByOwnerDeath) return;

        if (_ignoreNextMoveFailure) {
            _ignoreNextMoveFailure = false;
            return;
        }

        InterruptDash();
    }

    private void OnOwnerDeath(DeathData data) {
        if (_dashResolved || _cancelledByOwnerDeath) return;

        _cancelledByOwnerDeath = true;
        _dashResolved          = true;

        if (_zed.MovementParameters != null) {
            //_zed.SetForcedMovementState(false, MoveStopReason.Death);
        }

        RemoveRShadow();
        RestoreUltimateNoRefund();
        _buff.DeactivateBuff();
    }

    private void OnTargetDeath(DeathData data) {
        if (_dashStarted || _dashResolved) return;

        if (data.BecomeZombie) {
            _targetBecameZombieBeforeDash = true;
            return;
        }

        CancelBeforeDash();
    }

    private void OnOwnerResurrect(ObjAIBase unit) {
        if (unit != _zed || !_dashStarted || _dashResolved || _cancelledByOwnerDeath) return;
        InterruptDash();
    }

    private void InterruptDash() {
        if (_dashResolved || _cancelledByOwnerDeath) return;

        _dashInterrupted = true;
        _dashResolved    = true;

        if (_zed.MovementParameters != null) {
            //_zed.SetForcedMovementState(false, MoveStopReason.HeroReincarnate);
        }

        _buff.DeactivateBuff();
    }

    private bool ShouldCancelBeforeDash() {
        if (_target == null) return true;
        if (_target.IsDead && !_targetBecameZombieBeforeDash) return true;
        return false;
    }

    private void CancelBeforeDash() {
        if (_cancelledBeforeDash || _dashStarted || _dashResolved) return;
        _cancelledBeforeDash = true;
        _dashResolved        = true;

        if (_zedRShadowHandler != null) {
            _zedRShadowHandler.CancelBeforeDash();
        } else {
            SetSpell(_zed, "ZedUlt", SpellSlotType.SpellSlots, 3);
            var zedUlt = _zed.GetSpell("ZedUlt");
            if (zedUlt != null) {
                zedUlt.SetLevel(_spell.CastInfo.SpellLevel);
                zedUlt.SetSpellToggle(false);
                zedUlt.SetCooldown(0.5f, true);
            }
        }

        _buff.DeactivateBuff();
    }

    private void ForceDashToTrackedPosition() {
        if (_forcedDashToTrackedPosition || _dashResolved || !_dashStarted) return;
        _forcedDashToTrackedPosition = true;

        var tracked = _trackedTargetPosition;
        if (tracked == Vector2.Zero) tracked = _target?.Position ?? _zed.Position;

        var direction = tracked - _zed.Position;
        if (direction == Vector2.Zero) direction = new Vector2(_zed.Direction.X, _zed.Direction.Z);
        if (direction == Vector2.Zero) direction = Vector2.UnitX;
        direction = Vector2.Normalize(direction);

        var endPoint = tracked + direction * (_zed.PathfindingRadius + (_target?.PathfindingRadius ?? 0f) +
                                              ZedDashForwardDistance);

        _ignoreNextMoveFailure = true;
        if (_zed.MovementParameters != null) {
            //_zed.SetForcedMovementState(false, MoveStopReason.ForceMovement);
        }
        _zed.DashToLocation(endPoint, DashSpeed, "zed_spell4_strike");
    }

    private void StartShadowPostTargetDash() {
        if (!_shadowPostDashQueued || _shadowPostDashStarted) return;
        _shadowPostDashQueued  = false;
        _shadowPostDashStarted = true;

        StartShadowPostTargetDash(_ultMinion1, _ultMinion1DashCastPosition);
        StartShadowPostTargetDash(_ultMinion2, _ultMinion2DashCastPosition);
        StartShadowPostTargetDash(_ultMinion3, _ultMinion3DashCastPosition);
    }

    private void StartShadowPostTargetDash(Minion shadow, Vector2 dashCastPosition) {
        if (shadow == null || shadow.IsDead) return;

        if (shadow.MovementParameters != null) {
            //shadow.SetForcedMovementState(false, MoveStopReason.ForceMovement);
        }

        var finalDashEndPoint = GetFinalDashEndPoint(shadow, dashCastPosition);
        var dashDistance      = Vector2.Distance(finalDashEndPoint, shadow.Position);

        if (dashDistance <= 1f) {
            HideAndExpireShadow(shadow);
            return;
        }

        shadow.DashToLocation(finalDashEndPoint, DashSpeed, "zed_spell4_strike");
        ScheduleShadowCleanup(shadow, dashDistance / DashSpeed);
    }

    private void ScheduleShadowCleanup(Minion shadow, float dashTravelTimeSeconds) {
        if (shadow == null || shadow.IsDead) return;

        var cleanupDelay = Math.Max(0.02f, dashTravelTimeSeconds);
        CreateTimer(cleanupDelay, () => {
            if (shadow == null || shadow.IsDead) return;
            HideAndExpireShadow(shadow);
        });
    }

    private void HideAndExpireShadow(Minion shadow) {
        if (shadow == null || shadow.IsDead) return;
        shadow.SetStatus(StatusFlags.NoRender, true);
        AddBuff("ExpirationTimer", 0.05f, 1, _spell, shadow, shadow);
    }

    private Vector2 GetFinalDashEndPoint(AttackableUnit dashingUnit, Vector2 dashCastPosition) {
        dashingUnit ??= _zed;
        var targetPos = _target?.Position ?? _trackedTargetPosition;
        if (targetPos == Vector2.Zero) targetPos = dashingUnit.Position;

        if (dashCastPosition == Vector2.Zero) dashCastPosition = _castPosition;

        var direction = targetPos - dashCastPosition;
        if (direction.LengthSquared() < 0.0001f) direction = targetPos - dashingUnit.Position;
        if (direction.LengthSquared() < 0.0001f) direction = new Vector2(_zed.Direction.X, _zed.Direction.Z);
        if (direction.LengthSquared() < 0.0001f) direction = Vector2.UnitX;
        direction = Vector2.Normalize(direction);

        var offset = dashingUnit.PathfindingRadius + (_target?.PathfindingRadius ?? 0f) + ZedDashForwardDistance;
        return targetPos + direction * offset;
    }

    private bool CanApplyMarkAtDashEnd() {
        if (_target == null || _target.IsDead) return false;
        if (!_target.Status.HasFlag(StatusFlags.Targetable)) return false;
        return !TryConsumeSpellShield(_target);
    }

    private static bool TryConsumeSpellShield(AttackableUnit unit) {
        var shieldBuff = unit.GetBuffs().FirstOrDefault(buff => buff.BuffType == BuffType.SPELL_IMMUNITY);
        if (shieldBuff == null) return false;
        RemoveBuff(shieldBuff);
        return true;
    }

    private void OrderBasicAttackAfterReappear() {
        if (_zed.IsDead || _target == null || _target.IsDead) return;
        if (!_target.Status.HasFlag(StatusFlags.Targetable)) return;

        _zed.SetTargetUnit(_target, true);
        _zed.UpdateMoveOrder(OrderType.AttackTo);
    }

    private void RemoveRShadow() {
        var shadowTrackerBuff = _zed.GetBuffWithName("ZedShadowHandler");
        var zedShadowHandler  = shadowTrackerBuff?.BuffScript as ZedShadowHandler;
        zedShadowHandler?.RemoveRShadow();
    }

    private void RestoreUltimateNoRefund() {
        SetSpell(_zed, "ZedUlt", SpellSlotType.SpellSlots, 3);
        var zedUlt = _zed.GetSpell("ZedUlt");
        if (zedUlt != null) {
            zedUlt.SetLevel(_spell.CastInfo.SpellLevel);
            zedUlt.SetSpellToggle(false);
            zedUlt.SetCooldown(zedUlt.GetCooldown(), true);
        }

        var rHandlerBuff = _zed.GetBuffWithName("ZedRHandler");
        rHandlerBuff?.DeactivateBuff();
    }

    private Vector2 GetPositionByOffset(float angleOffset, float distanceOffset) {
        var angle    = angleOffset * Math.PI / 180.0f;
        var dX       = _target.Position.X - _zed.Position.X;
        var dY       = _target.Position.Y - _zed.Position.Y;
        var distance = (float) Math.Sqrt(dX * dX + dY * dY);
        var offset   = distanceOffset;
        dX /= distance;
        dY /= distance;
        var rotX         = dX * Math.Cos(angle) - dY   * Math.Sin(angle);
        var rotY         = dX * Math.Sin(angle) + dY   * Math.Cos(angle);
        var newX         = _target.Position.X   + rotX * offset;
        var newY         = _target.Position.Y   + rotY * offset;
        var targetVector = new Vector2((float) newX, (float) newY);
        return targetVector;
    }

    private void SetAllCastSlotsSealed(bool sealedState) {
        for (var i = 0; i < 4; i++) {
            SealSpellSlot(_zed, SpellSlotType.SpellSlots, i, SpellbookType.SPELLBOOK_CHAMPION, sealedState);
        }

        SealSpellSlot(_zed, SpellSlotType.SummonerSpellSlots, 0, SpellbookType.SPELLBOOK_SUMMONER, sealedState);
        SealSpellSlot(_zed, SpellSlotType.SummonerSpellSlots, 1, SpellbookType.SPELLBOOK_SUMMONER, sealedState);

        for (var i = 0; i <= 6; i++) {
            SealSpellSlot(_zed, SpellSlotType.InventorySlots, i, SpellbookType.SPELLBOOK_CHAMPION, sealedState);
        }

        SealSpellSlot(_zed, SpellSlotType.BluePillSlot, (int)SpellSlotType.BluePillSlot,
                      SpellbookType.SPELLBOOK_CHAMPION, sealedState);
    }
}
