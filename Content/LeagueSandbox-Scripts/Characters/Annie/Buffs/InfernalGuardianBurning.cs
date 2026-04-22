using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class InfernalGuardianBurning : IBuffGameScript {
    private       ObjAIBase _annie;
    private       Spell     _spell;
    private       Pet       _pet;
    private       float     _timer  = 0f;
    private const float     MaxTime = 1000f;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_ENCHANCER
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _annie = ownerSpell.CastInfo.Owner;
        _spell = ownerSpell;
        if (unit is not Pet pet) return;
        _pet = pet;

        StatsModifier.Armor.FlatBonus        = 20.0f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.MagicResist.FlatBonus  = 20f   * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.AttackDamage.FlatBonus = 20f   * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.HealthPoints.FlatBonus = 900f  * (ownerSpell.CastInfo.SpellLevel - 1);
        pet.AddStatModifier(StatsModifier);
        pet.Stats.CurrentHealth = pet.Stats.HealthPoints.Total;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        unit.Die(CreateDeathData(false,                                  0, unit, unit, DamageType.DAMAGE_TYPE_TRUE,
                                 DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
        RemoveBuff(buff.SourceUnit, "InfernalGuardianTimer");
    }

    public void OnUpdate(float diff) {
        _timer += diff;
        if (!(_timer >= MaxTime)) return;
        if (_pet == null) return;
        var enemiesInRange = GetUnitsInRange(_annie, _pet.Position, 350f, true,
                                             SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes |
                                             SpellDataFlags.AffectMinions | SpellDataFlags.AffectNeutral);
        var ap          = _annie.Stats.AbilityPower.Total * _spell.SpellData.Coefficient2;
        var totalDamage = 35.0f + ap;
        foreach (var enemy in enemiesInRange) {
            enemy.TakeDamage(_pet.Owner, totalDamage, DamageType.DAMAGE_TYPE_MAGICAL,
                             DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
        }

        _timer = 0f;
    }
}