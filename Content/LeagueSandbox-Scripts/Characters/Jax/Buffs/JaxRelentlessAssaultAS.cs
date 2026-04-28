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
        _jax                                       = ownerspell.CastInfo.Owner;
        StatsModifier.AttackSpeed.PercentBaseBonus = _jax.Stats.Level switch {
            <4 => 0.04f,
            <7 => 0.06f,
            <10 => 0.08f,
            <13 => 0.10f,
            <16 => 1.2f,
            _ => 1.4f
        };
        unit.AddStatModifier(StatsModifier);
        ApiEventManager.OnUpdateStats.AddListener(this, unit, OnUpdateStats);
    }
    
    public void OnUpdate(float diff) {
        
    }

    private void OnUpdateStats(AttackableUnit unit, float diff) {
        var attackSpeed = _jax.Stats.Level switch {
            <4  => 4f,
            <7  => 6f,
            <10 => 8f,
            <13 => 10f,
            <16 => 12f,
            _   => 14
        };
        SetBuffToolTipVar(_buff, 0, attackSpeed);
    }
    
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell spell) {
    }
    
}