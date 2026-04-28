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

internal class TrundleCircleSlow : IBuffGameScript {
    private AttackableUnit _unit;

    private Particle _slow;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit                              = unit;
        StatsModifier.MoveSpeed.PercentBonus   = -(0.34f + 0.04f * (ownerSpell.CastInfo.SpellLevel - 1));
        _unit.AddStatModifier(StatsModifier);
        _slow  = AddParticleTarget(ownerSpell.CastInfo.Owner, null, "Global_Slow", unit, buff.Duration, bone: "BUFFBONE_GLB_GROUND_LOC");
        ApplyAssistMarker(unit, ownerSpell.CastInfo.Owner, 10.0f);

        // For attack speed and move speed mod changes:
        //ApiEventManager.OnUpdateBuffs.AddListener(this, buff, OnUpdateBuffs, false);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit.RemoveStatModifier(StatsModifier);
        RemoveParticle(_slow);
    }
}
