using System;
using System.Collections.Generic;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

internal static class ZedShurikenCastTracker {
    private const long CastLifetimeMs = 3000;
    private static int _nextCastId = 1;
    private static readonly Dictionary<uint, int> _activeCastByOwner = new();
    private static readonly Dictionary<uint, int> _castByMissile = new();
    private static readonly Dictionary<int, Dictionary<uint, int>> _targetHitCountByCast = new();
    private static readonly Dictionary<int, bool> _energyRefundedByCast = new();
    private static readonly Dictionary<int, long> _castExpiresAt = new();

    public static void BeginCast(ObjAIBase owner) {
        if (owner == null) return;
        Cleanup();

        var castId = _nextCastId++;
        if (_nextCastId == int.MaxValue) _nextCastId = 1;

        _activeCastByOwner[owner.NetId] = castId;
        _targetHitCountByCast[castId] = new Dictionary<uint, int>();
        _energyRefundedByCast[castId] = false;
        _castExpiresAt[castId] = NowMs() + CastLifetimeMs;
    }

    public static void RegisterMissile(ObjAIBase owner, SpellMissile missile) {
        if (owner == null || missile == null) return;
        Cleanup();

        if (!_activeCastByOwner.TryGetValue(owner.NetId, out var castId)) return;
        _castByMissile[missile.NetId] = castId;
    }

    public static float RegisterHitAndGetDamageMultiplier(SpellMissile missile, AttackableUnit target, out bool shouldRefundEnergy) {
        shouldRefundEnergy = false;
        if (missile == null || target == null) return 1.0f;
        Cleanup();

        if (!_castByMissile.TryGetValue(missile.NetId, out var castId)) return 1.0f;
        if (!_targetHitCountByCast.TryGetValue(castId, out var hitCountByTarget)) {
            hitCountByTarget = new Dictionary<uint, int>();
            _targetHitCountByCast[castId] = hitCountByTarget;
        }

        hitCountByTarget.TryGetValue(target.NetId, out var hitCount);
        hitCount++;
        hitCountByTarget[target.NetId] = hitCount;

        if (hitCount >= 2 && TryConsumeEnergyRefund(castId)) {
            shouldRefundEnergy = true;
        }

        _castExpiresAt[castId] = NowMs() + CastLifetimeMs;
        return MathF.Pow(0.5f, hitCount - 1);
    }

    public static void OnMissileEnd(SpellMissile missile) {
        if (missile == null) return;
        _castByMissile.Remove(missile.NetId);
    }

    private static long NowMs() { return Environment.TickCount64; }

    private static bool TryConsumeEnergyRefund(int castId) {
        if (castId <= 0) return false;
        Cleanup();

        if (!_energyRefundedByCast.TryGetValue(castId, out var alreadyRefunded)) {
            _energyRefundedByCast[castId] = false;
            alreadyRefunded = false;
        }

        if (alreadyRefunded) return false;
        _energyRefundedByCast[castId] = true;
        _castExpiresAt[castId]        = NowMs() + CastLifetimeMs;
        return true;
    }

    private static void Cleanup() {
        var now = NowMs();
        var expiredCastIds = new List<int>();
        foreach (var entry in _castExpiresAt) {
            if (entry.Value <= now) expiredCastIds.Add(entry.Key);
        }

        foreach (var castId in expiredCastIds) {
            _castExpiresAt.Remove(castId);
            _targetHitCountByCast.Remove(castId);
            _energyRefundedByCast.Remove(castId);
        }

        var staleMissileIds = new List<uint>();
        foreach (var entry in _castByMissile) {
            if (!_targetHitCountByCast.ContainsKey(entry.Value)) staleMissileIds.Add(entry.Key);
        }

        foreach (var missileId in staleMissileIds) {
            _castByMissile.Remove(missileId);
        }

        var staleOwners = new List<uint>();
        foreach (var entry in _activeCastByOwner) {
            if (!_targetHitCountByCast.ContainsKey(entry.Value)) staleOwners.Add(entry.Key);
        }

        foreach (var ownerId in staleOwners) {
            _activeCastByOwner.Remove(ownerId);
        }
    }
}

public class ZedShuriken : ISpellScript {
    private const float ShadowMimicRange = 2000f;

