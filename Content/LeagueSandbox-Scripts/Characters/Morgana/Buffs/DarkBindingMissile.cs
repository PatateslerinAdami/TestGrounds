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

internal class DarkBindingMissile : IBuffGameScript {
    private ObjAIBase _morgana;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _morgana       = ownerSpell.CastInfo.Owner;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}