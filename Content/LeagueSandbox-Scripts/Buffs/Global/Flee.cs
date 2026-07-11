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

// Flee = Riot AI_FLEEING: the unit runs directly away from the source. Same buff-as-flag / AI-as-mover
// model as Fear: the buff raises the Feared status flag and records the source + flee flavour on the
// unit; the shared CrowdControlComponent (auto-attached to every BaseAIScript) drives the run-away
// movement. Differs from Fear (RandomDirection=false) only by BuffType + particle.
internal class Flee : IBuffGameScript
{
    public BuffScriptMetaData BuffMetaData { get; set; } = new()
    {
        BuffType = BuffType.FLEE,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; private set; } = new();

    // Optional move-speed change while fleeing (fraction, e.g. 0.3 = -30%). Default 0 = no slow.
    public float slowPercent = 0f;

    private AttackableUnit _unit;
    private ObjAIBase _owner;
    private Particle _particle;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _unit = unit;
        _owner = buff.SourceUnit;
        _particle = SpellEffectCreate("LOC_fear.troy",_owner, unit,  unit, lifetime: buff.Duration, boneName: "C_Buffbone_Glb_Head_Loc");

        // Feared is DERIVED from BuffType.FLEE (AttackableUnit.RecomputeBuffEffects) — overlap-safe.

        // Record the CC source + flee flavour (never wander) so the AI-driven CrowdControlComponent
        // drives the run-away movement (Riot AI_FLEEING).
        if (unit is ObjAIBase cc)
        {
            cc.CrowdControlSource = _owner;
            cc.CrowdControlWander = false;
        }

        ApplyAssistMarker(unit, _owner, 10.0f);
        StatsModifier.MoveSpeed.PercentBonus = -slowPercent;
        unit.AddStatModifier(StatsModifier);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        if (_particle != null) _particle.SetToRemove();

        if (unit is ObjAIBase cc)
        {
            cc.CrowdControlSource = null;
        }
        unit.StopMovement();
    }
}
