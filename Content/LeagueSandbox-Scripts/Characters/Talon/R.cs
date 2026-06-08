using System;
using System.Collections.Generic;
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
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Spells;

public class TalonShadowAssault : ISpellScript {
    private ObjAIBase _talon;

    private const int BladeCount = 8;
    private readonly float[] angles = { 0, 45, 90, 135, 180, -135, -90, -45};
    private readonly float[] angles2 = { 22.5f, 67.5f, 112.5f, 157.5f, -157.5f, -112.5f, -67.5f, -22.5f};
    private readonly Vector2[] _positions = new Vector2[BladeCount];
    private readonly Vector2[] _positions2 = new Vector2[BladeCount];


    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        IsDamagingSpell     = true,
        TriggersSpellCasts = true,
        DoesntBreakShields = true,
        SpellToggleSlot = 2
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _talon, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        
    }

    public void OnSpellPostCast(Spell spell)
    {
        
        
        for (var i = 0; i < BladeCount; i++)
        {
            _positions[i] = GetPointFromUnit(_talon, _talon.GetSpell("TalonShadowAssaultMisOne").SpellData.CastRange[0],
                angles[i]);
            _positions2[i] = GetPointFromUnit(_talon,
                _talon.GetSpell("TalonShadowAssaultMisOneHalf").SpellData.CastRange[0], angles2[i]);
        }
        
        AddParticleTarget(_talon, _talon, "talon_ult_sound", _talon);
        AddParticleTarget(_talon, null, "talon_ult_cas",   _talon, bone: "root");
        
        var variables = new BuffVariables();
        variables.Set("positions", _positions);
        AddBuff("TalonShadowAssaultMisBuff", 2.5f, 1, spell, _talon, _talon, buffVariables: variables);
        AddBuff("TalonShadowAssaultAnimBuff", 0.5f, 1, spell, _talon, _talon);
        AddBuff("TalonShadowAssaultBuff", 2.5f, 1, spell, _talon, _talon);
        
        spell.CastInfo.Variables.Set("hitOutgoing", new HashSet<AttackableUnit>());
        for (var i = 0; i < BladeCount; i++) {
            SpellCast(_talon, 3, SpellSlotType.ExtraSlots, _positions[i], _positions[i], true, Vector2.Zero, inheritVariablesFrom: spell.CastInfo);
            SpellCast(_talon, 5, SpellSlotType.ExtraSlots, _positions2[i], _positions2[i], true, Vector2.Zero, inheritVariablesFrom: spell.CastInfo);
        }
        
        
    }

    private void OnUpdateStats(AttackableUnit owner,float diff) {
        SetSpellToolTipVar(owner, 0, _talon.Stats.AttackDamage.FlatBonus * 0.6f, SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
    }
}


public class TalonShadowAssaultToggle : ISpellScript
{
    private ObjAIBase _talon;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata()
    {
        NotSingleTargetSpell = true,
        TriggersSpellCasts = false
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _talon = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        if (!_talon.HasBuff("TalonShadowAssaultBuff")) return;
        RemoveBuff(owner, "TalonShadowAssaultBuff");
    }
}


public class TalonShadowAssaultMisOne : ISpellScript {
    private ObjAIBase _talon;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters{
            Type = MissileType.Arc,
        },
        IsDamagingSpell    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ScriptMetadata.MissileParameters.OverrideEndPosition = end;
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_talon, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions)) return;
        
        var hitThisPass = missile?.CastInfo.Variables.Get<HashSet<AttackableUnit>>("hitOutgoing");
        if (hitThisPass == null || !hitThisPass.Add(target)) {
            return;
        }
        
        var dmg = 120f + 50f * (_talon.Spells[3].CastInfo.SpellLevel - 1) + _talon.Stats.AttackDamage.FlatBonus * _talon.GetSpell("TalonShadowAssaultMisTwo").SpellData.Coefficient;

        AddParticleTarget(_talon, target, "talon_ult_tar", target);
        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }
}

public class TalonShadowAssaultMisOneHalf : ISpellScript {
    private ObjAIBase _talon;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters{
            Type = MissileType.Arc,
        },
        IsDamagingSpell    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end)
    {
        ScriptMetadata.MissileParameters.OverrideEndPosition = end;
    }
    
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_talon, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions)) return;
        
        var hitThisPass = missile?.CastInfo.Variables.Get<HashSet<AttackableUnit>>("hitOutgoing");
        if (hitThisPass == null || !hitThisPass.Add(target)) {
            return;
        }
        var dmg = 120f + 50f * (_talon.Spells[3].CastInfo.SpellLevel - 1) + _talon.Stats.AttackDamage.FlatBonus * _talon.GetSpell("TalonShadowAssaultMisTwo").SpellData.Coefficient;

        AddParticleTarget(_talon, target, "talon_ult_tar", target);
        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }
}

public class TalonShadowAssaultMisTwo : ISpellScript {
    private ObjAIBase _talon;
    private Spell     _spell;

    public SpellScriptMetadata ScriptMetadata { get; private set; } = new SpellScriptMetadata() {
        MissileParameters = new MissileParameters{
            Type = MissileType.Arc,
        },
        IsDamagingSpell    = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _talon = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _spell = spell;
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_talon, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions)) return;
        
        var hitThisPass = missile?.CastInfo.Variables.Get<HashSet<AttackableUnit>>("hitOutgoing");
        if (hitThisPass == null || !hitThisPass.Add(target)) {
            return;
        }
        var dmg = 120f + 50f * (_talon.Spells[3].CastInfo.SpellLevel - 1) + _talon.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;

        AddParticleTarget(_talon, target, "talon_ult_tar", target);
        target.TakeDamage(_talon, dmg, DamageType.DAMAGE_TYPE_PHYSICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
    }
}
