using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class DarkBindingMissile: ISpellScript {
    
    private ObjAIBase _morgana;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Circle
        },
        TriggersSpellCasts   = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _morgana = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
    }

    public void OnSpellCast(Spell spell) {
        
    }

    public void OnSpellPostCast(Spell spell) {
        
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        if (!IsValidTarget(_morgana, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;

        var dmg = 80f + 55f * (spell.CastInfo.SpellLevel - 1) + _morgana.Stats.AbilityPower.Total * 0.9f;
        target.TakeDamage(_morgana, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                          DamageResultType.RESULT_NORMAL);
        var duration = 2f + 0.25f * (spell.CastInfo.SpellLevel - 1);
        SpellCast(_morgana, 0, SpellSlotType.ExtraSlots, true, target, Vector2.Zero);
        AddBuff("DarkBinding", duration, 1, spell, target, _morgana);
        missile.SetToRemove();
    }
}

public class DarkBinding: ISpellScript {
    
    private ObjAIBase      _morgana;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts   = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _morgana = owner;
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target; 
        var duration = 2f + 0.25f * (_morgana.GetSpell("DarkBindingMissile").CastInfo.SpellLevel - 1);
        AddBuff("DarkBindingMissile", duration, 1, spell, _target, _morgana);
    }
}
