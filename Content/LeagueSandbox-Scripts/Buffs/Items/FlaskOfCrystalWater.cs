using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class FlaskOfCrystalWater : IBuffGameScript {
    private Particle _potion;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_CONTINUE,
        MaxStacks   = 5
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        var caster = ownerSpell.CastInfo.Owner;
        StatsModifier.ManaRegeneration.FlatBonus = 3.33f;
        unit.AddStatModifier(StatsModifier);
        _potion = AddParticleTarget(caster, unit, "GLOBAL_Item_ManaPotion", unit, buff.Duration,
                                    bone: "Buffbone_Glb_Ground_Loc");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { _potion.SetToRemove(); }
}