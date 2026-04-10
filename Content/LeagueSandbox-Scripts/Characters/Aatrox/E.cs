using System;
using System.Collections.Generic;
using System.Numerics;
using Buffs;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

internal static class AatroxECastTracker {
    private const long CastLifetimeMs = 2500;
    private static int _nextCastId = 1;

    private static readonly Dictionary<uint, int> _activeCastByOwner = new();
    private static readonly Dictionary<uint, int> _castByMissile = new();
    private static readonly Dictionary<int, HashSet<uint>> _targetsHitByCast = new();
    private static readonly Dictionary<int, long> _castExpiresAt = new();

    public static void BeginCast(ObjAIBase owner) {
        if (owner == null) return;
        Cleanup();

        var castId = _nextCastId++;
        if (_nextCastId == int.MaxValue) _nextCastId = 1;

        _activeCastByOwner[owner.NetId] = castId;
        _targetsHitByCast[castId] = new HashSet<uint>();
        _castExpiresAt[castId] = NowMs() + CastLifetimeMs;
    }

    public static void RegisterMissile(ObjAIBase owner, SpellMissile missile) {
        if (owner == null || missile == null) return;
        Cleanup();

        if (!_activeCastByOwner.TryGetValue(owner.NetId, out var castId)) return;
        _castByMissile[missile.NetId] = castId;
        _castExpiresAt[castId] = NowMs() + CastLifetimeMs;
    }

    public static bool RegisterFirstHit(SpellMissile missile, AttackableUnit target) {
        if (missile == null || target == null) return true;
        Cleanup();

        if (!_castByMissile.TryGetValue(missile.NetId, out var castId)) return true;
        if (!_targetsHitByCast.TryGetValue(castId, out var hitTargets)) {
            hitTargets = new HashSet<uint>();
            _targetsHitByCast[castId] = hitTargets;
        }

        _castExpiresAt[castId] = NowMs() + CastLifetimeMs;
        return hitTargets.Add(target.NetId);
    }

    public static void OnMissileEnd(SpellMissile missile) {
        if (missile == null) return;
        _castByMissile.Remove(missile.NetId);
    }

    private static long NowMs() { return Environment.TickCount64; }

    private static void Cleanup() {
        var now = NowMs();
        var expiredCastIds = new List<int>();
        foreach (var entry in _castExpiresAt) {
            if (entry.Value <= now) expiredCastIds.Add(entry.Key);
        }

        foreach (var castId in expiredCastIds) {
            _castExpiresAt.Remove(castId);
            _targetsHitByCast.Remove(castId);
        }

        var staleMissileIds = new List<uint>();
        foreach (var entry in _castByMissile) {
            if (!_targetsHitByCast.ContainsKey(entry.Value)) staleMissileIds.Add(entry.Key);
        }

        foreach (var missileId in staleMissileIds) {
            _castByMissile.Remove(missileId);
        }

        var staleOwners = new List<uint>();
        foreach (var entry in _activeCastByOwner) {
            if (!_targetsHitByCast.ContainsKey(entry.Value)) staleOwners.Add(entry.Key);
        }

        foreach (var ownerId in staleOwners) {
            _activeCastByOwner.Remove(ownerId);
        }
    }
}

public class AatroxE : ISpellScript {
    ObjAIBase _aatrox;
    Vector2 _targetPosition;
    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true,
        CastingBreaksStealth = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _aatrox, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var healthCost                  = _aatrox.Stats.CurrentHealth * 0.05f;
        _aatrox.Stats.CurrentHealth = Math.Max(1, _aatrox.Stats.CurrentHealth - healthCost);
        var buff = _aatrox.GetBuffWithName("AatroxPassive")?.BuffScript as AatroxPassive;
        buff?.AddBlood(healthCost);
        
