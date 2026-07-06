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

// The actual airborne/knockup applied to ENEMIES hit by Aatrox Q at landing (replay 663eda09:
// BuffType KNOCKUP, hash 185404368, name "AatroxQKnockup"). The two self-state buffs are AatroxQ
// (ascend) and AatroxQDescent (dive) — see those scripts.
internal class AatroxQKnockup : IBuffGameScript {
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.KNOCKUP,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ForceMove(unit, GetRandomPointInArea(unit.Position, 10f, 10f), 8.5f, 20f, ForceMovementType.FURTHEST_WITHIN_RANGE, ForceMovementOrdersFacing.KEEP_CURRENT_FACING, true, false, ForceMovementOrdersType.CANCEL_ORDER);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { }
}
