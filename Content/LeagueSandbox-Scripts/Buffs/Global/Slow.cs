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

        // Refresh instead of stack for the same source: if a Slow effect from the same caster already exists on the target,
        // only its duration is refreshed, and this new buff ends immediately (so no particles or modifiers).
        var existingSlows = unit.GetBuffsWithName("Slow");
        for (var i = 0; i < existingSlows.Count; i++) {
            var existing = existingSlows[i];
            if (existing == buff) continue;
            if (existing.SourceUnit == _owner) {
                existing.Refresh();
                buff.SetToExpired();
                return;
            }
        }

        var movementSlowAmount = buff.Variables.GetFloat("slowPercent");
        var attackSpeedSlowAmount = buff.Variables.GetFloat("attackSpeedSlowAmount");
        StatsModifier.MoveSpeed.PercentBonus   -= movementSlowAmount;
        StatsModifier.AttackSpeed.PercentBonus -= attackSpeedSlowAmount;
        _unit.AddStatModifier(StatsModifier);
        // Particle infinite (lifetime < 0), so that it survives a refresh; manual cleanup in OnDeactivate.
        _slow  = AddParticleTarget(ownerSpell.CastInfo.Owner, null, "Global_Slow", unit, -1f, bone: "BUFFBONE_GLB_GROUND_LOC");
        ApplyAssistMarker(unit, ownerSpell.CastInfo.Owner, 10.0f);

        // For attack speed and move speed mod changes:
        //ApiEventManager.OnUpdateBuffs.AddListener(this, buff, OnUpdateBuffs, false);
    }

    // Removes the slow particle. Null check because no particle was spawned on the refresh path.
    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        if (_slow != null) RemoveParticle(_slow);
    }
}