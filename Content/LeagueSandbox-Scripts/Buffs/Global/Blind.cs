using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class Blind : IBuffGameScript {
    private Particle _blind;
    
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.BLIND,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; }

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _blind = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "LOC_Blind ", unit, buff.Duration, bone: "head");
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { RemoveParticle(_blind); }
}

