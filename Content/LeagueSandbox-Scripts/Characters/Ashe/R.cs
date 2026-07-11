using System;
using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class EnchantedCrystalArrow : ISpellScript {
    private float     _stunDuration = 1f;
    private ObjAIBase _ashe;
    public SpellScriptMetadata ScriptMetadata { get; }  = new () {
        MissileParameters = new MissileParameters() {
            Type = MissileType.Arc
        },
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ashe = owner;
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        if (target is not Champion) return;
        Vector2 castPosition = new Vector2(spell.CastInfo.SpellCastLaunchPosition.X, spell.CastInfo.SpellCastLaunchPosition.Z);
        var distance = Vector2.Distance(target.Position, castPosition);
        _stunDuration       = Math.Clamp(1f + 0.18f * distance * 0.02f, 1f, 3.5f);
        AddBuff("EnchantedCrystalArrow", _stunDuration, 1, spell, target, _ashe);
        AddBuff("EnchantedCrystalArrowSlow", 3f, 1, spell, target, _ashe);
        
        
        var ap = _ashe.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap;
        target.TakeDamage(_ashe, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
        
        var particleName = _ashe.SkinID switch
        {
            6 => "Ashe_Skin06_R_tar.troy",
            _ => "Ashe_Base_R_tar.troy"
        };
        
        SpellEffectCreate(particleName, _ashe, null, null, target.Position, target.Position, scale: 1f, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
        
        var units= GetUnitsInRange(_ashe, target.Position, 400, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes).Where(unit => unit != target);
        foreach (var unit in units) {
            AddBuff("EnchantedCrystalArrowSlow", 3f, 1, spell, unit, _ashe);
            unit.TakeDamage(_ashe, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
        }
        missile.SetToRemove();
    }
}