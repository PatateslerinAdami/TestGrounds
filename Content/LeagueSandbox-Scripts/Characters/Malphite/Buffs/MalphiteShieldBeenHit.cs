using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net.Repository.Hierarchy;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class MalphiteShieldBeenHit : IBuffGameScript {
    private ObjAIBase _malphite;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.RENEW_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _malphite = ownerSpell.CastInfo.Owner;
        RemoveBuff(_malphite, "MalphiteShieldEffect");
        RemoveBuff(_malphite, "MalphiteShield");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        AddBuff("MalphiteShieldEffect", 25000f, 1, ownerSpell, _malphite, _malphite, true);
        AddBuff("MalphiteShield",       25000f, 1, ownerSpell,      _malphite, _malphite, true);
    }
}