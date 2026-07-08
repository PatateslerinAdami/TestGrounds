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

internal class FleeSlow : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; private set; } = new();
    
    
    private Particle _particle;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        StatsModifier.MoveSpeed.PercentBonus = -0.9f;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        
    }
}
