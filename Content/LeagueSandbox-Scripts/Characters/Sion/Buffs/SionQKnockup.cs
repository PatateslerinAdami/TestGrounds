using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using GameMaths;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace Buffs;

public class SionQKnockUp : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; } = new BuffScriptMetaData
    {
        BuffType = BuffType.KNOCKUP,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1
    };

    public StatsModifier StatsModifier { get; private set; } = new StatsModifier();

    private Spell _spell;
    private Buff _buff;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _spell = ownerSpell;


        ApiEventManager.OnMoveEnd.AddListener(this, unit, OnMoveEnd);

        const float knockPath = 10f;
        const float knockGravity = 20f;
        float knockupTime = buff.BuffVars.GetFloat("KnockupTime", 0.5f);
        var bouncePos = GetRandomPointInAreaUnit(unit, 10, 10f);
        ForceMove(unit, bouncePos,
            knockPath / knockupTime, gravity: knockGravity,
            facing: ForceMovementOrdersFacing.KEEP_CURRENT_FACING, orders: ForceMovementOrdersType.CANCEL_ORDER,
            resolve: ForceMovementType.FURTHEST_WITHIN_RANGE, idealDistance: 10f,
            movementName: "SionQKnockUp");
    }

    private void OnMoveEnd(AttackableUnit unit, ForceMovementParameters parameters)
    {
        if (parameters.MovementName != "SionQKnockUp") return;
        var stunDuration = _buff.BuffVars.GetFloat("StunTail", 0.75f);
        if (!unit.IsDead && stunDuration > 0f)
        {
            AddBuff("Stun", stunDuration, 1, _spell, unit, _spell.CastInfo.Owner);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}