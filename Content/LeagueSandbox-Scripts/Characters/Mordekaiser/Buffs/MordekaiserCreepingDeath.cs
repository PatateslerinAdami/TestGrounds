using System;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Missile;
using LeagueSandbox.GameServer.GameObjects.SpellNS.Sector;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class MordekaiserCreepingDeath : IBuffGameScript
{
    private ObjAIBase _mordekaiser;
    private AttackableUnit _unit;
    private Particle _buffParticle;
    private Particle _buffParticle2;
    private PeriodicTicker _periodicTicker;
    private float _dmg;

    public BuffScriptMetaData BuffMetaData { get; } = new()
    {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _mordekaiser = ownerSpell.CastInfo.Owner;
        _unit = unit;

        var ap = _mordekaiser.Stats.AbilityPower.Total * ownerSpell.SpellData.Coefficient;
        _dmg = 24 + (14 * _mordekaiser.GetSpell("MordekaiserCreepingDeathCast").CastInfo.SpellLevel - 1) + ap;

        var resistances = 10f + 5f * (_mordekaiser.GetSpell("MordekaiserCreepingDeathCast").CastInfo.SpellLevel - 1);
        StatsModifier.Armor.FlatBonus = resistances;
        StatsModifier.MagicResist.FlatBonus = resistances;

        unit.AddStatModifier(StatsModifier);
        if (unit == _mordekaiser)
        {
            var buffParticleName = _mordekaiser.SkinID switch
            {
                1 => "mordekaiser_creepingDeath_auraGold",
                2 => "mordekaiser_creepingDeath_auraRed",
                _ => "mordekaiser_creepingDeath_aura"
            };
            _buffParticle2 = AddParticleTarget(_mordekaiser, unit, buffParticleName, unit, buff.Duration);
        }
        else
        {
            var buffParticleName = _mordekaiser.SkinID switch
            {
                1 => "mordekaiser_creepingDeath_auraGold",
                2 => "mordekaiser_creepingDeath_auraRed",
                _ => "mordekaiser_creepingDeath_tar"
            };
            _buffParticle = AddParticleTarget(_mordekaiser, unit, buffParticleName, unit, buff.Duration);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_buffParticle);
        RemoveParticle(_buffParticle2);
    }

    public void OnUpdate(float diff)
    {
        var ticks = _periodicTicker.ConsumeTicks(diff, 1000f, true, 1, 6);
        if (ticks <= 0) return;
        foreach (var unit in GetUnitsInRange(_mordekaiser, _unit.Position, 250, true,
                     SpellDataFlags.AffectEnemies | SpellDataFlags.AffectHeroes | SpellDataFlags.AffectMinions |
                     SpellDataFlags.AffectNeutral))
        {
            unit.TakeDamage(_mordekaiser, _dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_SPELLAOE,
                DamageResultType.RESULT_NORMAL);
        }
    }
}