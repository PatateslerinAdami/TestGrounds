using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

internal class VayneNightHunter : IBuffGameScript {
    private const float NightHunterMoveSpeedBonus            = 30.0f;
    private const float NightHunterMoveSpeedInquisitionBonus = 90.0f;
    private float          _currentMoveSpeedBonus;
    private AttackableUnit _unit;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.HASTE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit = unit;
        ApplyMoveSpeedBonus(unit, GetDesiredMoveSpeedBonus(unit));
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit = null;
    }

    public void OnUpdate(float diff) {
        if (_unit == null) {
            return;
        }

        var desiredMoveSpeedBonus = GetDesiredMoveSpeedBonus(_unit);
        if (Math.Abs(desiredMoveSpeedBonus - _currentMoveSpeedBonus) <= float.Epsilon) {
            return;
        }

        _unit.RemoveStatModifier(StatsModifier);
        ApplyMoveSpeedBonus(_unit, desiredMoveSpeedBonus);
    }

    private static float GetDesiredMoveSpeedBonus(AttackableUnit unit) {
        return unit.HasBuff("VayneInquisition") ? NightHunterMoveSpeedInquisitionBonus : NightHunterMoveSpeedBonus;
    }

    private void ApplyMoveSpeedBonus(AttackableUnit unit, float moveSpeedBonus) {
        StatsModifier.MoveSpeed.FlatBonus = moveSpeedBonus;
        unit.AddStatModifier(StatsModifier);
        _currentMoveSpeedBonus = moveSpeedBonus;
    }
}
