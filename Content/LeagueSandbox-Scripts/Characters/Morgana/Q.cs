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
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class DarkBindingMissile: ISpellScript {
    
    private ObjAIBase _morgana;

    public SpellScriptMetadata ScriptMetadata => new() {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Arc
        },
        NotSingleTargetSpell = false,
        TriggersSpellCasts   = true,
        IsDamagingSpell      = true
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _morgana = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile) {
        if (!IsValidTarget(_morgana, target, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + _morgana.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        target.TakeDamage(_morgana, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL,
                          DamageResultType.RESULT_NORMAL);
        var duration = spell.SpellData.EffectLevelAmount[2][spell.CastInfo.SpellLevel];
        AddBuff("DarkBindingMissile", duration, 1, spell, target, _morgana);
        missile.SetToRemove();
    }
}
