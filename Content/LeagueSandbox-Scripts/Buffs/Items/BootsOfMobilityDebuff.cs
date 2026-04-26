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

internal class BootsOfMobilityDebuff : IBuffGameScript {
    private ObjAIBase _owner;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _owner = ownerSpell.CastInfo.Owner;
        RemoveBuff(unit, "BootsOfMobilityBuff");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        AddBuff("BootsOfMobilityBuff", 25000f, 1, ownerSpell, unit, _owner, true);
    }
}