using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class MalphiteShieldEffect : IBuffGameScript {
    private ObjAIBase         _malphite;
    private float     _shieldHealth;

    private Shield _graniteShield;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.AURA,
        BuffAddType = BuffAddType.RENEW_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _malphite = ownerSpell.CastInfo.Owner;
        _shieldHealth  = _malphite.Stats.HealthPoints.Total * 0.1f;
        _graniteShield = new Shield(_malphite, _malphite, true, true, _shieldHealth);
        unit.AddShield(_graniteShield);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        AddBuff("MalphiteShieldRemoval", 3f, 1, ownerSpell, _malphite, _malphite, true);
        RemoveShield(_malphite, _graniteShield);
    }
}