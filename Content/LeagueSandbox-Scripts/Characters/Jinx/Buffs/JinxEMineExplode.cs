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

internal class JinxEMineExplode : IBuffGameScript {
    private ObjAIBase        _jinx;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.INTERNAL,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx = ownerSpell.CastInfo.Owner;
        PlayAnimation(unit, "Death1", flags: AnimationFlags.Override);
        AddParticleTarget(_jinx, unit, "Jinx_E_Mine_Explosion", unit);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        AddBuff("ExpirationTimer", 1.5f, 1, ownerSpell, unit, _jinx);
    }
}