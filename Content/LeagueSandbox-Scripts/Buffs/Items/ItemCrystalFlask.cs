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
    private       ObjAIBase      _owner;
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
        _owner = ownerSpell.CastInfo.Owner;
    }

    public void OnUpdate(float diff) {
        var ticks = _periodicTicker.ConsumeTicks(diff, IntervalMs, fireImmediately: true, maxTicksPerUpdate: 1, maxTotalTicks: 12);
        if (ticks != 1) return;
        _owner.TakeHeal(_owner, Health  /12f, HealType.HealthRegeneration);
        _owner.IncreasePAR(_owner, Mana /12f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        
    }
}
