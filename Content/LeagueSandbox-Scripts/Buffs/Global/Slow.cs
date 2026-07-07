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

internal class Slow : IBuffGameScript
{
    private ObjAIBase _owner;
    private AttackableUnit _unit;
    private Particle _slow;

    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.SLOW,
        BuffAddType = BuffAddType.STACKS_AND_OVERLAPS,
        MaxStacks = 10,
        IsHidden = false
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _unit = unit;

        // Null-safe for item passives such as Rylai that pass originSpell = null.
        _owner = ownerSpell?.CastInfo?.Owner ?? buff.SourceUnit as ObjAIBase;

        var movementSlowAmount = buff.BuffVars.GetFloat("slowPercent");

        if (movementSlowAmount < 0.0f)
        {
            movementSlowAmount = -movementSlowAmount;
        }
        
        StatsModifier.MoveSpeed.PercentBonus -= movementSlowAmount;
        _unit.AddStatModifier(StatsModifier);

        var particleCaster = _owner ?? unit;

        _slow = AddParticleTarget(
            particleCaster,
            null,
            "Global_Slow",
            unit,
            -1f,
            bone: "BUFFBONE_GLB_GROUND_LOC"
        );

        if (_owner != null)
        {
            ApplyAssistMarker(unit, _owner, 10.0f);
        }
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
            RemoveParticle(_slow);
    }
}
