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

public class Overload : ISpellScript
{
    private ObjAIBase _ryze;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = true,
        TriggersSpellCasts = true,
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Target
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ryze = owner;
        _spell = spell;
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
        ApiEventManager.OnUpdateStats.AddListener(this, _ryze, OnUpdateStats);
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
    }

    private void OnLevelUpSpell(Spell spell)
    {
        AddBuff("Overload", 25000f, 1, spell, _ryze, _ryze, true);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        SpellEffectCreate("Overload_tar.troy", _ryze, target, target, boneName: "C_Buffbone_Glb_Chest_Loc",
            targetBoneName: "C_Buffbone_Glb_Chest_Loc", flags: FXFlags.SimulateWhileOffScreen);

        var ap = _ryze.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var mana = _ryze.Stats.ManaPoints.Total * spell.SpellData.EffectLevelAmount[3][spell.CastInfo.SpellLevel] /
                   100f;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap + mana;
        target.TakeDamage(_ryze, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);

        if (!_ryze.HasBuff("DesperatePower")) return;
        SpellEffectCreate("DesperatePower_aoe.troy", _ryze, target, target, flags: FXFlags.SimulateWhileOffScreen,
            fowVisibilityRadius: 10f);
        var unitsInRange = GetUnitsInRange(_ryze, target.Position, 300f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral |
            SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes).Where(unit => unit != target);
        foreach (var unit in unitsInRange)
        {
            SpellEffectCreate("ManaLeach_tar.troy",_ryze, unit,  unit, flags: FXFlags.SimulateWhileOffScreen, fowVisibilityRadius: 10f);
            unit.TakeDamage(_ryze, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
        }
    }

    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        var mana = _ryze.Stats.ManaPoints.Total * _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel] /
                   100f;
        SetSpellToolTipVar(unit, 0, mana, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}