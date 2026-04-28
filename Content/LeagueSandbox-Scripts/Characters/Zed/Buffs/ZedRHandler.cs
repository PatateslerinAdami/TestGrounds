using System.Numerics;
using GameMaths;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class ZedRHandler : IBuffGameScript {
    private ObjAIBase _zed;
    private Minion    _shadow;
    private Spell     _zedUlt;
    private Spell     _zedR2;
    private Buff      _buff;
    private Spell     _spell;
    private byte      _level;
    private bool      _currentShadowSwapped;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff          = buff;
        _zed           = ownerSpell.CastInfo.Owner;
        _spell         = ownerSpell;
        _zedUlt = _zed.GetSpell("ZedUlt");
        _level         = _zedUlt.CastInfo.SpellLevel;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (!_currentShadowSwapped) SetSpellShadowDash();
    }

    public void SpawnShadow(Vector2 position, Vector2 facingPoint) {
        _currentShadowSwapped = false;
        _shadow = AddMinion(_zed, "ZedShadow", "ZedWShadow", position, _zed.Team, _zed.SkinID, true, false, false,
                            SpellDataFlags.NonTargetableAll, null, true, true);
        FaceDirection(facingPoint, _shadow, true);
        var shadowTrackerBuff = _zed.GetBuffWithName("ZedShadowHandler");
        if (shadowTrackerBuff == null) {
            shadowTrackerBuff = AddBuff("ZedShadowHandler", 10f, 1, _spell, _zed, _zed, true);
        }
        var zedShadowHandler  = shadowTrackerBuff?.BuffScript as ZedShadowHandler;
        zedShadowHandler?.AddRShadow(_shadow);
        //AddParticleTarget(_zed, null, "Zed_Base_W_tar.troy", _shadow, lifetime: 1f);
    }

    public void Swap() {
        var zedPosition = _zed.Position;
        TeleportTo(_zed, _shadow.Position.X, _shadow.Position.Y);
        TeleportTo(_shadow, zedPosition.X, zedPosition.Y);
        AddParticleTarget(_zed, null, "Zed_CloneSwap.troy", _shadow);
        AddParticleTarget(_zed, null, "Zed_CloneSwap.troy", _zed);
        SetSpellShadowDash();
        _currentShadowSwapped = true;
    }

    public void CancelBeforeDash() {
        var shadowTrackerBuff = _zed.GetBuffWithName("ZedShadowHandler");
        var zedShadowHandler  = shadowTrackerBuff?.BuffScript as ZedShadowHandler;
        zedShadowHandler?.RemoveRShadow();
        _shadow = null;

        _currentShadowSwapped = true;
        SetSpellShadowDash(0.5f);
    }

    private void SetSpellShadowDash(float? cooldownOverride = null) {
        SetSpell(_zed, "ZedUlt", SpellSlotType.SpellSlots, 3);
        _zedUlt = _zed.GetSpell("ZedUlt");
        _zedUlt.SetLevel(_level);
        _zedUlt.SetSpellToggle(false);
        _zedUlt.SetCooldown(cooldownOverride ?? _zed.GetSpell("ZedUlt").GetCooldown(), true);
        _buff.DeactivateBuff();
    }

    public void SetSpellUltShadowSwap() {
        _level = _zedUlt.CastInfo.SpellLevel;
        SetSpell(_zed, "ZedR2", SpellSlotType.SpellSlots, 3);
        _zedR2         = _zed.GetSpell("ZedR2");
        _zedR2.SetLevel(_level);
        _zedR2.SetCooldown(0f, true);
        _zedR2.SetSpellToggle(true);
    }
}
