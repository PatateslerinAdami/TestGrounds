using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace Buffs;

public class AkaliShadowDance : IBuffGameScript {
    private Buff  _buff;
    private Spell _ownerSpell;
    private int   _lastDisplayedAmmo = -1;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.STACKS_AND_RENEWS,
        MaxStacks   = 3
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff      = buff;
        _ownerSpell = ownerSpell;
        UpdateDisplay();
    }

    public void OnUpdate(float diff) {
        UpdateDisplay();
    }

    private void UpdateDisplay() {
        if (_buff == null) {
            return;
        }

        var ammo = 0;
        if (_ownerSpell != null && _ownerSpell.CastInfo.SpellLevel > 0) {
            ammo = _ownerSpell.CurrentAmmo;
        }

        ammo = Math.Clamp(ammo, 0, _buff.MaxStacks);
        if (ammo == _lastDisplayedAmmo) {
            return;
        }

        _buff.SetToolTipVar(0, ammo);
        _lastDisplayedAmmo = ammo;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}
