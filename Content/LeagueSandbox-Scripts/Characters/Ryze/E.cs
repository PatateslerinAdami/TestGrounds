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

public class SpellFlux : ISpellScript
{
    private ObjAIBase _ryze;
    private Spell _spell;

    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = false,
        TriggersSpellCasts = true,
        DoesntBreakShields = false,
        CastingBreaksStealth = true,
        IsDamagingSpell = true,
        MissileParameters = new MissileParameters
        {
            Type = MissileType.Chained,
            BounceSelection = BounceSelection.Random,
            BounceSpellNameEnemy = "spellfluxmissile",
            MaximumHits = 5,
            CanHitSameTargetConsecutively = false,
            CanHitSameTarget = true,
            CanHitCaster = true,
            CanHitEnemies = true,
            CanHitFriends = false,
        },
    };

    public void OnActivate(ObjAIBase owner, Spell spell)
    {
        _ryze = owner;
        _spell = spell;
        ApiEventManager.OnUpdateStats.AddListener(this, _ryze, OnUpdateStats);
        ApiEventManager.OnSpellHit.AddListener(this, spell, OnSpellHit);
    }

    private void OnSpellHit(Spell spell, AttackableUnit target, SpellMissile missile)
    {
        if (!IsValidTarget(_ryze, target,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral | SpellDataFlags.AffectHeroes |
                SpellDataFlags.AffectMinions)) return;

        SpellEffectCreate("SpellFlux_tar2.troy", _ryze, target, target, flags: FXFlags.SimulateWhileOffScreen,
            fowVisibilityRadius: 10f);

        var ap = _ryze.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var mana = _ryze.Stats.ManaPoints.Total * spell.SpellData.EffectLevelAmount[3][spell.CastInfo.SpellLevel] /
                   100f;
        var dmg = spell.SpellData.EffectLevelAmount[1][spell.CastInfo.SpellLevel] + ap + mana;
        target.TakeDamage(_ryze, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
            DamageResultType.RESULT_NORMAL);

        AddBuff("SpellFlux", 5f, 1, spell, target, _ryze);

        if (!_ryze.HasBuff("DesperatePower")) return;
        SpellEffectCreate("DesperatePower_aoe.troy", _ryze, target, target, flags: FXFlags.SimulateWhileOffScreen,
            fowVisibilityRadius: 10f);
        var unitsInRange = GetUnitsInRange(_ryze, target.Position, 300f, true,
            SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
            SpellDataFlags.AffectMinions |
            SpellDataFlags.AffectNeutral).Where(unit => unit != target);
        foreach (var unit in unitsInRange)
        {
            SpellEffectCreate("ManaLeach_tar.troy", _ryze, unit, unit, flags: FXFlags.SimulateWhileOffScreen,
                fowVisibilityRadius: 10f);

            unit.TakeDamage(_ryze, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);

            AddBuff("SpellFlux", 5f, 1, spell, target, _ryze);
        }
    }

    private void OnUpdateStats(AttackableUnit unit, float diff)
    {
        var mana = _ryze.Stats.ManaPoints.Total * _spell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel] /
                   100f;
        SetSpellToolTipVar(unit, 0, mana, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }
}

public class SpellFluxMissile : ISpellScript
{
    public SpellScriptMetadata ScriptMetadata => new()
    {
        NotSingleTargetSpell = false,
        DoesntBreakShields = false,
        IsDamagingSpell = true,
        PersistsThroughDeath = true
    };
}