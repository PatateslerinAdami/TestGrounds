using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;


namespace Buffs;

internal class EzrealRisingSpellForce : IBuffGameScript {
    private ObjAIBase _ezreal;
    private Particle  _particle1;
    private Particle  _particle2;
    private Particle  _particle3;
    private Particle  _particle4;
    private Particle  _particle5;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks = 5
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _ezreal = ownerSpell.CastInfo.Owner;
        StatsModifier.AttackSpeed.PercentBonus = 0.1f;
        buff.SetToolTipVar(0, buff.StackCount * 10f);
        unit.AddStatModifier(StatsModifier);

        if (buff.StackCount <= 0) return;
        switch (buff.StackCount) {
            case 1: _particle1 = AddParticle(_ezreal,          _ezreal,          "Ezreal_glow1",
                                             _ezreal.Position, bone: "L_hand", lifetime: 2500000f);
                break;
            case 2: RemoveParticle(_particle1); 
                _particle2 = AddParticle(_ezreal,          _ezreal,          "Ezreal_glow2",
                                         _ezreal.Position, bone: "L_hand", lifetime: 2500000f);
                break;
            case 3: RemoveParticle(_particle2);
                _particle3 = AddParticle(_ezreal,          _ezreal,          "Ezreal_glow3",
                                         _ezreal.Position, bone: "L_hand", lifetime: 2500000f);
                break;
            case 4: RemoveParticle(_particle3);
                _particle4 = AddParticle(_ezreal,          _ezreal,          "Ezreal_glow4",
                                         _ezreal.Position, bone: "L_hand", lifetime: 2500000f);
                break;
            default: RemoveParticle(_particle5);
                _particle5 = AddParticle(_ezreal,          _ezreal,          "Ezreal_glow5",
                                         _ezreal.Position, bone: "L_hand", lifetime: 2500000f);
                break;
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_particle1);
        RemoveParticle(_particle2);
        RemoveParticle(_particle3);
        RemoveParticle(_particle4);
        RemoveParticle(_particle5);
    }
}
