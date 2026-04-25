﻿using System;
 using System.Collections.Generic;
 using System.Numerics;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using Spells;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ZedWHandler : IBuffGameScript {
    private const float            ShadowMimicRange   = 2000f;
    private       ObjAIBase        _zed;
    private       Minion           _shadow;
    private       Spell            _zedShadowDash;
    private       Spell            _zedW2;
    private       Buff             _buff;
    private       Spell            _spell;
    private       Vector2          _end;
    private       ZedShadowHandler _zedShadowHandler;
    private       byte             _level;
    private       bool             _swapOnSpawn;
    private       bool             _currentShadowSwapped;
    private       bool             _shadowSpawnPending;
    private       bool             _slashQueued, _shurikenQueued;
    private       bool             _deferFullSlashOnSpawn;
    private       int              _slashQueuedCastId;
    private       bool             _shurikenSpawnReady;
    private       float            _shurikenCastTimer  = 0f;
    private       bool             _isZedW2;
    private       float            _cooldownToLower    = 0f;
    private const float            MaxShurikenCastTime = 250f;
    

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff          = buff;
        _zed           = ownerSpell.CastInfo.Owner;
        _spell         = ownerSpell;
        _zedShadowDash = _zed.GetSpell("ZedShadowDash");
        _level         = _zedShadowDash.CastInfo.SpellLevel;
        _shadowSpawnPending = true;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (!_currentShadowSwapped) SetSpellShadowDash();
    }
    
    public void OnUpdate(float diff) {
        if (_shurikenQueued) {
            _shurikenCastTimer += diff;
            TryCastQueuedShuriken();
        }

        if (_shadow != null && (_shadow.IsDead || _shadow.IsToRemove())) {
            _shadow = null;
        }

        UpdateWSeal();
    }

    private void UpdateWSeal() {
        var shouldSeal = IsUltDashActive() || ShouldSealSwap();
        SealSpellSlot(
            _zed,
            SpellSlotType.SpellSlots, 1,
            SpellbookType.SPELLBOOK_CHAMPION,
            shouldSeal
        );
    }
    
    private bool ShouldSealSwap() {
        if (!HasValidShadow()) return false;
        return Vector2.Distance(_shadow.Position, _zed.Position) >= 1100.0F;
    }

    public void QueueShuriken(Vector2 end) {
        if (!_shadowSpawnPending) return;

        _end            = end;
        _shurikenQueued = true;
        _shurikenSpawnReady = false;
        _shurikenCastTimer  = 0f;
    }

    public void QueueSlash(int castId = 0) {
        if (!_shadowSpawnPending) return;

        _slashQueuedCastId = castId > 0 ? castId : ZedPBAOEDummy.GetActiveSlashCastId(_zed);
        _slashQueued = true;
    }

    public bool ShouldDeferSlashUntilSpawnSwap() {
        return _shadowSpawnPending && _swapOnSpawn;
    }

    public void DeferFullSlashOnSpawn(int castId = 0) {
        _slashQueued            = true;
        _deferFullSlashOnSpawn = true;
        _slashQueuedCastId      = castId > 0 ? castId : ZedPBAOEDummy.GetActiveSlashCastId(_zed);
    }

    public void SpawnShadow(Vector2 position) {
        _currentShadowSwapped = false;
        var swappedOnSpawn = _swapOnSpawn;
        var direction   = (position - _zed.Position).Normalized();
        var facingPoint = position + direction * 200f;
        if (swappedOnSpawn) {
            var previousZedPosition = _zed.Position;
            TeleportTo(_zed, position.X, position.Y);
            position = previousZedPosition;
            _swapOnSpawn          = false;
            _currentShadowSwapped = true;
        }
        _shadow = AddMinion(_zed, "ZedShadow", "ZedWShadow", position, _zed.Team, _zed.SkinID, true, false, false,
                            SpellDataFlags.NonTargetableAll, null, true, true);
        _shadowSpawnPending = false;
        FaceDirection(facingPoint, _shadow, true);
        var shadowTrackerBuff = _zed.GetBuffWithName("ZedShadowHandler");
        if (shadowTrackerBuff == null) {
            shadowTrackerBuff = AddBuff("ZedShadowHandler", 10f, 1, _spell, _zed, _zed, true);
        }
        _zedShadowHandler  = shadowTrackerBuff?.BuffScript as ZedShadowHandler;
        _zedShadowHandler?.AddWShadow(_shadow);
        AddParticleTarget(_zed, null, "Zed_Base_W_tar.troy", _shadow, lifetime: 1f);
        if (swappedOnSpawn) {
            AddParticleTarget(_zed, null, "Zed_CloneSwap.troy", _shadow);
            AddParticleTarget(_zed, null, "Zed_CloneSwap.troy", _zed);
        }

        if (_shurikenQueued) {
            _shurikenSpawnReady = true;
            FaceDirection(_end, _shadow, true);
            PlayAnimation(_shadow, "Spell1");
            TryCastQueuedShuriken();
        }

        if (_slashQueued) {
            if (_deferFullSlashOnSpawn) {
                TryCastDeferredFullSlash();
            } else {
                TryCastQueuedShadowSlash();
            }
            _slashQueued            = false;
            _deferFullSlashOnSpawn = false;
            _slashQueuedCastId      = 0;
        }

        UpdateWSeal();
    }

    private void TryCastQueuedShuriken() {
        if (!_shurikenQueued || !_shurikenSpawnReady || _shadow == null) return;
        if (_shurikenCastTimer < MaxShurikenCastTime) return;
        if (!IsShadowWithinMimicRange(_shadow)) {
            _shurikenQueued     = false;
            _shurikenSpawnReady = false;
            return;
        }

        SpellCast(_zed, 0, SpellSlotType.ExtraSlots, _end, _end, true, _shadow.Position);
        _shurikenQueued     = false;
        _shurikenSpawnReady = false;
    }

    private void TryCastQueuedShadowSlash() {
        if (!IsShadowWithinMimicRange(_shadow)) return;

        var slashScript = _zed.GetSpell("ZedPBAOEDummy")?.Script as ZedPBAOEDummy;
        slashScript?.ExecuteQueuedShadowSlashOnArrival(_shadow, _slashQueuedCastId);
    }

    private void TryCastDeferredFullSlash() {
        var slashScript = _zed.GetSpell("ZedPBAOEDummy")?.Script as ZedPBAOEDummy;
        slashScript?.ExecuteSlashAtCurrentPositions(_slashQueuedCastId);
    }

    public void Swap() {
        if (_shadow == null) {
            _swapOnSpawn = true;
            SetSpellShadowDash(false);
            return;
        }

        _swapOnSpawn = false;
        var zedPosition = _zed.Position;
        TeleportTo(_zed, _shadow.Position.X, _shadow.Position.Y);
        TeleportTo(_shadow, zedPosition.X, zedPosition.Y);
        AddParticleTarget(_zed, null, "Zed_CloneSwap.troy", _shadow);
        AddParticleTarget(_zed, null, "Zed_CloneSwap.troy", _zed);
        SetSpellShadowDash();
        _currentShadowSwapped = true;
    }

    private void SetSpellShadowDash(bool deactivateBuff = true) {
        SetSpell(_zed, "ZedShadowDash", SpellSlotType.SpellSlots, 1);
        _zedShadowDash = _zed.GetSpell("ZedShadowDash");
        _zedShadowDash.SetLevel(_level);
        _zedShadowDash.SetSpellToggle(false);
        _zedShadowDash.SetCooldown(Math.Max(0f, _zed.GetSpell("ZedShadowDash").GetCooldown() - _cooldownToLower), true);
        UpdateWSeal();
        if (deactivateBuff) _buff.DeactivateBuff();
        _isZedW2         = false;
        _cooldownToLower = 0f;
    }

    public void SetSpellShadowSwap() {
        _level = _zedShadowDash.CastInfo.SpellLevel;
        SetSpell(_zed, "ZedW2", SpellSlotType.SpellSlots, 1);
        _zedW2         = _zed.GetSpell("ZedW2");
        _zedW2.SetLevel(_level);
        _zedW2.SetCooldown(0f, true);
        _zedW2.SetSpellToggle(true);
        UpdateWSeal();
        _isZedW2 = true;
    }

    public void ReduceWCooldown() {
        _cooldownToLower += 2f;
    }

    public bool IsZedW2 () {
        return _isZedW2;
    }

    private bool IsUltDashActive() {
        if (_zed == null) return false;

        return _zed.Status.HasFlag(StatusFlags.Rooted)
               && _zed.Status.HasFlag(StatusFlags.Ghosted)
               && !_zed.Status.HasFlag(StatusFlags.Targetable)
               && !_zed.Status.HasFlag(StatusFlags.CanAttack);
    }

    private bool IsShadowWithinMimicRange(Minion shadow) {
        if (shadow == null || shadow.IsDead || shadow.IsToRemove()) return false;
        return Vector2.Distance(_zed.Position, shadow.Position) <= ShadowMimicRange;
    }

    private bool HasValidShadow() {
        return _shadow != null && !_shadow.IsDead && !_shadow.IsToRemove();
    }
}
