using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class MoltenShield : IBuffGameScript {
    private ObjAIBase _annie;
    private Spell     _spell;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _annie = buff.SourceUnit;
        _spell = ownerSpell;
        var bonus = ownerSpell.SpellData.EffectLevelAmount[2][_spell.CastInfo.SpellLevel];
        StatsModifier.Armor.FlatBonus       = bonus;
        StatsModifier.MagicResist.FlatBonus = bonus;
        unit.AddStatModifier(StatsModifier);
        
        ApiEventManager.OnBeingHit.AddListener(this, unit, OnBeingHit);
    }
    
    private void OnBeingHit(AttackableUnit unit, AttackableUnit attacker) {
        if (!IsValidTarget(_annie, attacker,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var ap     = _annie.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var damage = _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] + ap;
        
        SpellEffectCreate("AnnieSparks.troy", _annie, attacker, _annie, scale: 1f, flags: FXFlags.SimulateWhileOffScreen);
        attacker.TakeDamage(_annie, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.OnBeingHit.RemoveListener(this, _annie, OnBeingHit);
    }
}