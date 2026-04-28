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

internal class SoulShackles : IBuffGameScript {
    private ObjAIBase _morgana;
    private Region    _bubble;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _morgana       = ownerSpell.CastInfo.Owner;
        StatsModifier.MoveSpeed.PercentBonus = -0.2f * ownerSpell.CastInfo.SpellLevel;
        unit.AddStatModifier(StatsModifier);
        _bubble = AddUnitPerceptionBubble(unit, unit.Stats.Size.Total, buff.Duration, _morgana.Team, true, unit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        unit.RemoveStatModifier(StatsModifier);
        _bubble?.SetToRemove();
    }
}
