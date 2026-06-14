using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using System.Numerics;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

// Flee = Riot AI_FLEEING: the unit runs directly away from the source. Same buff-as-flag / AI-as-mover
// model as Fear: the buff raises the Feared status flag and records the source + flee flavour on the
// unit; the shared CrowdControlComponent drives the run-away movement. The buff only drives movement
// itself as a legacy fallback for units without an AI crowd-control driver (e.g. a player champion
// still on EmptyAIScript). Differs from Fear (RandomDirection=false) only by BuffType + particle.
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

    private const float LegacyFleeDistance = 1000f;

    private AttackableUnit _unit;
    private ObjAIBase _owner;
    private Particle _particle;

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _unit = unit;
        _owner = buff.SourceUnit;
        _particle = AddParticleTarget(_owner ?? unit, unit, "Global_Fear.troy", unit, buff.Duration);

        // Feared is DERIVED from BuffType.FLEE (AttackableUnit.RecomputeBuffEffects) — overlap-safe.

        // Record the CC source + flee flavour (never wander) so the AI-driven CrowdControlComponent
        // drives the run-away movement (Riot AI_FLEEING).
        if (unit is ObjAIBase cc)
        {
            cc.CrowdControlSource = _owner;
            cc.CrowdControlWander = false;

            // Migration bridge: only drive the movement here for units WITHOUT an AI crowd-control
            // driver. Units with the CrowdControlComponent (minions today) get it from the AI layer.
            if (!cc.AICrowdControlActive)
            {
                DriveFlee();
            }
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

    public void OnUpdate(float diff)
    {
        if (_unit == null || _unit.IsDead) return;

        if (_unit is ObjAIBase ai)
        {
            // AI-driven units have the CrowdControlComponent re-issuing the flee; the buff only keeps
            // driving here for the legacy (non-AI-driven) path.
            if (ai.AICrowdControlActive) return;

            if (ai.IsPathEnded())
            {
                DriveFlee();
            }
        }
    }

    private void DriveFlee()
    {
        if (_unit is ObjAIBase ai && _owner != null)
        {
            var dir = Vector2.Normalize(_unit.Position - _owner.Position);
            if (float.IsNaN(dir.X) || float.IsNaN(dir.Y)) dir = new Vector2(1, 0);

            var targetPos = _unit.Position + dir * LegacyFleeDistance;
            var path = GetPath(_unit.Position, targetPos);
            ai.SetWaypoints(path);
            ai.UpdateMoveOrder(OrderType.MoveTo);
        }
    }
}
