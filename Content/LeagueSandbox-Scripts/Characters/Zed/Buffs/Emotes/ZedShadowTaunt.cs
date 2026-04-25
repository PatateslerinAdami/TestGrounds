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

internal class ZedShadowTaunt : IBuffGameScript {
    private ObjAIBase _zed;
    private Minion    _minion;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _zed = ownerSpell.CastInfo.Owner;
        var pos       = GetPointFromUnit(_zed, 10f,  20f);
        var facingPos = GetPointFromUnit(_zed, 100f, 20f);
        _minion = AddMinion(_zed, "ZedShadow", "ZedShadow", pos, _zed.Team, _zed.SkinID, true, false);
        FaceDirection(facingPos, _minion, true);
        PlayAnimation(_minion, "Taunt_SH", timeScale: 10f);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (_minion == null || _minion.IsDead || _minion.IsToRemove()) return;
        AddBuff("ExpirationTimer", 1.5f, 1, ownerSpell, _minion, _minion);
        _minion = null;
    }
}
