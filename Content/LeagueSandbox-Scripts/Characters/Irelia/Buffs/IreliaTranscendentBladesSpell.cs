using System;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class IreliaTranscendentBladesSpell : IBuffGameScript {
    private ObjAIBase _irelia;
    private Buff _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        MaxStacks   = 4
    };
    
    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff = buff;
        _irelia = ownerSpell.CastInfo.Owner;
    }

    public void OnUpdate(float diff) {
        if (_buff.StackCount > 0) return;
        _irelia.GetBuffWithName("IreliaTranscendentBlades").DeactivateBuff();
        _buff.DeactivateBuff();
    }
}