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
        TriggersSpellCasts = true
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
            
        var dmg = 250f + (175f * spell.CastInfo.SpellLevel - 1) * _ashe.Stats.AbilityPower.Total;
        AddBuff("Stun", _stunDuration, 1, spell, target, _ashe);
            
        var buffVariables1 = new BuffVariables();
        buffVariables1.Set("slowPercent", 0.5f);
        AddBuff("Chilled", 3f, 1, spell, target, _ashe, buffVariables: buffVariables1);
        target.TakeDamage(_ashe, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
        AddParticleTarget(_ashe, target, "Ashe_Base_R_tar", target);
        var units= GetUnitsInRange(_ashe, target.Position, 400, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral).Where(unit => unit != target);

        foreach (var unit in units) {
            var buffVariables = new BuffVariables();
            buffVariables.Set("slowPercent", 0.5f);
            AddBuff("Chilled", 3f, 1, spell, unit, _ashe, buffVariables: buffVariables);
            unit.TakeDamage(_ashe, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, DamageResultType.RESULT_NORMAL);
        }
        missile.SetToRemove();
    }
}