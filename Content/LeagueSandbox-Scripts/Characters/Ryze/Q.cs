using System.Linq;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Overload : ISpellScript {
    private ObjAIBase     _ryze;
    public  StatsModifier StatsModifier { get; } = new();

    public SpellScriptMetadata ScriptMetadata => new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true,
        MissileParameters = new MissileParameters {
            Type = MissileType.Target
        }
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ryze = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _ryze, OnUpdateStats);
        ApiEventManager.OnSpellHit.AddListener(this, spell, TargetExecute);
        ApiEventManager.OnLevelUpSpell.AddListener(this, spell, OnLevelUpSpell);
    }

    private void TargetExecute(Spell spell, AttackableUnit target, SpellMissile missile, SpellSector sector) {
        var ap     = _ryze.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var mana   = _ryze.Stats.ManaPoints.Total   * 0.065f;
        var dmg = 40f + 20f * (spell.CastInfo.SpellLevel - 1) + ap + mana;

        AddParticleTarget(_ryze, target, "Overload_tar.troy", target, bone: "root");
        target.TakeDamage(_ryze, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        if (!_ryze.HasBuff("DesperatePower")) return;
        AddParticle(_ryze, target, "DesperatePower_aoe.troy", target.Position);
        var unitsInRange = GetUnitsInRange(_ryze, target.Position, 200f, true,
                                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                           SpellDataFlags.AffectMinions |
                                           SpellDataFlags.AffectNeutral).Where(unit => unit != target);
        foreach (var unit in unitsInRange) {
            AddParticleTarget(_ryze, unit, "ManaLeach_tar", unit);
            unit.TakeDamage(_ryze, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                            DamageResultType.RESULT_NORMAL);
        }
    }

    private void OnLevelUpSpell(Spell spell) {
        AddBuff("Overload", 25000f, 1, spell, _ryze, _ryze, true);
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var mana   = _ryze.Stats.ManaPoints.Total   * 0.065f;
        SetSpellToolTipVar(unit, 0, mana, SpellbookType.SPELLBOOK_CHAMPION, 0, SpellSlotType.SpellSlots);
    }
}