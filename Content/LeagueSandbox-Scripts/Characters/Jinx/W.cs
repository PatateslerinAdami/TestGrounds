using System;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static GameServerCore.Content.HashFunctions;

namespace Spells;

public class JinxW : ISpellScript {
    private Vector2      _targetPos;
    private ObjAIBase _jinx;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        NotSingleTargetSpell = false,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jinx  = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate);
    }
    
    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _targetPos                      = end;
        FaceDirection(_targetPos, _jinx, true);
        // Patch 4.x: Zap's wind-up is a FIXED 0.6s (JinxWMissile.json OverrideCastTime);
        // the attack-speed scaling (0.6 → 0.4) was only introduced in V9.1 — don't model it.
        // No lockout buff: 0 hits for "JinxWLockout" across 143 replay W casts — the
        // movement lock comes from the real JinxWMissile cast itself (CantCancelWhileWindingUp).

        var dir = Vector2.Normalize(end - _jinx.Position);
        AddParticlePos(_jinx, "Jinx_W_Beam", _jinx.Position, _jinx.Position, lifetime: 1f, direction: new Vector3(dir.X, 0, dir.Y), flags: FXFlags.UpdateOrientation | FXFlags.SimulateWhileOffScreen);
    }

    

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_jinx, 6, SpellSlotType.ExtraSlots, _targetPos, _targetPos, false, Vector2.Zero);
    }

    private void OnStatsUpdate(AttackableUnit unit, float diff) {
        var bonusAd = _jinx.Stats.AttackDamage.Total * _spell.SpellData.Coefficient;
        SetSpellToolTipVar(_jinx, 2, bonusAd, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }
}

public class JinxWMissile : ISpellScript {
    private ObjAIBase _jinx;

    // Replay-verified flow (144 W casts): the 0.6s Zap delay lives in THIS spell's own
    // cast — NPC_CastSpellAns JinxWMissile with castTime=0.60 goes out at W cast time
    // (same tick as the JinxW CastSpellAns), the client opens a 0.6s pending cast, and the
    // server fires the missile at windup end via ForceCreateMissile (+594ms median).
    // Requirements for that wire pattern:
    //  - TriggersSpellCasts = true → CastSpellAns is sent AND HasClientCastInfo=true →
    //    FinishCasting emits FCM instead of a MissileReplication.
    //  - cast via SpellCast(..., fireWithoutCasting: false, ...) (JinxW.OnSpellPostCast)
    //    so the real 0.6s windup runs server-side.
    //  - the 0.6 comes from JinxWMissile.json OverrideCastTime automatically (Spell.Cast
    //    reads it into DesignerCastTime) — no manual override needed. Fixed in 4.x; the
    //    attack-speed scaling is a V9.1 change.
    //  - NOTE: Riot's server-lua stub says DoesntTriggerSpellCasts=true, yet the wire has
    //    144 CastSpellAns for this spell — Riot's flag governs PROC/event semantics
    //    (Sheen, stealth break), NOT the cast packet. Our TriggersSpellCasts is overloaded
    //    (gates packets too), so it must stay true here. Never copy that lua flag 1:1.
    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        MissileParameters = new MissileParameters {
            Type = MissileType.Arc
        },
        NotSingleTargetSpell = false,
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _jinx = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ad     = _jinx.Stats.AttackDamage.Total * spell.SpellData.Coefficient;
        var damage = 10 + 50 * (spell.CastInfo.SpellLevel - 1) + ad;

        AddParticleTarget(_jinx, target, "Jinx_W_Tar", target);
        target.TakeDamage(_jinx, damage, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        var variables      = new BuffVariables();
        variables.Set("slowPercent", 0.3f + 0.1f * (_jinx.GetSpell("JinxW").CastInfo.SpellLevel - 1));
        AddBuff("Slow", 2f, 1, spell, target, _jinx, buffVariables: variables);
        AddBuff("JinxWSight", 2f, 1, spell, target, _jinx);

        missile.SetToRemove();
    }
}
