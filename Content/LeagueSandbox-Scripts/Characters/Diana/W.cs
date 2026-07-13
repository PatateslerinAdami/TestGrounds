using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
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

public class DianaOrbs : ISpellScript {
    private  ObjAIBase _diana;
    private const int   OrbCount  = 3;
    private const float OrbRadius = 150.0f;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _diana = owner;
    }

    public void OnSpellPostCast(Spell spell) {
        var orbsBuff = AddBuff("DianaOrbs", 5f, 1, spell, _diana, _diana);
        orbsBuff.BuffVars.Set("orbsConsumed", 0);
        AddBuff("DianaShield", 5f, 1, spell, _diana, _diana);
        var owner = spell.CastInfo.Owner;
        var facing = new Vector2(owner.Direction.X, owner.Direction.Z);
        if (facing.LengthSquared() <= float.Epsilon) facing = new Vector2(1.0f, 0.0f);
        facing = Vector2.Normalize(facing);

        var baseAngle = MathF.Atan2(facing.Y, facing.X);
        var deltaAngle = MathF.Tau / OrbCount;
        AddParticleTarget(_diana, _diana, "Diana_Base_W_Cas", _diana);
        for (var i = 0; i < OrbCount; i++) {
            var angle = baseAngle + i * deltaAngle;
            var orbPos = owner.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * OrbRadius;

            // Self-orbit cast: the orb orbits the cast TARGET (Diana, tracked live) and spawns at
            // the override-cast point. The circle-missile engine reads the orbit center from
            // Targets[0] and the radius/phase from the launch (= override-cast) position.
            SpellCast(owner, 3, SpellSlotType.ExtraSlots, true, owner, orbPos,
                      overrideForceLevel: spell.CastInfo.SpellLevel);
        }
    }
}

public class DianaOrbsMissile : ISpellScript {
    private ObjAIBase _diana;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell      = true,
        NotSingleTargetSpell = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _diana = owner;
        _spell = spell;
        ApiEventManager.OnLaunchMissile.AddListener(this, spell, OnLaunchMissile);
    }

    private void OnLaunchMissile(Spell spell, SpellMissile missile)
    {
        ApiEventManager.OnSpellMissileUpdate.AddListener(this, missile, OnUpdateMissile);
    }

    private void OnUpdateMissile(SpellMissile missile, float diff)
    {
        // Riot's orb model — no engine collision hit; the orb scans around its own position on each
        // distance-paced script update (LuaOnMissileUpdateDistanceInterval = 75u, same as Ahri) and
        // detonates script-side. Diana W is Riot's clone of Ahri W (DianaOrbsMissile.lua still
        // carries BuffName = "AhriFoxFire"); S1 AhriFoxFireMissile.lua — the only unstripped version
        // of this BB — is the reference for the update-hook shape.

        // Arming gate (S1 Ahri: SpellVars.Ready += 1 per update, seek from the Nth update on).
        // Replay-verified on 616 Diana + 862 Ahri detonations (spawn-MISREP → 0x5A destroy):
        // detonation times comb at exact multiples of 75u of ACTUAL orbit travel (Diana 0.192s =
        // 75/390 u/s, Ahri 0.25s = 75/300 u/s — confirming the engine's actual-distance pacing),
        // and the first comb tooth sits at the 2nd update for Diana (~0.38s floor; Ahri: 3rd,
        // 0.75s, matching her S1 Ready >= 3). So Diana arms one interval EARLIER than Ahri —
        // Ready >= 2. An orb spawning inside an enemy therefore correctly waits ~0.38s.
        var ready = missile.CastInfo.InstanceVars.GetInt("Ready") + 1;
        missile.CastInfo.InstanceVars.Set("Ready", ready);
        if (ready < 2) return;

        // Two-phase acquisition mirrored from the S1 Ahri BB (nearest enemy HERO first, only then
        // anything valid) — both scans use the BB iterator with EXPLICIT flags (never AlwaysSelf,
        // so the owner is never a candidate). Range 150 = contact feel: unlike Ahri's 650 seek,
        // Diana's orbs only pop on touch; 150 matches the orbit offset / the old CollisionRadius.
        var targets = ForNClosestVisibleUnitsInTargetArea(_diana, missile.Position, 150f, 1,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        if (targets.Count == 0)
        {
            targets = ForNClosestVisibleUnitsInTargetArea(_diana, missile.Position, 150f, 1,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
        }
        if (targets.Count == 0) return;

        Detonate(missile, targets[0]);
    }

    private void Detonate(SpellMissile missile, AttackableUnit target) {
        // Unlike Ahri (separate ExtraSpell4 = AhriFoxFireMissileTwo damage missile), Diana lists NO
        // damage sub-spell in her ExtraSpell slots — the detonation damage is applied directly here.
        var spellLevel = Math.Clamp((int) missile.CastInfo.SpellLevel, 1, 5);
        var damage = 22.0f + 12.0f * (spellLevel - 1) + _diana.Stats.AbilityPower.Total * _diana.GetSpell("DianaOrbs").SpellData.Coefficient;

        AddParticleTarget(_diana, target, "Diana_Base_W_Tar.troy", target);
        target.TakeDamage(_diana, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                          DamageResultType.RESULT_NORMAL);

        // Detonation splash around the orb (radius kept from the previous port; unrecoverable from
        // the stripped Lua). Non-visible BB variant — AoE damage is not fog-gated.
        var unitsInRange = ForEachUnitInTargetArea(_diana, missile.Position, 175f,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectHeroes).Where(unit => unit != target);

        foreach (var unit in unitsInRange)
        {
            AddParticleTarget(_diana, unit, "Diana_Base_W_Tar.troy", unit);
            unit.TakeDamage(_diana, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
        }
        AddParticlePos(_diana, "Diana_Base_W_Orb_End.troy", missile.Position, missile.Position);

        var orbsBuff = _diana.GetBuffWithName("DianaOrbs");
        if (orbsBuff != null) {
            var consumed = orbsBuff.BuffVars.GetInt("orbsConsumed") + 1;
            orbsBuff.BuffVars.Set("orbsConsumed", consumed);
            if (consumed >= 3) {
                var oldShield = _diana.GetBuffWithName("DianaShield");
                if (oldShield != null) _diana.RemoveBuff(oldShield);
                var refreshVars = new VariableTable();
                refreshVars.Set("isRefresh", true);
                AddBuff("DianaShield", 5f, 1, _spell, _diana, _diana, variableTable: refreshVars);
            }
        }

        ApiEventManager.OnSpellMissileUpdate.RemoveListener(this, missile, OnUpdateMissile);
        missile.SetToRemove();
    }
}
