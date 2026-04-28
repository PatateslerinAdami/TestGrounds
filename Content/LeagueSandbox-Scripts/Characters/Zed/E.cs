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

internal static class ZedSlashCastTracker {
    private const long CastLifetimeMs = 3000;
    private static int _nextCastId = 1;
    private static readonly Dictionary<uint, int> _activeCastByOwner = new();
    private static readonly Dictionary<int, HashSet<uint>> _damagedTargetsByCast = new();
    private static readonly Dictionary<int, bool> _energyRefundedByCast = new();
    private static readonly Dictionary<int, long> _castExpiresAt = new();

    public static int BeginCast(ObjAIBase owner) {
        if (owner == null) return 0;
        Cleanup();

        var castId = _nextCastId++;
        if (_nextCastId == int.MaxValue) _nextCastId = 1;

        _activeCastByOwner[owner.NetId] = castId;
        _damagedTargetsByCast[castId]   = new HashSet<uint>();
        _energyRefundedByCast[castId]   = false;
        _castExpiresAt[castId]          = Environment.TickCount64 + CastLifetimeMs;
        return castId;
    }

    public static int GetActiveCastId(ObjAIBase owner) {
        if (owner == null) return 0;
        Cleanup();
        return _activeCastByOwner.TryGetValue(owner.NetId, out var castId) ? castId : 0;
    }

    public static bool TryMarkDamaged(int castId, AttackableUnit target) {
        if (castId <= 0 || target == null) return true;
        Cleanup();

        if (!_damagedTargetsByCast.TryGetValue(castId, out var damagedTargets)) {
            damagedTargets = new HashSet<uint>();
            _damagedTargetsByCast[castId] = damagedTargets;
        }

        _castExpiresAt[castId] = Environment.TickCount64 + CastLifetimeMs;
        return damagedTargets.Add(target.NetId);
    }

    public static bool TryConsumeEnergyRefund(int castId) {
        if (castId <= 0) return false;
        Cleanup();

        if (!_energyRefundedByCast.TryGetValue(castId, out var alreadyRefunded)) {
            _energyRefundedByCast[castId] = false;
            alreadyRefunded = false;
        }

        if (alreadyRefunded) return false;
        _energyRefundedByCast[castId] = true;
        _castExpiresAt[castId]        = Environment.TickCount64 + CastLifetimeMs;
        return true;
    }

    private static void Cleanup() {
        var now = Environment.TickCount64;
        var expiredCastIds = new List<int>();
        foreach (var entry in _castExpiresAt) {
            if (entry.Value <= now) expiredCastIds.Add(entry.Key);
        }

        foreach (var castId in expiredCastIds) {
            _castExpiresAt.Remove(castId);
            _damagedTargetsByCast.Remove(castId);
            _energyRefundedByCast.Remove(castId);
        }

        var staleOwners = new List<uint>();
        foreach (var entry in _activeCastByOwner) {
            if (!_damagedTargetsByCast.ContainsKey(entry.Value)) staleOwners.Add(entry.Key);
        }

        foreach (var ownerId in staleOwners) {
            _activeCastByOwner.Remove(ownerId);
        }
    }
}

public class ZedPBAOEDummy : ISpellScript {
    private const float ZedSlashRange       = 315f;
    private const float ShadowSlashRange    = 290f;
    private const float ShadowMimicRange    = 2000f;
    private const float SlashBaseDamage     = 60f;
    private const float SlashDamagePerLevel = 30f;
    private const float SlashBonusAdRatio   = 0.9f;
    private const float SlowDuration        = 1.5f;
    private const float StrongSlowBase      = 0.3f;
    private const float StrongSlowPerLevel  = 0.075f;
    private const float WeakSlowBase        = 0.2f;
    private const float WeakSlowPerLevel    = 0.05f;
    private static readonly SpellDataFlags SlashTargetFlags =
        SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
        SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions;

    private ObjAIBase _zed;
    private Spell     _spell;
    private int       _activeCastId;

