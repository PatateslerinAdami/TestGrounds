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

internal class VayneSilverDebuff : IBuffGameScript {
    private ObjAIBase _vayne;
    private Particle  _p, _p1;
    
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks   = 3
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _vayne = ownerSpell.CastInfo.Owner;
        switch (buff.StackCount) {
            case 1:  _p  = AddParticleTarget(_vayne, unit, "vayne_W_ring1", unit, buff.Duration); break;
            case 2:  _p1 = AddParticleTarget(_vayne, unit, "vayne_W_ring2", unit, buff.Duration); break;
            default:
                break;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_p);
        RemoveParticle(_p1);
    }
}
