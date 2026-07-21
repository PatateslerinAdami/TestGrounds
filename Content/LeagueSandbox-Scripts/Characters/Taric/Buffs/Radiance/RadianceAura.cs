using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

internal class RadianceAura : IBuffGameScript {
    // Pulse aura: re-applied every tick by Radiance.cs with a short fixed duration. RENEW_EXISTING so
    // a re-add refreshes the existing instance (Buff.Refresh -> NPC_BuffUpdateCount rt=0, the wire
    // renewal signal) instead of REPLACE re-running OnActivate (which would re-add the StatsModifier)
    // and emitting BuffReplace. Replay-verified: RadianceAura renews via BuffUpdateCount, dur 1.25s.
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.RENEW_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        var allyBonus = 15f + 10f * (ownerSpell.CastInfo.SpellLevel - 1);
        StatsModifier.AttackDamage.FlatBonus = allyBonus;
        StatsModifier.AbilityPower.FlatBonus = allyBonus;
        unit.AddStatModifier(StatsModifier);
    }
}