    private sealed class SlashHitProfile {
        public bool HitByZed;
        public bool HitByWShadow;
        public bool HitByRShadow;
    }

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
        _activeCastId = ZedSlashCastTracker.BeginCast(_zed);
    }

    public void OnSpellCast(Spell spell) { }

    public void OnSpellPostCast(Spell spell) {
        var wHandlerBuff = _zed.GetBuffWithName("ZedWHandler");
        var wHandler     = wHandlerBuff?.BuffScript as ZedWHandler;
        if (wHandler != null && wHandler.ShouldDeferSlashUntilSpawnSwap()) {
            wHandler.DeferFullSlashOnSpawn(_activeCastId);
            return;
        }

        // Queue shadow slash on arrival for this exact cast if W shadow is still in-flight.
        wHandler?.QueueSlash(_activeCastId);

        ExecuteSlashAtCurrentPositions(_activeCastId);
    }

    public static int GetActiveSlashCastId(ObjAIBase owner) {
        return ZedSlashCastTracker.GetActiveCastId(owner);
    }

    public void ExecuteSlashAtCurrentPositions(int castId = 0) {
        SpellCast(_zed, 2, SpellSlotType.ExtraSlots, true, _zed, _zed.Position);
        var level       = _spell.CastInfo.SpellLevel;
        var strongSlow  = StrongSlowBase + StrongSlowPerLevel * (level - 1);
        var weakSlow    = WeakSlowBase   + WeakSlowPerLevel   * (level - 1);
        var slashDamage = SlashBaseDamage + SlashDamagePerLevel * (level - 1)
                          + _zed.Stats.AttackDamage.FlatBonus * SlashBonusAdRatio;

        var zedSlashHits = GetSlashHits(_zed, ZedSlashRange);

        var zedShadowHandlerBuff = _zed.GetBuffWithName("ZedShadowHandler");
        var zedShadowHandler     = zedShadowHandlerBuff?.BuffScript as ZedShadowHandler;

        var wShadow = zedShadowHandler?.GetWShadow();
        var rShadow = zedShadowHandler?.GetRShadow();
        var wShadowSlashHits = GetShadowSlashHits(IsShadowWithinMimicRange(wShadow) ? wShadow : null);
        var rShadowSlashHits = GetShadowSlashHits(IsShadowWithinMimicRange(rShadow) ? rShadow : null);

        ResolveSlashHits(zedSlashHits, wShadowSlashHits, rShadowSlashHits, slashDamage, strongSlow, weakSlow, castId);

        // Cooldown reduction should only come from enemy champions hit by Zed's own slash.
        LowerWCooldownPerHit(CountChampionHits(zedSlashHits));
    }

    public void ExecuteQueuedShadowSlashOnArrival(Minion shadow, int castId = 0) {
        if (shadow == null) return;
        if (shadow.IsDead || shadow.IsToRemove()) return;
        if (!IsShadowWithinMimicRange(shadow)) return;

        var level       = _spell.CastInfo.SpellLevel;
        var strongSlow  = StrongSlowBase + StrongSlowPerLevel * (level - 1);
        var weakSlow    = WeakSlowBase   + WeakSlowPerLevel   * (level - 1);
        var slashDamage = SlashBaseDamage + SlashDamagePerLevel * (level - 1)
                          + _zed.Stats.AttackDamage.FlatBonus * SlashBonusAdRatio;

        var shadowHits = GetSlashHits(shadow, ShadowSlashRange);
        PlayAnimation(shadow, "Spell3", 0.5f);
        AddParticleTarget(_zed, shadow, "Zed_Base_E_cas", shadow);
        if (shadowHits.Count == 0) return;

        var zedHits = GetSlashHits(_zed, ZedSlashRange);
        var zedShadowHandlerBuff = _zed.GetBuffWithName("ZedShadowHandler");
        var zedShadowHandler     = zedShadowHandlerBuff?.BuffScript as ZedShadowHandler;
        var wShadow             = zedShadowHandler?.GetWShadow();
        var rShadow             = zedShadowHandler?.GetRShadow();

        HashSet<AttackableUnit> wShadowHits = [];
        HashSet<AttackableUnit> rShadowHits = [];

        if (wShadow == shadow) {
            wShadowHits = shadowHits;
            if (IsShadowWithinMimicRange(rShadow)) {
                rShadowHits = GetSlashHits(rShadow, ShadowSlashRange);
            }
        } else if (rShadow == shadow) {
            rShadowHits = shadowHits;
            if (IsShadowWithinMimicRange(wShadow)) {
                wShadowHits = GetSlashHits(wShadow, ShadowSlashRange);
            }
        } else {
            // Fallback: treat the casting shadow as a shadow source even if tracker lookup is stale.
            wShadowHits = shadowHits;
        }

        ResolveSlashHits(zedHits, wShadowHits, rShadowHits, slashDamage, strongSlow, weakSlow, castId);
    }

    private HashSet<AttackableUnit> GetSlashHits(AttackableUnit source, float range) {
        return new HashSet<AttackableUnit>(GetUnitsInRange(source, source.Position, range, true, SlashTargetFlags));
    }

    private HashSet<AttackableUnit> GetShadowSlashHits(Minion shadow) {
        if (shadow == null) return [];

        var hits = GetSlashHits(shadow, ShadowSlashRange);
        PlayAnimation(shadow, "Spell3", 0.5f);
        AddParticleTarget(_zed, shadow, "Zed_Base_E_cas", shadow);
        return hits;
    }

    private bool IsShadowWithinMimicRange(Minion shadow) {
        if (shadow == null || shadow.IsDead || shadow.IsToRemove()) return false;
        return Vector2.Distance(_zed.Position, shadow.Position) <= ShadowMimicRange;
    }

    private void ResolveSlashHits(
        HashSet<AttackableUnit> zedSlashHits,
        HashSet<AttackableUnit> wShadowSlashHits,
        HashSet<AttackableUnit> rShadowSlashHits,
        float                   slashDamage,
        float                   strongSlow,
        float                   weakSlow,
        int                     castId
    ) {
        var hitProfiles = BuildSlashHitProfiles(zedSlashHits, wShadowSlashHits, rShadowSlashHits);
        var energyReturn                      = 20f + 5f * (_spell.CastInfo.SpellLevel - 1);
        var refundedForUntrackedCastInstance = false;

        foreach (var (enemy, profile) in hitProfiles) {
            if (enemy == null || enemy.IsDead || enemy.IsToRemove()) continue;

            AddParticleTarget(_zed, enemy, "Zed_E_Tar", enemy);

            var canDealDamage = castId <= 0 || ZedSlashCastTracker.TryMarkDamaged(castId, enemy);
            if (canDealDamage) {
                var healthBefore = enemy.Stats.CurrentHealth;
                enemy.TakeDamage(
                    _zed,
                    slashDamage,
                    DamageType.DAMAGE_TYPE_PHYSICAL,
                    DamageSource.DAMAGE_SOURCE_SPELLAOE,
                    DamageResultType.RESULT_NORMAL
                );

                var tookDamage = enemy.Stats.CurrentHealth < healthBefore;
                if (!tookDamage) {
                    ConsumeBlockedSlash(profile);
                }
            }

            var successfulShadowHits = GetSuccessfulShadowHitCount(profile);
            if (successfulShadowHits <= 0) continue;

            var successfulHitCount = successfulShadowHits + (profile.HitByZed ? 1 : 0);
            ApplySlow(enemy, successfulHitCount >= 2 ? strongSlow : weakSlow);

            var hasOverlap = successfulHitCount >= 2;
            if (!hasOverlap) continue;

            if (castId > 0) {
                if (ZedSlashCastTracker.TryConsumeEnergyRefund(castId)) {
                    IncreasePAR(_zed, energyReturn, PrimaryAbilityResourceType.Energy);
                }
            } else if (!refundedForUntrackedCastInstance) {
                IncreasePAR(_zed, energyReturn, PrimaryAbilityResourceType.Energy);
                refundedForUntrackedCastInstance = true;
            }
        }
    }

    private Dictionary<AttackableUnit, SlashHitProfile> BuildSlashHitProfiles(
        HashSet<AttackableUnit> zedSlashHits,
        HashSet<AttackableUnit> wShadowSlashHits,
        HashSet<AttackableUnit> rShadowSlashHits
    ) {
        var hitProfiles = new Dictionary<AttackableUnit, SlashHitProfile>();

        MarkSlashHits(hitProfiles, zedSlashHits, byZed: true);
        MarkSlashHits(hitProfiles, wShadowSlashHits, byWShadow: true);
        MarkSlashHits(hitProfiles, rShadowSlashHits, byRShadow: true);
        return hitProfiles;
    }

    private void MarkSlashHits(
        Dictionary<AttackableUnit, SlashHitProfile> hitProfiles,
        IEnumerable<AttackableUnit>                hits,
        bool                                       byZed     = false,
        bool                                       byWShadow = false,
        bool                                       byRShadow = false
    ) {
        foreach (var enemy in hits) {
            if (enemy == null) continue;
            if (!hitProfiles.TryGetValue(enemy, out var profile)) {
                profile            = new SlashHitProfile();
                hitProfiles[enemy] = profile;
            }

            if (byZed) profile.HitByZed = true;
            if (byWShadow) profile.HitByWShadow = true;
            if (byRShadow) profile.HitByRShadow = true;
        }
    }

    private void ConsumeBlockedSlash(SlashHitProfile profile) {
        // Spell shields prioritize blocking a shadow slash over Zed's own slash.
        if (profile.HitByWShadow) {
            profile.HitByWShadow = false;
            return;
        }

        if (profile.HitByRShadow) {
            profile.HitByRShadow = false;
            return;
        }

        if (profile.HitByZed) {
            profile.HitByZed = false;
        }
    }

    private int GetSuccessfulShadowHitCount(SlashHitProfile profile) {
        var count = 0;
        if (profile.HitByWShadow) count++;
        if (profile.HitByRShadow) count++;
        return count;
    }

    private int CountChampionHits(IEnumerable<AttackableUnit> hits) {
        var count = 0;
        foreach (var unit in hits) {
            if (unit is Champion) count++;
        }

        return count;
    }

    private void ApplySlow(AttackableUnit target, float slowAmount) {
        var variables = new BuffVariables();
        variables.Set("slowPercent", slowAmount);
        AddBuff("Slow", SlowDuration, 1, _spell, target, _zed, buffVariables: variables);
    }

    private void LowerWCooldownPerHit(int hitCount) {
        if (hitCount <= 0) return;

        var wHandlerBuff = _zed.GetBuffWithName("ZedWHandler");
        var wHandler     = wHandlerBuff?.BuffScript as ZedWHandler;
        if (wHandler != null && wHandler.IsZedW2()) {
            for (var i = 0; i < hitCount; i++) {
                wHandler.ReduceWCooldown();
            }
            return;
        }

        var wCooldownSpell = _zed.GetSpell("ZedShadowDash") ?? _zed.GetSpell("ZedW2");
        if (wCooldownSpell == null) return;
        for (var i = 0; i < hitCount; i++) {
            wCooldownSpell.LowerCooldown(2f);
        }
    }
}

public class ZedPBAOE : ISpellScript {
    private ObjAIBase _zed;

    public SpellScriptMetadata ScriptMetadata => new() { };

    public void OnActivate(ObjAIBase owner, Spell spell) { _zed = owner; }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        PlayAnimation(_zed, "Spell3", 0.5f);
        AddParticleTarget(_zed, _zed, "Zed_Base_E_cas", _zed);
    }
}
