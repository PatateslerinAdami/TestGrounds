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

internal class ItemSmiteAoE : IBuffGameScript {
    private       ObjAIBase      _owner;
    private       AttackableUnit _unit;
    private       float          _mana      = 3f;
    private       float          _health    = 7f;
    private const float          IntervalMs = 500f;
    private       PeriodicTicker _periodicTicker;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HEAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner  = buff.SourceUnit;
        _unit   = unit;
        _health = buff.Variables.GetFloat("healthAmount");
        _mana   = buff.Variables.GetFloat("manaAmount");
    }

    public void OnUpdate(float diff) {
        var ticks = _periodicTicker.ConsumeTicks(diff, IntervalMs, fireImmediately: true, maxTicksPerUpdate: 1);
        if (ticks != 1) return;
        _unit.TakeHeal(_owner, _health, HealType.SelfHeal);
        _unit.IncreasePAR(_unit, _mana);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        
    }
}