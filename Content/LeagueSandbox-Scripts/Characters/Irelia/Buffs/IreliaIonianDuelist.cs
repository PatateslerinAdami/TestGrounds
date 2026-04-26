using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

public class IreliaIonianDuelist : IBuffGameScript {
    private Buff  _buff;
    private float _lastTooltipAmount = -1.0f;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks   = 3,
        IsHidden    = false
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff = buff;
        UpdateTooltip();
    }

    public void OnUpdate(float diff) {
        UpdateTooltip();
    }

    private void UpdateTooltip() {
        if (_buff == null) {
            return;
        }

        var amount = Math.Clamp(_buff.StackCount, 0, 3) switch {
            1    => 10.0f,
            2    => 25.0f,
            >= 3 => 40.0f,
            _    => 0.0f
        };

        if (Math.Abs(amount - _lastTooltipAmount) < 0.001f) {
            return;
        }
        
        _buff.SetToolTipVar(0, amount);
        _buff.SetToolTipVar(1, amount);
        _lastTooltipAmount = amount;
    }
}
