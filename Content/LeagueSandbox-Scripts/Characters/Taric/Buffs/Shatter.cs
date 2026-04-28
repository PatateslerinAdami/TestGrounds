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

internal class Shatter : IBuffGameScript {
    private ObjAIBase _taric;
    private Particle _shatterParticle1, _shatterParticle2;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SHRED,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _taric = ownerSpell.CastInfo.Owner;
        //_shatterParticle1              = AddParticleTarget(owner, unit, "ShatterReady_buf", unit, -1);
        _shatterParticle1 = AddParticleTarget(_taric, unit, "Shatter_tar", unit, buff.Duration);
        _shatterParticle2 = AddParticleTarget(_taric, unit, "BloodSlash", unit, buff.Duration);
        StatsModifier.Armor.FlatBonus = -buff.Variables.GetFloat("armorReduction");
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_shatterParticle1);
        RemoveParticle(_shatterParticle2);
    }
}
