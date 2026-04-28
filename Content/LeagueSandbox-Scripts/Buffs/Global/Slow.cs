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

internal class Slow : IBuffGameScript {
    private ObjAIBase _owner;
    private AttackableUnit _unit;

    private Particle _slow;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.SLOW,
        BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
        MaxStacks   = 10
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _unit    = unit;
        _owner = ownerSpell.CastInfo.Owner;
        var movementSlowAmount = buff.Variables.GetFloat("slowPercent");
        var attackSpeedSlowAmount = buff.Variables.GetFloat("attackSpeedSlowAmount");
        StatsModifier.MoveSpeed.PercentBonus   -= movementSlowAmount;
        StatsModifier.AttackSpeed.PercentBonus -= attackSpeedSlowAmount;
        _unit.AddStatModifier(StatsModifier);
        _slow  = AddParticleTarget(ownerSpell.CastInfo.Owner, null, "Global_Slow", unit, buff.Duration, bone: "BUFFBONE_GLB_GROUND_LOC");
        ApplyAssistMarker(unit, ownerSpell.CastInfo.Owner, 10.0f);

        // For attack speed and move speed mod changes:
        //ApiEventManager.OnUpdateBuffs.AddListener(this, buff, OnUpdateBuffs, false);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) { RemoveParticle(_slow); }
}