    private ObjAIBase _zed;
    private Spell     _spell;
    private Vector2   _end;
    private ZedShadowHandler      _buff;
    private Minion    _wShadow;
    private Minion    _rShadow;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        },
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _zed   = owner;
        _spell = spell;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _end = end;
        ZedShurikenCastTracker.BeginCast(_zed);

        var wHandlerBuff = _zed.GetBuffWithName("ZedWHandler");
        var zedWHandler  = wHandlerBuff?.BuffScript as ZedWHandler;
        zedWHandler?.QueueShuriken(_end);

        var shadowHandlerBuff = _zed.GetBuffWithName("ZedShadowHandler");
        _buff                 = shadowHandlerBuff?.BuffScript as ZedShadowHandler;
        if (_buff == null) return;

        _wShadow = _buff.GetWShadow();
        if (IsShadowWithinMimicRange(_wShadow)) {
            FaceDirection(_end, _wShadow);
            PlayAnimation(_wShadow, "Spell1");
        } else {
            _wShadow = null;
        }

        _rShadow = _buff.GetRShadow();
        if (!IsShadowWithinMimicRange(_rShadow)) {
            _rShadow = null;
            return;
        }

        FaceDirection(_end, _rShadow);
        PlayAnimation(_rShadow, "Spell1");
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_zed, 1, SpellSlotType.ExtraSlots, _end, _end, true, Vector2.Zero);
        if (IsShadowWithinMimicRange(_wShadow)) {
            SpellCast(_zed, 0, SpellSlotType.ExtraSlots, _end, _end, true, _wShadow.Position);
        }
        if (IsShadowWithinMimicRange(_rShadow)) {
            SpellCast(_zed, 6, SpellSlotType.ExtraSlots, _end, _end, true, _rShadow.Position);
        }
    }

    private bool IsShadowWithinMimicRange(Minion shadow) {
        if (shadow == null || shadow.IsDead || shadow.IsToRemove()) return false;
        return Vector2.Distance(_zed.Position, shadow.Position) <= ShadowMimicRange;
    }
}

public class ZedShurikenMisOne : ISpellScript {
    private ObjAIBase      _owner;
    private Spell          _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
    }

    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
        
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        ZedShurikenCastTracker.RegisterMissile(_owner, missile);
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
    }

    private void OnMissileEnd(SpellMissile missile) {
        ZedShurikenCastTracker.OnMissileEnd(missile);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ad     = _owner.Stats.AttackDamage.FlatBonus;
        var qLevel = _owner.GetSpell("ZedShuriken").CastInfo.SpellLevel;
        var damage = 75f + 35f * (qLevel - 1) + ad;
        damage *= ZedShurikenCastTracker.RegisterHitAndGetDamageMultiplier(missile, target, out var shouldRefundEnergy);

        if (shouldRefundEnergy) {
            var energyReturn = 20f + 5f * (qLevel - 1);
            IncreasePAR(_owner, energyReturn, PrimaryAbilityResourceType.Energy);
        }

        AddParticleTarget(_owner, target, "Zed_Q_tar", target, bone: "C_BUFFBONE_GLB_CHEST_LOC");
        target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
    }
}

public class ZedShurikenMisTwo : ISpellScript {
    private ObjAIBase _owner;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
    }

    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
        
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        ZedShurikenCastTracker.RegisterMissile(_owner, missile);
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
    }

    private void OnMissileEnd(SpellMissile missile) {
        ZedShurikenCastTracker.OnMissileEnd(missile);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ad     = _owner.Stats.AttackDamage.FlatBonus;
        var qLevel = _owner.GetSpell("ZedShuriken").CastInfo.SpellLevel;
        var damage = 75f + 35f * (qLevel - 1) + ad;
        damage *= ZedShurikenCastTracker.RegisterHitAndGetDamageMultiplier(missile, target, out var shouldRefundEnergy);

        if (shouldRefundEnergy) {
            var energyReturn = 20f + 5f * (qLevel - 1);
            IncreasePAR(_owner, energyReturn, PrimaryAbilityResourceType.Energy);
        }

        AddParticleTarget(_owner, target, "Zed_Q_tar_Double", target, bone: "C_BUFFBONE_GLB_CHEST_LOC");
        target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
    }
}

public class ZedShurikenMisThree : ISpellScript {
    private ObjAIBase _owner;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
    }

    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
        
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        ZedShurikenCastTracker.RegisterMissile(_owner, missile);
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
    }

    private void OnMissileEnd(SpellMissile missile) {
        ZedShurikenCastTracker.OnMissileEnd(missile);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ad     = _owner.Stats.AttackDamage.FlatBonus;
        var qLevel = _owner.GetSpell("ZedShuriken").CastInfo.SpellLevel;
        var damage = 75f + 40f * (qLevel - 1) + ad;
        damage *= ZedShurikenCastTracker.RegisterHitAndGetDamageMultiplier(missile, target, out var shouldRefundEnergy);

        if (shouldRefundEnergy) {
            var energyReturn = 20f + 5f * (qLevel - 1);
            IncreasePAR(_owner, energyReturn, PrimaryAbilityResourceType.Energy);
        }

        AddParticleTarget(_owner, target, "Zed_Q_tar_Double", target);
        target.TakeDamage(_owner, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
    }
}
