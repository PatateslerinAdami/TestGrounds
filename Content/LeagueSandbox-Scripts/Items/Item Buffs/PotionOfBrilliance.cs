using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class PotionOfBrilliance : IBuffGameScript {
    private Particle _potion;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_CONTINUE,
        MaxStacks   = 5
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        var owner = ownerSpell.CastInfo.Owner;
        StatsModifier.AbilityPower.FlatBonus      = 25f + 15f / 17f * (owner.Stats.Level - 1);
        StatsModifier.CooldownReduction.FlatBonus = 0.10f;
        unit.AddStatModifier(StatsModifier);
        _potion = AddParticleTarget(owner, unit, "PotionofBrilliance_itm", unit, buff.Duration,
                                    bone: "C_BUFFBONE_GLB_CENTER_LOC");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { _potion.SetToRemove(); }
}