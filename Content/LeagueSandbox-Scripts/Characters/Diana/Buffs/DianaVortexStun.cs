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

internal class DianaVortexStun : IBuffGameScript
{
    private ObjAIBase _diana;
    private Spell _spell;
    private Region _bubble;

    public BuffScriptMetaData BuffMetaData { get; set; } = new BuffScriptMetaData
    {
        BuffType = BuffType.KNOCKUP,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks = 1,
        IsNonDispellable = false
    };

    public StatsModifier StatsModifier { get; private set; }

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _diana = buff.SourceUnit;
        _spell = ownerSpell;
        AddParticleTarget(_diana, unit, "Diana_Base_E_Tar.troy", unit);
        _bubble = AddUnitPerceptionBubble(unit, 480f, buff.Duration, _diana.Team, revealSpecificUnitOnly: unit);
        var toDiana = _diana.Position - unit.Position;
        float dist = toDiana.Length();
        float pullDist = System.MathF.Min(225f, dist);
        var dest = unit.Position + Vector2.Normalize(toDiana) * pullDist;
        ApiEventManager.OnMoveEnd.AddListener(this, unit, OnMoveEnd);
        ForceMove(unit, dest, 455f, ownerSpell.SpellData.EffectLevelAmount[3][_spell.CastInfo.SpellLevel], ForceMovementType.FURTHEST_WITHIN_RANGE,
            ForceMovementOrdersFacing.KEEP_CURRENT_FACING, orders: ForceMovementOrdersType.CANCEL_ORDER,
            idealDistance: 225f, movementName: "dianaVortexStun");
    }

    private void OnMoveEnd(AttackableUnit unit, ForceMovementParameters parameters)
    {
        if (parameters.MovementName != "dianaVortexStun") return;
        var variables = new VariableTable();
        variables.Set("slowPercent", _spell.SpellData.EffectLevelAmount[1][_spell.CastInfo.SpellLevel] / 100);
        AddBuff("Slow", 2f, 1, _spell, unit, _diana, variableTable: variables);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _bubble.SetToRemove();
        ApiEventManager.RemoveAllListenersForOwner(this);
    }
}