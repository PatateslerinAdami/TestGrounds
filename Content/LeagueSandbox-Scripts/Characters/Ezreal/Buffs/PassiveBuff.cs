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
    private Particle _particle1;
    private Particle _particle2;
    private Particle _particle3;
    private Particle _particle4;
    private Particle _particle5;
    //a band-aid solution for the tooltip for now.
    private int _lastStackCount = 0;
    private Buff _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks = 5
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff = buff;
        ObjAIBase owner = ownerSpell.CastInfo.Owner;
        StatsModifier.AttackSpeed.PercentBonus = 0.1f;
        unit.AddStatModifier(StatsModifier);

        if (buff.StackCount > 0) {
            switch (buff.StackCount) {
                case 1: _particle1 = AddParticle(owner,owner, "Ezreal_glow1",
                                                owner.Position, bone: "L_hand", lifetime: 5f);
                    break;
                case 2: RemoveParticle(_particle1); 
                    _particle2 = AddParticle(owner,owner, "Ezreal_glow2",
                                             owner.Position, bone: "L_hand", lifetime: 5f);
                    break;
                case 3: RemoveParticle(_particle2);
                    _particle3 = AddParticle(owner, owner, "Ezreal_glow3",
                                                owner.Position, bone: "L_hand", lifetime: 5f);
                    break;
                case 4: RemoveParticle(_particle3);
                    _particle4 = AddParticle(owner, owner, "Ezreal_glow4",
                                                owner.Position, bone: "L_hand", lifetime: 5f);
                    break;
                default: RemoveParticle(_particle5);
                    _particle5 = AddParticle(owner, owner, "Ezreal_glow5",
                                                 owner.Position, bone: "L_hand", lifetime: 5f);
                    break;
            }
        } 
    }
    public void OnUpdate(float diff)
    {
        if (_buff != null && _buff.StackCount != _lastStackCount)
        {
            UpdateToolTip();
        }
    }
    private void UpdateToolTip()
    {
        _lastStackCount = _buff.StackCount;
        _buff.SetToolTipVar(0, _buff.StackCount * 10f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_particle1);
        RemoveParticle(_particle2);
        RemoveParticle(_particle3);
        RemoveParticle(_particle4);
        RemoveParticle(_particle5);
    }
}
