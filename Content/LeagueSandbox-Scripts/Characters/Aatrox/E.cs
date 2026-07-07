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
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class AatroxE : ISpellScript {
    ObjAIBase _aatrox;
    Vector2 _targetPosition;
    public SpellScriptMetadata ScriptMetadata => new() {
        NotSingleTargetSpell = false,
        TriggersSpellCasts   = true,
        IsDamagingSpell      = true,
        IsPetDurationBuff    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _aatrox, OnUpdateStats);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        if (!IsValidTarget(_aatrox, target,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral |
                SpellDataFlags.AffectHeroes  | SpellDataFlags.AffectMinions)) return;

        var ap  = _aatrox.Stats.AbilityPower.Total     * spell.SpellData.Coefficient;
        var ad  = _aatrox.Stats.AttackDamage.FlatBonus * spell.SpellData.Coefficient2;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap + ad;
        var hitParticle = _aatrox.SkinID switch {
            1 => "Aatrox_Skin01_EMissile_Hit",
            2 => "Aatrox_Skin02_EMissile_Hit",
            _ => "Aatrox_Base_EMissile_Hit"
        };
        AddParticleTarget(_aatrox, target, hitParticle, target, bone: "C_Buffbone_Glb_Chest_Loc", flags: FXFlags.UpdateOrientation);
        var duration = spell.SpellData.EffectLevelAmount[4][spell.CastInfo.SpellLevel];
        AddBuff("AatroxEConeMissile", duration, 1, spell, target, _aatrox);
        target.TakeDamage(_aatrox, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        var healthCost                  = _aatrox.Stats.CurrentHealth * 0.05f;
        _aatrox.Stats.CurrentHealth = Math.Max(1, _aatrox.Stats.CurrentHealth - healthCost);
        var buff = _aatrox.GetBuffWithName("AatroxPassive")?.BuffScript as AatroxPassive;
        buff?.AddBlood(healthCost);
        
        _targetPosition             = end;
        FaceDirection(_targetPosition, _aatrox, true);
    }

    public void OnSpellPostCast(Spell spell) {
        // Replay 39988886 (294 side / 136 center MISREPs): side-blade launch offset is 115u
        // perpendicular (|Start − OwnerPos| = 114..115 in every packet), not 100.
        Vector2 startingPosRight = GetPointFromUnit(_aatrox, 115f, 90);
        Vector2 startingPosLeft = GetPointFromUnit(_aatrox, 115f, 270);
        Vector2 endPos = GetPointFromUnit(_aatrox, 1000f, 0);
        _targetPosition = endPos;
        spell.CastInfo.InstanceVars.Set("hitOutgoing", new HashSet<AttackableUnit>());
        SpellCast(_aatrox, 0, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition, true, _aatrox.Position, inheritVariablesFrom: spell.CastInfo);
        SpellCast(_aatrox, 1, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition, true, startingPosLeft, inheritVariablesFrom: spell.CastInfo);
        SpellCast(_aatrox, 1, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition, true, startingPosRight, inheritVariablesFrom: spell.CastInfo);
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
            Type = MissileType.Arc,
            OverrideHeightAugment = 0f,
        },
        NotSingleTargetSpell = false,
        DoesntBreakShields = true,
        TriggersSpellCasts = false,
        IsDamagingSpell = true,
        IsDeathRecapSource = true
    };
    
    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        var hitThisPass = missile?.CastInfo.InstanceVars.Get<HashSet<AttackableUnit>>("hitOutgoing");
        if (hitThisPass == null || !hitThisPass.Add(target)) {
            return; // already hit by another blade of this pass, server-internal, nothing networked
        }
        _aatrox.Spells[2].ApplyEffects(target);
    }
}

public class AatroxEConeMissile2 : ISpellScript {
    private ObjAIBase _aatrox;
    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Arc,
        },
        NotSingleTargetSpell = false,
        DoesntBreakShields = true,
        TriggersSpellCasts = false,
        IsDamagingSpell = true,
        IsDeathRecapSource = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _aatrox = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        var hitThisPass = missile?.CastInfo.InstanceVars.Get<HashSet<AttackableUnit>>("hitOutgoing");
        if (hitThisPass == null || !hitThisPass.Add(target)) {
            return; // already hit by another blade of this pass, server-internal, nothing networked
        }
        _aatrox.Spells[2].ApplyEffects(target);
    }
}
