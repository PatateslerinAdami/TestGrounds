using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Buffs;
using CharScripts;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class TalonRake : ISpellScript {
    private ObjAIBase _talon;
    private Vector2   _pos1, _pos2, _pos3;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        NotSingleTargetSpell = true,
        DoesntBreakShields = true,
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        SpellToggleSlot = 2
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _talon, OnUpdateStats);
    }

    public void OnSpellCast(Spell spell) {
        // Hit-dedup is scoped PER CAST and PER PASS: each cast opens a fresh outgoing and a
        // fresh return window, and within a window only ONE of the three blades can hit a
        // given unit. Two quick casts (CDR) never dedup against each other. Riot tracks this
        // server-internally with zero wire trace (the TalonRakeMissileOneMarker client stub
        // is the replay-era leftover of that server script) — so no networked buffs for it.
        // The hit sets live in the cast's InstanceVars bag (Riot's InstanceVars equivalent):
        // all blade casts inherit it below, so every missile of THIS cast — outgoing and
        // return — shares the two sets, and overlapping casts stay isolated for free.
        spell.CastInfo.InstanceVars.Set("hitOutgoing", new HashSet<AttackableUnit>());
        spell.CastInfo.InstanceVars.Set("hitReturn", new HashSet<AttackableUnit>());

        // Replay-verified (55 casts): target points at exactly 750 units / +20°, 0°, −20° of
        // the cast direction. The 750 targets only define the directions — the missiles stop
        // at CastRange=700 (Riot's return blades spawn at ~699.7u from Talon).
        // fireWithoutCasting=true: three same-tick casts on the SAME spell object collide in
        // the cast state machine with a real cast (only one FinishCasting fires, stuck-ghost
        // missiles) — the forced path runs each call independently. The missing CastSpellAns
        // wire-fidelity for the blades (Riot sends 3× CastSpellAns + FCM) needs engine
        // support for overlapping casts first; tracked as a known delta.
        _pos1 = GetPointFromUnit(_talon, 750, 20);
        SpellCast(_talon, 1, SpellSlotType.ExtraSlots, _pos1, _pos1, true, Vector2.Zero,
                  inheritVariablesFrom: spell.CastInfo);
        _pos2 = GetPointFromUnit(_talon, 750, 0);
        SpellCast(_talon, 1, SpellSlotType.ExtraSlots, _pos2, _pos2, true, Vector2.Zero,
                  inheritVariablesFrom: spell.CastInfo);
        _pos3 = GetPointFromUnit(_talon, 750, -20);
        SpellCast(_talon, 1, SpellSlotType.ExtraSlots, _pos3, _pos3, true, Vector2.Zero,
                  inheritVariablesFrom: spell.CastInfo);
    }

    private void OnUpdateStats(AttackableUnit owner, float diff) {
        SetSpellToolTipVar(owner, 0, _talon.Stats.AttackDamage.FlatBonus * 0.6f, SpellbookType.SPELLBOOK_CHAMPION, 1,
                           SpellSlotType.SpellSlots);
    }
}

public class TalonRakeMissileOne : ISpellScript {
    private ObjAIBase _talon;

