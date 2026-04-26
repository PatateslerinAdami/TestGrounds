using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

internal class RadianceAura : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        var allyBonus = 15f + 10f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.AttackDamage.FlatBonus = allyBonus;
        StatsModifier.AbilityPower.FlatBonus = allyBonus;
        unit.AddStatModifier(StatsModifier);
    }
}
