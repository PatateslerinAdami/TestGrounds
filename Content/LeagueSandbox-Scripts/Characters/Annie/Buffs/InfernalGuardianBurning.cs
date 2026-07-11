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

internal class InfernalGuardianBurning : IBuffGameScript
{
    private ObjAIBase _annie;
    private Buff _buff;
    private Spell _spell;
    private Pet _pet;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _annie = buff.SourceUnit;
        _buff = buff;
        _spell = ownerSpell;
        
        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath, true);
        
        if (unit is not Pet pet) return;
        _pet = pet;

        // The effect tables carry Tibbers' TOTAL stats per R level (Effect2 = health 1200/2100/3000,
        // Effect3 = attack damage 80/105/130, Effect5 = magic resist 30/50/70); the replay shows Riot
        // keeps the char-data base and applies the difference as a flat modifier one tick after spawn.
        // Armor has no effect column of its own but tracks the MR totals exactly on the wire (30->70).
        var effects = ownerSpell.SpellData.EffectLevelAmount;
        var level = ownerSpell.CastInfo.SpellLevel;
        StatsModifier.Armor.FlatBonus = effects[5][level] - pet.Stats.Armor.BaseValue;
        StatsModifier.MagicResist.FlatBonus = effects[5][level] - pet.Stats.MagicResist.BaseValue;
        StatsModifier.AttackDamage.FlatBonus = effects[3][level] - pet.Stats.AttackDamage.BaseValue;
        StatsModifier.HealthPoints.FlatBonus = effects[2][level] - pet.Stats.HealthPoints.BaseValue;
        pet.AddStatModifier(StatsModifier);
        pet.Stats.CurrentHealth = pet.Stats.HealthPoints.Total;
    }

    private void OnDeath(DeathData data)
    {
        RemoveBuff(_buff.SourceUnit, "InfernalGuardianBurning");
    }


    public void OnUpdate(Buff buff, float diff)
    {
        ExecutePeriodically(buff.BuffVars, "infernalGuardianBurningTick", 1000f, false, 0, () =>
        {
            var ap = _annie.Stats.AbilityPower.Total * _spell.SpellData.Coefficient2;
            var totalDamage = _spell.SpellData.EffectLevelAmount[4][_spell.CastInfo.SpellLevel] + ap;
            var enemiesInRange = GetUnitsInRange(_annie, _pet.Position, 350f, true,
                SpellDataFlags.AffectEnemies | SpellDataFlags.AffectNeutral
                                             | SpellDataFlags.AffectMinions | SpellDataFlags.AffectHeroes);
            foreach (var enemy in enemiesInRange)
            {
                enemy.TakeDamage(_pet.Owner, totalDamage, DamageType.DAMAGE_TYPE_MAGICAL,
                    DamageSource.DAMAGE_SOURCE_SPELLAOE, false);
            }
        });
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.OnDeath.RemoveListener(this, unit, OnDeath);
        if (!unit.IsDead)
        {
            unit.Die(CreateDeathData(false, 0, unit, unit, DamageType.DAMAGE_TYPE_TRUE,
                DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
        }

        RemoveBuff(buff.SourceUnit, "InfernalGuardianTimer");
    }
}