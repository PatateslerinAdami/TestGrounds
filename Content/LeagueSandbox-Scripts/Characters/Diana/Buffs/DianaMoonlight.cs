using AIScripts;
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

public class DianaMoonlight : IBuffGameScript {
    private ObjAIBase _diana;
    private Region _bubbleRegion;
    private Particle _moonlightParticle;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _diana = ownerSpell.CastInfo.Owner;
        _moonlightParticle = AddParticleTarget(_diana, unit, "Diana_Base_Q_Moonlight.troy", unit, buff.Duration);
        _bubbleRegion = AddUnitPerceptionBubble(unit, 480f, buff.Duration, _diana.Team, revealSpecificUnitOnly: unit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        RemoveParticle(_moonlightParticle);
        _bubbleRegion.SetToRemove();
    }
}
