using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

internal class MordekaiserCOTGSelf : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        var ghost = buff.SourceUnit.GetPet();

        var adBonus = ghost.ClonedUnit.Stats.AttackDamage.Total * 0.2f;
        var apBonus = ghost.ClonedUnit.Stats.AbilityPower.Total * 0.2f;

        StatsModifier.AttackDamage.FlatBonus = adBonus;
        StatsModifier.AbilityPower.FlatBonus = apBonus;

        unit.AddStatModifier(StatsModifier);

        buff.SetToolTipVar(0, adBonus);
        buff.SetToolTipVar(1, apBonus);
    }
}