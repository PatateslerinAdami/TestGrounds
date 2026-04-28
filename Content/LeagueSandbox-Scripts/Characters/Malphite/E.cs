using System.Linq;
using System.Numerics;
using Buffs;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Spells;

public class Landslide : ISpellScript {
    private ObjAIBase _malphite;

    public SpellScriptMetadata ScriptMetadata { get; } = new() {
        TriggersSpellCasts   = true,
        NotSingleTargetSpell = true,
    };

    public void OnActivate(ObjAIBase owner, Spell spell) {
        _malphite = owner;
        ApiEventManager.OnUpdateStats.AddListener(this, _malphite, OnUpdateStats);
    }

    public void OnSpellPostCast(Spell spell) {
        
        AddParticlePos(_malphite, "landslide_nova", _malphite.Position, _malphite.Position, default);
        ApplyAreaDamage(spell, _malphite);
    }
    
    private void ApplyAreaDamage( Spell spell, AttackableUnit target) {
        var units = GetUnitsInRange(_malphite, _malphite.Position, 400, true, SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions| SpellDataFlags.AffectNeutral);
        foreach (var unit in units) {
            var ap         = _malphite.Stats.Armor.Total        * 0.3f;
            var armor      = _malphite.Stats.AbilityPower.Total * 0.2f;
            var baseDamage = 60f   + 40f + (spell.CastInfo.SpellLevel - 1);
            var damage = baseDamage + ap + armor;
            
            unit.TakeDamage(_malphite, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            AddBuff("LandslideDebuff", 4f, 1, spell, unit, _malphite);
        }
    }
    
    private void OnUpdateStats(AttackableUnit malphite, float diff) {
        var ap    = _malphite.Stats.Armor.Total        * 0.3f;
        var armor = _malphite.Stats.AbilityPower.Total * 0.2f;
        SetSpellToolTipVar(malphite, 0, ap, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
        SetSpellToolTipVar(malphite, 1, armor, SpellbookType.SPELLBOOK_CHAMPION, 2, SpellSlotType.SpellSlots);
    }
}