using System;
using System.Linq;
using System.Numerics;
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
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class EvelynnR : ISpellScript {
    private ObjAIBase _evelynn;
    private Vector2   _targetPosition;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _evelynn        = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _targetPosition = start;
    }

    public void OnSpellPostCast(Spell spell) {
        SpellCast(_evelynn, 1, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition, true, Vector2.Zero);
        SpellCast(_evelynn, 4, SpellSlotType.ExtraSlots, _targetPosition, _targetPosition, true, Vector2.Zero);
    }
}

public class EvelynnRNuke : ISpellScript {
    private ObjAIBase _evelynn;
    private Vector2   _targetPosition;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        IsDamagingSpell = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _evelynn        = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _targetPosition = start;
        var ap = _evelynn.Stats.AbilityPower.Total / 100f * 0.01f;
        
        AddParticlePos(_evelynn, "Evelynn_R_cas", _targetPosition, _targetPosition);
        var units = GetUnitsInRange(_evelynn, _targetPosition, 250f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions| SpellDataFlags.AffectNeutral);
        foreach (var unit in units) {
            AddParticleTarget(_evelynn, unit, "Evelynn_R_tar", unit);
            var variables      = new BuffVariables();
            variables.Set("slowPercent", 0.3f + 0.2f * (_evelynn.GetSpell("EvelynnR").CastInfo.SpellLevel - 1));
            AddBuff("Slow", 2f, 1, _evelynn.GetSpell("EvelynnR"), unit, _evelynn, buffVariables: variables);
            unit.TakeDamage(_evelynn, IsValidTarget(_evelynn, unit, SpellDataFlags.AffectNeutral) ? Math.Min(1000f, unit.Stats.CurrentHealth * 0.15f + 0.05f * (_evelynn.GetSpell("EvelynnR").CastInfo.SpellLevel - 1) + ap) 
                                          : unit.Stats.CurrentHealth * 0.15f + 0.05f * (_evelynn.GetSpell("EvelynnR").CastInfo.SpellLevel - 1) + ap, 
                            DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
            if (_evelynn.GetSpell("EvelynnW").CastInfo.SpellLevel > 0){
                AddBuff("EvelynnWPassive", 3f, 1, spell, _evelynn, _evelynn);
            }
        }
    }
}

public class EvelynnRHeal : ISpellScript {
    private ObjAIBase _evelynn;
    private Vector2   _targetPosition;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _evelynn        = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _targetPosition = start;
        var units = GetUnitsInRange(_evelynn, _targetPosition, 250f, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes);
        if (units.Count == 0) return;
        var variables    = new BuffVariables();
        variables.Set("shieldAmount", units.Sum(unit => 150f));
        AddParticleTarget(_evelynn, _evelynn, "Evelynn_R_heal", _evelynn);
        AddBuff("EvelynnRShield", 6f, 1, _evelynn.GetSpell("EvelynnR"), _evelynn, _evelynn, buffVariables: variables);
    }
}