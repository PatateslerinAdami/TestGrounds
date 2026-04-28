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
        BuffType = BuffType.COMBAT_ENCHANCER
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _annie = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        var bonus = 20f + 10f * (ownerSpell.CastInfo.SpellLevel - 1);

        StatsModifier.Armor.FlatBonus       = bonus;
        StatsModifier.MagicResist.FlatBonus = bonus;
        unit.AddStatModifier(StatsModifier);
        
        ApiEventManager.OnTakeDamage.AddListener(this, unit, OnBeingHit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.OnBeingHit.RemoveListener(this);
    }

    public void OnUpdate(float diff) { }

    private void OnBeingHit(DamageData data) {
        if (!IsValidTarget(_annie, data.Attacker,
                           SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                           SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral)) return;
        var ap     = _annie.Stats.AbilityPower.Total * _spell.SpellData.Coefficient;
        var damage = 20f + 10f * (_spell.CastInfo.SpellLevel - 1) + ap;
            
        AddParticleTarget(_annie, data.Attacker, "AnnieSparks", data.Attacker);
        data.Attacker.TakeDamage(_annie, damage, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_ATTACK, false);
    }
}