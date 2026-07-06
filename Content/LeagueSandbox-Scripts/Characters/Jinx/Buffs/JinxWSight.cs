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

public class JinxWSight : IBuffGameScript {
    private ObjAIBase        _jinx;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx = ownerSpell.CastInfo.Owner;

        // The perception bubble below already rendered before the vision rework; the only thing
        // missing was actual vision of the hit unit. RevealSpecificUnit fixes that: it sets the
        // StatusFlag UnitHasVisionOn honors, so the target is visible to this team for 2s.
        unit.RevealSpecificUnit(2f);

        // Restored to the original call that produced a working bubble (LOS on, target-bound).
        AddUnitPerceptionBubble(unit, 400f, 2f, _jinx.Team, false, revealSpecificUnitOnly: unit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}