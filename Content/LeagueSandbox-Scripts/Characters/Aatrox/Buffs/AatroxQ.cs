using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class AatroxQ : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.KNOCKUP,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        CancelDash(unit);
        ForceMovement(unit, "RUN", new Vector2(unit.Position.X + 6f, unit.Position.Y + 6f), 10f, 10f, 20f, 0, movementType: ForceMovementType.FURTHEST_WITHIN_RANGE);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }

    public void OnUpdate(float diff) { }
}