    // NO MissileParameters in the metadata: the three blades of one cast all run through
    // the SAME Spell object, and only one of the three same-tick casts reaches
    // FinishCasting — the metadata auto-create therefore yields exactly ONE missile (plus
    // it produced a ghost duplicate alongside the manual creates before). The blades are
    // created manually in OnSpellPreCast instead (fires once per SpellCast call), with the
    // parameters passed inline.
    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        NotSingleTargetSpell = true,
        IsDamagingSpell = true,
        SpellDamageRatio = 0.5f,
        IsDeathRecapSource = true,
        TriggersSpellCasts = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var missile = spell.CreateSpellMissile(new MissileParameters { Type = MissileType.Arc });
        if (missile != null) {
            ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd);
        }
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        // One blade hit per unit per outgoing pass — the set lives in the cast's shared
        // InstanceVars bag (set up by TalonRake per cast), so all three blades of this cast
        // dedup against each other while overlapping casts stay independent.
        var hitThisPass = missile?.CastInfo.InstanceVars.Get<HashSet<AttackableUnit>>("hitOutgoing");
        if (hitThisPass == null || !hitThisPass.Add(target)) {
            return; // already hit by another blade of this pass — server-internal, nothing networked
        }

        var dmg = 30 + 25                             * (_talon.GetSpell("TalonRake").CastInfo.SpellLevel - 1) +
                  _talon.Stats.AttackDamage.FlatBonus * _talon.GetSpell("TalonRakeMissileTwo").SpellData.Coefficient;

        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);
        AddParticleTarget(_talon, target, "talon_w_tar", target);
        // Replay-verified: "talonslow" (2.0s) applies on BOTH passes. "TalonSlow" hashes
        // identically to Riot's "talonslow" (hash is case-insensitive).
        AddBuff("TalonSlow", 2f, 1, spell, target, _talon);
    }

    private void OnMissileEnd(SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnMissileEnd);

        // Replay-verified return mechanism: NO CastSpellAns for TalonRakeMissileTwo — only a
        // MissileReplication (162 across two replays). fireWithoutCasting=true skips the
        // CastSpellAns (gated behind `if (cast)`) and FinishCasting auto-creates the missile
        // from the metadata MissileParameters with HasClientCastInfo=false → replication-only,
        // exactly Riot's wire shape: SpellSlot=47, Targets=1 (Talon), TargetPosition = Talon,
        // launch = blade endpoint (overrideCastPos, IsOverrideCastPosition=true). The far
        // ~20000u trajectory fallback comes from CastRange via the missile ctor. The blades
        // HOME onto Talon: LineMissileTrackUnits=1 re-aims server- and client-side at
        // Targets[0] every frame (S4 SpellLineMissile.cpp:923-930); en-route enemies are hit
        // by the native line collision and the blade ends when it reaches Talon.
        // inheritVariablesFrom: the return cast joins the originating cast's InstanceVars bag,
        // so the return blades share the per-cast "hitReturn" set.
        SpellCast(_talon, 2, SpellSlotType.ExtraSlots, true, _talon, missile.Position,
                  inheritVariablesFrom: missile.CastInfo);
    }
}

public class TalonRakeMissileTwo : ISpellScript {
    private ObjAIBase _talon;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        // Auto-creates the missile in FinishCasting when cast via fireWithoutCasting.
        // TriggersSpellCasts stays false (default) → no CastSpellAns, HasClientCastInfo=false
        // → MissileReplication-only spawn, matching Riot's wire for the return blades.
        MissileParameters = new MissileParameters {
            Type = MissileType.Arc,
        },
        NotSingleTargetSpell = false,
        DoesntBreakShields = false,
        IsDamagingSpell = true,
        SpellDamageRatio = 1
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileEnd.AddListener(this, missile, OnMissileEnd);
        //missile.SetSpeed(spell.SpellData.MissileMaxSpeed);
    }

    private void OnMissileEnd(SpellMissile missile)
    {
        ApiEventManager.OnLaunchMissile.RemoveListener(this, _spell, OnLaunchMissile);
        ApiEventManager.OnSpellMissileEnd.RemoveListener(this, missile, OnMissileEnd);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        // En-route hits come from the native line collision (the missile homes onto Talon
        // via LineMissileTrackUnits but still collides with valid units along the way).
        // Talon himself never passes IsValidTarget (ally), and the blade ends automatically
        // when it reaches its tracked destination (Talon).
        if (!IsValidTarget(_talon, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) {
            return;
        }
        // One blade hit per unit per return pass — shared per-cast set, inherited from the
        // originating cast via inheritVariablesFrom (see TalonRakeMissileOne.OnMissileEnd).
        var hitThisPass = missile?.CastInfo.InstanceVars.Get<HashSet<AttackableUnit>>("hitReturn");
        if (hitThisPass == null || !hitThisPass.Add(target)) {
            return; // already hit by another blade of this return pass
        }

        var dmg = 30f + 25f                           * (_talon.GetSpell("TalonRake").CastInfo.SpellLevel - 1) +
                  _talon.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;

        AddBuff("TalonSlow", 2f, 1, _spell, target, _talon);
        AddParticleTarget(_talon, target, "talon_w_tar", target);
        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);
    }
}
