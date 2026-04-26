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

internal class ItemMonsterBurn : IBuffGameScript {
    private       ObjAIBase      _owner;
    private       AttackableUnit _unit;
    private       Buff           _buff;
    private       float          _damage    = 30f;
    private const float          IntervalMs = 1000f;
    private       PeriodicTicker _periodicTicker;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner = buff.SourceUnit;
        _unit  = unit;
        _buff = buff;
        _damage    = buff.Variables.GetFloat("damageAmount");
    }

    public void OnUpdate(float diff) {
        var ticks = _periodicTicker.ConsumeTicks(diff, IntervalMs, fireImmediately: true, maxTicksPerUpdate: 1);
        if (ticks != 1) return;
        _unit.TakeDamage(_owner, _damage/_buff.Duration, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_PERIODIC,
                         DamageResultType.RESULT_NORMAL);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        
    }
}