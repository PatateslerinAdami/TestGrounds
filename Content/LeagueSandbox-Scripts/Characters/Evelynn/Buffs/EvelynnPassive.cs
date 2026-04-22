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

internal class EvelynnPassive : IBuffGameScript {
    private ObjAIBase        _evelynn;
    private Spell    _spell;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _evelynn = ownerSpell.CastInfo.Owner;
        _spell   = ownerSpell;
        if (_evelynn.HasBuff("EvelynnStealthMarker")) {
            RemoveBuff(_evelynn, "EvelynnStealthMarker");
        }
        PushCharacterFade(_evelynn, 1, 1f); 
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        AddBuff("EvelynnStealthMarker", 25000f, 1, _spell, _evelynn, _evelynn, true);
    }
}