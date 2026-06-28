using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior
{
    /// <summary>
    /// P5 shared auto-attack toggle driver (docs/AI_COMBAT_EXECUTION_SPLIT_PLAN.md). Encapsulates the
    /// "in attack range → TurnOnAutoAttack / past cancel range → TurnOffAutoAttack(Moving)" branch that
    /// is identical across Hero/Minion/Turret/Leashed.lua (TimerCheckAttack / TimerFindEnemies). The host
    /// script owns target SELECTION + state; this component owns only the firing toggle; the engine still
    /// owns per-swing timing (windup → fire → cooldown) once toggled on.
    ///
    /// Attached to every BaseAIScript and is now the SOLE auto-attack driver (P5.6 removed the legacy
    /// engine auto-fire path + the ScriptOwnsAutoAttack compat flag). It is inert when the host has no
    /// enemy target (guarded below), so non-combat AIs (e.g. RiverCrab, which never sets a target) never
    /// fire. Units with no BaseAIScript at all (EmptyAIScript summons = Riot idle.lua) have no component
    /// and so never auto-attack — the faithful passive-summon default.
    /// </summary>
    public class AutoAttackComponent : IAIComponent
    {
        private ObjAIBase _owner;
        private BaseAIScript _ai;

        public void OnAttach(BaseAIScript ai, ObjAIBase owner)
        {
            _ai = ai;
            _owner = owner;
        }

        public void OnUpdate(float diff)
        {
            if (_owner == null || _owner.IsDead)
            {
                return;
            }

            AttackableUnit target = _owner.TargetUnit;

            // No valid enemy target → nothing to fire at. The engine's target-pinning
            // (AutoAttackTogglePermits checks _autoAttackEnabledTarget == TargetUnit) already prevents a
            // stale enable from firing at a different/null target, so we needn't force-off here, and the
            // engine cancels any in-flight windup when TargetUnit goes null.
            if (target == null || target.IsDead || target.Team == _owner.Team)
            {
                return;
            }

            // State-gated auto-attack (option C): only fire when the brain's CURRENT state is an attacking
            // state (Riot: Hero.lua TimerCheckAttack swings only in attack states, Turret.lua in
            // HARDIDLE_ATTACKING, …). Default permits everything (behaviour-neutral) until an archetype
            // overrides AutoAttackStatePermits with its own attacking-state set. Gate the turn-ON only —
            // leaving an attack state stops being attack-eligible here, while an in-flight windup is left to
            // finish / be cancelled by the existing range + target-null paths.
            if (_ai != null && !_ai.AutoAttackStatePermits())
            {
                return;
            }

            // Edge-based attack range (Range + both collision radii) — the same expansion the engine's
            // swing gate uses. In range → keep auto-attack on (idempotent); past the larger cancel range
            // → off. Between the two = hysteresis: leave the toggle as-is so a committed windup is not
            // churned at the range boundary (Riot TargetInAttackRange vs TargetInCancelAttackRange).
            float edge = _owner.Stats.Range.Total + target.CollisionRadius + _owner.CollisionRadius;
            float distSq = Vector2.DistanceSquared(_owner.Position, target.Position);

            if (distSq <= edge * edge)
            {
                _owner.TurnOnAutoAttack(target);
            }
            else
            {
                float cancel = edge + LeagueSandbox.GameServer.Content.GlobalData.AttackRangeVariables.StopAttackRangeModifier;
                if (distSq > cancel * cancel)
                {
                    _owner.TurnOffAutoAttack(AutoAttackStopReason.Moving);
                }
            }
        }
    }
}
