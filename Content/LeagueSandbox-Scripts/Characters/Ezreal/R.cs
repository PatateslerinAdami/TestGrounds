using System;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class EzrealTrueshotBarrage : ISpellScript {
    private ObjAIBase _owner;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        CastTime = 1f,
        TriggersSpellCasts = true,
        NotSingleTargetSpell = true,
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Circle
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _owner = owner;
        _spell = spell;
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
        ApiEventManager.OnUpdateStats.AddListener(this, owner, OnStatsUpdate);
    }

    public void OnSpellCast(Spell spell) {
        AddParticleTarget(_owner, _owner, "Ezreal_bow_huge",      _owner, bone: "L_hand", flags: FXFlags.BindDirection);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var owner = spell.CastInfo.Owner;
        if (missile is not SpellCircleMissile skillshot) return;
        var reduc   = Math.Min(skillshot.ObjectsHit.Count, 7);
        var bonusAd = _owner.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        var ap      = owner.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var damage  = 350f + 150f * (spell.CastInfo.SpellLevel - 1) + bonusAd + ap;
        target.TakeDamage(owner, damage * (1f - (reduc - 1f) / 10f), DamageType.DAMAGE_TYPE_MAGICAL,
                          DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
        AddParticleTarget(owner, target, "Ezreal_TrueShot_tar", target, bone: "spine");
        AddBuff("EzrealRisingSpellForce", 6f, 1, spell, owner, owner);
    }
    
    private void OnStatsUpdate(AttackableUnit unit, float diff) {
        var bonusAd = _owner.Stats.AttackDamage.FlatBonus * _spell.SpellData.Coefficient;
        SetSpellToolTipVar(_owner, 0, bonusAd, SpellbookType.SPELLBOOK_CHAMPION, 3, SpellSlotType.SpellSlots);
    }
}