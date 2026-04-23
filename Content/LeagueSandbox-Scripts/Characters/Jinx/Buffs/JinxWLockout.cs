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

public class 
    JinxWLockout : IBuffGameScript {
    private ObjAIBase        _jinx;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx = ownerSpell.CastInfo.Owner;
        _jinx.StopMovement();
        _jinx.UpdateMoveOrder(OrderType.Stop);
        SetStatus(_jinx, StatusFlags.Rooted, true);
        SetStatus(_jinx, StatusFlags.CanAttack, false);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        SetStatus(_jinx, StatusFlags.Rooted, false);
        SetStatus(_jinx, StatusFlags.CanAttack, true);
        _jinx.StopMovement();
        _jinx.UpdateMoveOrder(OrderType.Stop);
    }
}