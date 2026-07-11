using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

public class JaxRelentlessAssaultAS  : IBuffGameScript {
    private ObjAIBase _jax;
    private Buff      _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks   = 6
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerspell) {
        _buff                                      = buff;
        _jax                                       = buff.SourceUnit;
        StatsModifier.AttackSpeed.PercentBaseBonus = _jax.Stats.Level switch {
            <4 => 0.04f,
            <7 => 0.06f,
            <10 => 0.08f,
            <13 => 0.10f,
            <16 => 1.2f,
            _ => 1.4f
        };
        unit.AddStatModifier(StatsModifier);
    }
}