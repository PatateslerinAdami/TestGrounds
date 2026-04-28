using System.Numerics;
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

internal class ZedDashCloneMaker : IBuffGameScript {
    private Minion    _shadow1;
    private Minion    _shadow2;
    private Minion    _shadow3;

    private Buff _buff;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _buff  = buff;
    }

    public void AddShadow1(Minion shadow1) {
        _shadow1 = shadow1;
    }

    public void AddShadow2(Minion shadow2) {
        _shadow2 = shadow2;
    }
    
    public void AddShadow3(Minion shadow3) {
        _shadow3 = shadow3;
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        AddBuff("ExpirationTimer", 1f, 1, ownerSpell, _shadow1, _shadow1);
        AddBuff("ExpirationTimer", 1f, 1, ownerSpell, _shadow2, _shadow2);
        AddBuff("ExpirationTimer", 1f, 1, ownerSpell, _shadow3, _shadow3);
    }
}
