using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class RunePrison : ISpellScript {
    private ObjAIBase      _ryze;
    private AttackableUnit _target;
    private Spell          _spell;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        NotSingleTargetSpell = false,
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };


    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ryze = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _ryze, OnUpdateStats);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell) {
        spell.ApplyEffects(_target);
    }
    
    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        var duration = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel];
        AddBuff("RunePrison", duration, 1, spell, _target, _ryze);
        
        var ap       = _ryze.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var mana     = _ryze.Stats.ManaPoints.Total   * spell.SpellData.EffectLevelAmount[3][spell.CastInfo.SpellLevel] / 100f;
        var dmg   = spell.SpellData.EffectLevelAmount[2][spell.CastInfo.SpellLevel] + ap + mana;
        _target.TakeDamage(_ryze, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        
        if (!_ryze.HasBuff("DesperatePower")) return;
        SpellEffectCreate("DesperatePower_aoe.troy", _ryze, target, target, flags: FXFlags.SimulateWhileOffScreen,
            fowVisibilityRadius: 10f);
        var unitsInRange = GetUnitsInRange(_ryze, _target.Position, 300f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectNeutral).Where(unit => unit != _target);
        foreach (var unit in unitsInRange) {
            SpellEffectCreate("ManaLeach_tar.troy",_ryze, unit,  unit, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
            unit.TakeDamage(_ryze, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
        }
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var mana = _ryze.Stats.ManaPoints.Total * _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel] / 100f;
        SetSpellToolTipVar(unit, 0, mana, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }
}