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

internal class JinxEMineSnare : IBuffGameScript {
    private ObjAIBase        _jinx;
    private Particle _haste;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SNARE,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx = ownerSpell.CastInfo.Owner;
        AddBuff("JinxEMineVision", 1.5f, 1, ownerSpell, unit, _jinx);
        switch (_jinx.SkinID) {
            default: _haste = AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Jinx_E_Mine_Debuff", unit,
                                                buff.Duration,             bone: "BUFFBONE_GLB_GROUND_LOC"); break;
        }
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_haste);
    }
}
