using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ShatterSelfBonus : IBuffGameScript {
    private ObjAIBase _taric;
    private Particle _shatterParticle;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _taric = ownerSpell.CastInfo.Owner;
        _shatterParticle = AddParticleTarget(_taric, unit, "ShatterReady_buf", unit, buff.Duration);
        StatsModifier.Armor.FlatBonus = 10f + 5f * (ownerSpell.CastInfo.SpellLevel - 1);
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_shatterParticle);
    }
}
