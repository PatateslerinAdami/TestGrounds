using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior
{
    /// <summary>
    /// Shared crowd-control movement driver, attached by default to every <see cref="BaseAIScript"/>
    /// (minion, champion HeroAI, monster, pet). Mirrors Riot's DefaultFear/DefaultFlee handlers in
    /// Hero.lua / Aggro.lua: the CC buff only raises the status flag and records the source on the
    /// unit (<see cref="ObjAIBase.CrowdControlSource"/>), the engine surfaces the flag transition as
    /// an <see cref="AIEvent"/>, and this component drives the actual wander / flee movement.
    ///
    /// Handles Fear (wander), Flee, Taunt (walk to + attack the taunter) and Charm (pull toward the
    /// charmer). Every BaseAIScript carries one, so all unit types — minions, monsters, pets, and
    /// champions (HeroAI) — get CC movement from this single component; the CC buffs are pure flag +
    /// source setters with no movement of their own.
    /// </summary>
    public class CrowdControlComponent : IAIComponent
    {
        // Riot AIComponentDefaultFearBehavior / DefaultFleeBehavior constants. Both default CC
        // components re-issue on a 0.5s timer (TimerFeared / TimerFlee). NOTE: the random-wander
        // mechanic was disabled in patch 3.13, so on our 4.20 target Fear and Flee are effectively
        // identical (both run directly away). The wander branch below is retained for completeness
        // but is not exercised by 4.20 content.
        private const float FEAR_WANDER_DISTANCE = 500f;
        private const float FLEE_RUN_DISTANCE = 2000f;
        private const float WANDER_REISSUE_INTERVAL = 0.5f;  // TimerFeared
        private const float FLEE_REISSUE_INTERVAL = 0.5f;    // TimerFlee
        private const float CHARM_REISSUE_INTERVAL = 0.5f;   // fallback cadence (charmer stationary)
        // Charm re-paths as soon as the charmer drifts this far, so the pull tracks the charmer's
        // CURRENT position. Replay-derived (1fbb603a, 21 charm events): the charmed unit's repaths
        // are movement-driven (NOT a fixed timer) — short when the charmer moves (~1-2 ticks),
        // long when it pauses — with a ~150ms median. 50u ≈ that median at champion move speed
        // (~340u/s); 75u lagged at ~220ms. Drift-based, matching how the engine tracks moving targets.
        private const float CHARM_REPATH_DRIFT = 50f;

        private enum Mode { None, Wander, Flee, Charm }

        private BaseAIScript _ai;
        private ObjAIBase _owner;
        private Mode _mode = Mode.None;
        private Vector2 _leashPoint;
        private Vector2 _lastDriveSourcePos;
        private float _reissueInterval;
        private float _sinceReissue;

        public void OnAttach(BaseAIScript ai, ObjAIBase owner)
        {
            _ai = ai;
            _owner = owner;

            ai.Subscribe(AIEvent.OnFearBegin, OnFearBegin);
            ai.Subscribe(AIEvent.OnFearEnd, OnFearEnd);
            ai.Subscribe(AIEvent.OnTauntBegin, OnTauntBegin);
            ai.Subscribe(AIEvent.OnCharmBegin, OnCharmBegin);
            ai.Subscribe(AIEvent.OnCharmEnd, OnCharmEnd);
        }

        public void OnUpdate(float diff)
        {
            if (_mode == Mode.None || _owner == null || _owner.IsDead)
            {
                return;
            }

            // Re-issue on the Riot timer cadence, and also whenever the current path ran out so the
            // unit keeps moving for the whole CC duration (mirrors Fear.cs's path-ended re-drive).
            _sinceReissue += diff / 1000f;
            bool reissue = _sinceReissue >= _reissueInterval || _owner.IsPathEnded();

            // Charm tracks the charmer's CURRENT position: re-path the moment the charmer drifts, so
            // the pull follows a moving charmer tightly rather than lagging the reissue interval.
            if (_mode == Mode.Charm)
            {
                AttackableUnit charmer = _owner.CrowdControlSource;
                if (charmer != null
                    && Vector2.DistanceSquared(charmer.Position, _lastDriveSourcePos) > CHARM_REPATH_DRIFT * CHARM_REPATH_DRIFT)
                {
                    reissue = true;
                }
            }

            if (reissue)
            {
                Drive();
            }
        }

        public void OnDetach()
        {
            _mode = Mode.None;
        }

        private void OnFearBegin(AttackableUnit _)
        {
            // CrowdControlWander distinguishes Riot's AI_FEARED (wander around the leash point) from
            // AI_FLEEING (run straight away from the source). The buff sets it alongside the flag.
            // (On 4.20 wander is disabled, so this is Flee in practice — see the class header.)
            _mode = _owner.CrowdControlWander ? Mode.Wander : Mode.Flee;
            _leashPoint = _owner.Position; // GetFearLeashPoint: wander around where we were feared.
            _reissueInterval = _mode == Mode.Wander ? WANDER_REISSUE_INTERVAL : FLEE_REISSUE_INTERVAL;
            _sinceReissue = 0f;

            // Riot DefaultFear/DefaultFleeBehavior both SetRoamState(RUN_IN_FEAR) on begin: a unit
            // that is fleeing/feared must not acquire targets (LaneMinionAI.CanAggro gates on
            // RoamState == Hostile). Restored to Hostile on end.
            if (_owner is Minion minion)
            {
                minion.RoamState = MinionRoamState.RunInFear;
            }

            // Riot DefaultFear/DefaultFleeBehavior call TurnOffAutoAttack(STOPREASON_IMMEDIATELY):
            // drop the current attack AND the target. Without clearing the target, ObjAIBase.UpdateTarget
            // keeps pathing toward the enemy to reach attack range (that branch runs even with
            // MoveOrder == MoveTo) and fights the flee movement — the "unit with auto-attack on won't
            // run away" bug. Client orders are already blocked while feared (CanIssueMoveOrders), so
            // the target stays cleared for the duration.
            _owner.CancelAutoAttack(reset: true, fullCancel: true, respectWindupLock: true);
            _owner.SetTargetUnit(null);

            Drive();
        }

        private void OnFearEnd(AttackableUnit _)
        {
            _mode = Mode.None;

            // Riot DefaultFear/DefaultFleeBehavior OnFearEnd -> SetRoamState(HOSTILE). The concrete
            // AI's own OnFearEnd handler (e.g. LaneMinionAI -> FindTargetOrMove) then re-acquires.
            if (_owner is Minion minion)
            {
                minion.RoamState = MinionRoamState.Hostile;
            }
        }

        // Riot DefaultTauntBehavior: walk to AND attack the taunter. This is NOT a fear — the
        // auto-attack stays ON (CanAttack permits AI_TAUNTED) and RoamState stays HOSTILE. We just
        // set the target + AttackTo once; ObjAIBase.UpdateTarget keeps chasing it as it moves, so
        // there is no component re-issue loop (no _mode is set — OnUpdate stays idle).
        private void OnTauntBegin(AttackableUnit _)
        {
            // Fear takes priority over taunt (Riot guards taunt on state != AI_FEARED).
            if (_mode != Mode.None)
            {
                return;
            }

            // Riot AIComponentNonAggressiveTauntBehavior (the Scuttle Crab registers this instead of
            // the default): a non-aggressive unit does NOT run to + attack the taunter — it just stops
            // (ClearTarget + StopMove + WanderPause). The unit's own wander/patrol is already gated on
            // its CC state, so it stays paused for the taunt and resumes on end with no extra handler.
            if (_ai.NonAggressiveTaunt)
            {
                _owner.CancelAutoAttack(reset: true, fullCancel: true, respectWindupLock: true);
                _owner.SetTargetUnit(null);
                _owner.StopMovement();
                return;
            }

            AttackableUnit taunter = _owner.CrowdControlSource;
            if (taunter == null || taunter.IsDead)
            {
                return;
            }

            if (_owner is Minion minion)
            {
                minion.RoamState = MinionRoamState.Hostile;
            }

            _ai.SetStateAndCloseToTarget(AIState.AI_TAUNTED, taunter);
        }

        // Charm: the unit is pulled toward the champion that charmed it and cannot act. Unlike taunt
        // it does NOT attack (CanAttack is false while Charmed), so drop the attack + target and drive
        // a move toward the charmer, re-issued each cadence so it tracks a moving charmer. (Hero.lua's
        // OnCharmBegin is state-only; the toward-charmer pull is driven here as component movement.)
        private void OnCharmBegin(AttackableUnit _)
        {
            _mode = Mode.Charm;
            _reissueInterval = CHARM_REISSUE_INTERVAL;
            _sinceReissue = 0f;
            _owner.CancelAutoAttack(reset: true, fullCancel: true, respectWindupLock: true);
            _owner.SetTargetUnit(null);
            Drive();
        }

        private void OnCharmEnd(AttackableUnit _)
        {
            _mode = Mode.None;
        }

        private void Drive()
        {
            _sinceReissue = 0f;
            AttackableUnit source = _owner.CrowdControlSource;

            switch (_mode)
            {
                case Mode.Wander:
                {
                    Vector2 wander = _ai.MakeWanderPoint(_leashPoint, FEAR_WANDER_DISTANCE);
                    _ai.SetStateAndMove(AIState.AI_FEARED, wander);
                    break;
                }
                case Mode.Flee:
                {
                    // Riot AI_FLEEING: run directly away from the source. If the source is gone — the
                    // flee buff ended (OnDeactivate clears CrowdControlSource + StopMovement before the
                    // flag-poll fires OnFearEnd, so we get one more re-drive tick with a null source),
                    // or the source died mid-flee — the flee is over: end the mode and stay put. Do NOT
                    // wander toward _leashPoint (the pre-flee position) — that ran the unit roughly back
                    // to where it was feared instead of stopping at the end of the run.
                    if (source == null || source.IsDead)
                    {
                        _mode = Mode.None;
                        break;
                    }
                    _ai.SetStateAndMove(AIState.AI_FLEEING, _ai.MakeFleePoint(source.Position, FLEE_RUN_DISTANCE));
                    break;
                }
                case Mode.Charm:
                {
                    // Walk toward the charmer (the unit is pulled to the champion that charmed it).
                    // Record the position we pathed to so OnUpdate can re-path once the charmer drifts.
                    // No source -> nothing to walk to.
                    if (source != null && !source.IsDead)
                    {
                        _lastDriveSourcePos = source.Position;
                        _ai.SetStateAndMove(AIState.AI_CHARMED, source.Position);
                    }
                    break;
                }
            }
        }
    }
}
