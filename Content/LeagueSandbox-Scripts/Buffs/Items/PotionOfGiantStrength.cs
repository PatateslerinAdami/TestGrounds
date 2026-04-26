using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class PotionOfGiantStrength : IBuffGameScript {
    private Particle _potion;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_CONTINUE,
        MaxStacks   = 5
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        var owner = ownerSpell.CastInfo.Owner;
        StatsModifier.AttackDamage.FlatBonus = 15f;
        var health = 120.0F + 115.0F / 17.0F * (owner.Stats.Level - 1.0F);
        StatsModifier.HealthPoints.FlatBonus       = health;
        StatsModifier.HealthRegeneration.FlatBonus = health / 180f * 0.5f;
        unit.AddStatModifier(StatsModifier);
        unit.Stats.CurrentHealth += health;
        _potion = AddParticleTarget(owner, unit, "PotionofGiantStrength_itm", unit, buff.Duration,
                                    bone: "C_BUFFBONE_GLB_CENTER_LOC");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { _potion.SetToRemove(); }
}