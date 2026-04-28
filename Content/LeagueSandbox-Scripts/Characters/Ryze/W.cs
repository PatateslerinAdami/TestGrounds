using System.Linq;
using System.Numerics;
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

public class runeprison : ISpellScript {
    private ObjAIBase      _ryze;
    private AttackableUnit _target;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts = true,
        IsDamagingSpell    = true
    };


    public void OnActivate(ObjAIBase owner, Spell spell) {
        _ryze = owner;
        
        ApiEventManager.OnUpdateStats.AddListener(this, _ryze, OnUpdateStats);
    }

    public void OnSpellPreCast(ObjAIBase owner, Spell spell, AttackableUnit target, Vector2 start, Vector2 end) {
        _target = target;
    }

    public void OnSpellPostCast(Spell spell) {
        var ap       = _ryze.Stats.AbilityPower.Total * spell.SpellData.Coefficient;
        var mana     = _ryze.Stats.ManaPoints.Total   * 0.046f;
        var dmg   = 60 * spell.CastInfo.SpellLevel + ap + mana;
        var duration = 0.75f                          + 0.25f * (spell.CastInfo.SpellLevel - 1);
        AddParticleTarget(_ryze, _target, "RunePrison_tar.troy", _target);
        AddBuff("RunePrison", duration, 1, spell, _target, _ryze);
        _target.TakeDamage(_ryze, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELL, false);
        if (!_ryze.HasBuff("DesperatePower")) return;
        AddParticle(_ryze, _target, "DesperatePower_aoe.troy", _target.Position);
        var unitsInRange = GetUnitsInRange(_ryze, _target.Position, 200f, true,
                                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                           SpellDataFlags.AffectMinions |
                                           SpellDataFlags.AffectNeutral).Where(unit => unit != _target);
        foreach (var unit in unitsInRange) {
            AddParticleTarget(_ryze, unit, "ManaLeach_tar", unit);
            unit.TakeDamage(_ryze, dmg * 0.5f, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                            DamageResultType.RESULT_NORMAL);
        }
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var mana = _ryze.Stats.ManaPoints.Total * 0.046f;
        SetSpellToolTipVar(unit, 0, mana, SpellbookType.SPELLBOOK_CHAMPION, 1, SpellSlotType.SpellSlots);
    }
}