        _targetPosition             = end;
        FaceDirection(_targetPosition, _aatrox, true);
        AatroxECastTracker.BeginCast(_aatrox);
    }

    public void OnSpellPostCast(Spell spell) {
        Vector2 startingPosRight = GetPointFromUnit(_aatrox, 100f, 90);
        Vector2 startingPosLeft = GetPointFromUnit(_aatrox, 100f, 270);
        Vector2 endPos = GetPointFromUnit(_aatrox, 1000f, 0);
        _targetPosition = endPos;
        SpellCast(_aatrox, 0, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition, true, _aatrox.Position);
        SpellCast(_aatrox, 1, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition, true, startingPosLeft);
        SpellCast(_aatrox, 1, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition, true, startingPosRight);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var cost = _aatrox.Stats.CurrentHealth * 0.05f;
        SetSpellToolTipVar(_aatrox, 2, cost, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }
}

public class AatroxEConeMissile : ISpellScript {
    private ObjAIBase _aatrox;
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle,
        },
        IsDamagingSpell = true
    };
    
    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnHit);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        AatroxECastTracker.RegisterMissile(_aatrox, missile);
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
    }

    private void OnMissileEnd(SpellMissile missile) {
        AatroxECastTracker.OnMissileEnd(missile);
    }

    public void OnHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_aatrox, target,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral |
                           SpellDataFlags.AffectHeroes  | SpellDataFlags.AffectMinions)) return;
        if (!AatroxECastTracker.RegisterFirstHit(missile, target)) return;

        var ap  = _aatrox.Stats.AbilityPower.Total     * _aatrox.Spells[2].SpellData.Coefficient;
        var ad  = _aatrox.Stats.AttackDamage.FlatBonus * _aatrox.Spells[2].SpellData.Coefficient2;
        var dmg = 75f + 35f * (_aatrox.GetSpell("AatroxE").CastInfo.SpellLevel - 1) + ap + ad;
        var hitParticle = _aatrox.SkinID switch {
            1 => "Aatrox_Skin01_EMissile_Hit",
            2 => "Aatrox_Skin02_EMissile_Hit",
            _ => "Aatrox_Base_EMissile_Hit"
        };
        AddParticleTarget(_aatrox, target, hitParticle, target);
        var duration = 1.75f + 0.25f * (_aatrox.Spells[2].CastInfo.SpellLevel - 1);
        AddBuff("AatroxEslow", duration, 1, spell, target, _aatrox);
        target.TakeDamage(_aatrox, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }
}

public class AatroxEConeMissile2 : ISpellScript {
    private ObjAIBase _aatrox;
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle,
        },
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile) {
        AatroxECastTracker.RegisterMissile(_aatrox, missile);
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd, true);
    }

    private void OnMissileEnd(SpellMissile missile) {
        AatroxECastTracker.OnMissileEnd(missile);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_aatrox, target,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral |
                           SpellDataFlags.AffectHeroes  | SpellDataFlags.AffectMinions)) return;
        if (!AatroxECastTracker.RegisterFirstHit(missile, target)) return;

        var ap  = _aatrox.Stats.AbilityPower.Total     * _aatrox.Spells[2].SpellData.Coefficient;
        var ad  = _aatrox.Stats.AttackDamage.FlatBonus * _aatrox.Spells[2].SpellData.Coefficient2;
        var dmg = 75f + 35f * (_aatrox.GetSpell("AatroxE").CastInfo.SpellLevel - 1) + ap + ad;
        var hitParticle = _aatrox.SkinID switch {
            1 => "Aatrox_Skin01_EMissile_Hit",
            2 => "Aatrox_Skin02_EMissile_Hit",
            _ => "Aatrox_Base_EMissile_Hit"
        };
        AddParticleTarget(_aatrox, target, hitParticle, target);
        var duration = 1.75f + 0.25f * (_aatrox.Spells[2].CastInfo.SpellLevel - 1);
        AddBuff("AatroxEslow", duration, 1, spell, target, _aatrox);
        target.TakeDamage(_aatrox, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }
}
