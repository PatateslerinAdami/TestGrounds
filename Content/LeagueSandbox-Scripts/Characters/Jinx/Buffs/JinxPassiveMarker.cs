using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class JinxPassiveMarker : IBuffGameScript {
    private ObjAIBase        _jinx;
    private AttackableUnit _unit;
    private Spell _spell;
    private Buff _buff;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.COMBAT_DEHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx  = ownerSpell.CastInfo.Owner;
        _unit  = unit;
        _spell = ownerSpell;
        _buff = buff;
        ApiEventManager.OnDeath.AddListener(this, unit, OnDeath);
    }

    public void OnDeath(DeathData data) {
        AddBuff("JinxPassiveKill", 5f, 1, _spell, _jinx, _jinx);
        _buff.DeactivateBuff();
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
    }
}