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

internal class ItemCrystalFlask : IBuffGameScript {
    private       AttackableUnit      _unit;
    private const float          Health     = 120f;
    private const float          Mana       = 60f;
    private const float          IntervalMs = 1000f;
    private       PeriodicTicker _periodicTicker;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit = unit;
    }

    public void OnUpdate(Buff buff, float diff) {
        var ticks = _periodicTicker.ConsumeTicks(diff, IntervalMs, fireImmediately: true, maxTicksPerUpdate: 1, maxTotalTicks: 12);
        if (ticks != 1) return;
        _unit.TakeHeal(_unit, Health  /12f, HealType.HealthRegeneration);
        _unit.IncreasePAR(_unit, Mana /12f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        
    }
}
