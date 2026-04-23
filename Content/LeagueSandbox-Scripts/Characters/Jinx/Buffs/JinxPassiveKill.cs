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

internal class JinxPassiveKill  : IBuffGameScript {
    private const float BaseMoveSpeedBonus   = 1.75f;
    private const float BaseAttackSpeedBonus = 0.25f;

    private ObjAIBase _jinx;
    private AttackableUnit _unit;
    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx = ownerSpell.CastInfo.Owner;
        _unit = unit;
        _buff = buff;
        StatsModifier.MoveSpeed.PercentBonus   = BaseMoveSpeedBonus;
        StatsModifier.AttackSpeed.PercentBonus = BaseAttackSpeedBonus;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }

    public void OnUpdate(float diff) {
        if (_buff.Duration <= 0.0f) return;

        var remainingMultiplier = 1.0f - (_buff.TimeElapsed / _buff.Duration);
        if (remainingMultiplier < 0.0f) remainingMultiplier = 0.0f;

        _unit.RemoveStatModifier(StatsModifier);
        StatsModifier.MoveSpeed.PercentBonus   = BaseMoveSpeedBonus * remainingMultiplier;
        StatsModifier.AttackSpeed.PercentBonus = BaseAttackSpeedBonus * remainingMultiplier;
        _unit.AddStatModifier(StatsModifier);
    }
}
