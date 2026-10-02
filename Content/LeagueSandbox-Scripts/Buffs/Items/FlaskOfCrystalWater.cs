using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;
internal class FlaskOfCrystalWater : IBuffGameScript
{
    private Buff _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType= BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks= 5
    };
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _buff = buff;
        StatsModifier.ManaRegeneration.FlatBonus = 100f / 15;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnUpdate(float diff)
    {
        if (_buff.TargetUnit.IsDead)
        {
            RemoveBuff(_buff);
        }
    }
}
