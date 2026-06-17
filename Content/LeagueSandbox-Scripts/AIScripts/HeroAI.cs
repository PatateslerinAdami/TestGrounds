using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;

namespace AIScripts
{
    // Minimal champion AI — the Riot Hero.lua equivalent, stripped to what we actually need today.
    // A player champion is order-driven: the AI must NOT acquire targets or move on its own (the
    // player does that through orders). Its only job is to exist as a BaseAIScript so the shared
    // CrowdControlComponent (auto-attached by BaseAIScript) drives fear/flee movement for champions
    // exactly like every other unit type. Before this, champions ran EmptyAIScript and their CC was
    // buff-driven (the Fear.cs/Flee.cs legacy path) — the divergence from Riot's uniform model.
    //
    // Bots keep their own AI script (Champion only falls back to HeroAI when none was given), so this
    // does not touch bot behaviour. Taunt/Charm are not component-handled yet, so they keep their
    // existing mechanism; only fear/flee migrate here. See project_cc_model_architecture.
    public class HeroAI : BaseAIScript
    {
        protected override void OnActivateBehavior()
        {
            // When crowd control ends the player resumes control via orders (no AI re-acquire, unlike
            // minions). Reset the logical state label so a champion is never left tagged AI_FEARED.
            // The movement during CC is the CrowdControlComponent's; Fear.cs/Flee.cs StopMovement on
            // buff end. Fear and Charm clear the target on begin, so resetting the state label suffices.
            Subscribe(AIEvent.OnFearEnd, _ => NetSetState(AIState.AI_IDLE));
            Subscribe(AIEvent.OnCharmEnd, _ => NetSetState(AIState.AI_IDLE));
            // Taunt is different: the component's OnTauntBegin SET the taunter as our target + AttackTo
            // chase (fear/charm instead CLEAR the target on begin). Just resetting the label would leave
            // that chase running, so the champion would keep attacking the ex-taunter after the taunt
            // ends. Release the target so the player regains control — with the Auto Attack option on,
            // OnTick re-acquires the nearest enemy next tick; with it off, the champion idles.
            Subscribe(AIEvent.OnTauntEnd, _ =>
            {
                Owner.CancelAutoAttack(reset: true, fullCancel: true);
                Owner.SetTargetUnit(null, true);
                NetSetState(AIState.AI_IDLE);
            });

            // Order/State split Phase 2: the champion brain owns combat SELECTION (Hero.lua
            // TimerDistanceScan): idle auto-acquire + attack-move acquire / soft-drop / resume. The
            // matching engine UpdateTarget blocks are skipped for HeroAI units; the engine keeps only
            // the EXECUTION (chase the set target to range + auto-attack).
            Owner.ScriptOwnsCombatSelection = true;

            NetSetState(AIState.AI_IDLE);
        }

        // Champion combat selection (Hero.lua TimerDistanceScan), relocated from ObjAIBase.UpdateTarget.
        // Runs before UpdateTarget in the tick, so a target acquired here is chased the same tick.
        protected override void OnTick(float diff)
        {
            var c = Owner;
            if (c == null || c.IsDead)
            {
                return;
            }

            // Soft-drop: a SOFTATTACK (attack-move-acquired) target that left acquisition range is
            // dropped, so the unit resumes the attack-move rather than chasing forever. A right-click
            // HARDATTACK target is NOT dropped (that is the hard chase).
            if (CurrentState == AIState.AI_SOFTATTACK && c.TargetUnit != null && !c.TargetUnit.IsDead)
            {
                float acqSq = c.Stats.AcquisitionRange.Total * c.Stats.AcquisitionRange.Total;
                if (Vector2.DistanceSquared(c.Position, c.TargetUnit.Position) > acqSq)
                {
                    c.CancelAutoAttack(reset: true, fullCancel: true);
                    c.SetTargetUnit(null, true);
                }
            }

            // Resume the attack-move after a soft target was lost (still SOFTATTACK + a destination to
            // walk to): re-path to the destination and drop to AI_ATTACKMOVESTATE so the scan below
            // re-acquires along the way.
            if (c.TargetUnit == null && c.AttackMoveDestination != Vector2.Zero
                && CurrentState == AIState.AI_SOFTATTACK)
            {
                c.ResumeAttackMove();
                NetSetState(AIState.AI_ATTACKMOVESTATE);
            }

            // Hold position (AI_STANDING): the champion holds its EXACT position and never moves — it
            // does not chase and does not walk to close a gap (unlike Hero.lua's AI_STANDING soft-chase;
            // our H/Hold maps to a pure hold). With the "Auto Attack" option on it auto-attacks any
            // enemy already within auto-ATTACK range: keep the current target while it is in range, else
            // grab the nearest enemy already in attack range. MoveOrder stays Hold, so the engine
            // attacks in place (RefreshWaypoints is suppressed for Hold) but never issues a chase.
            // (H = "don't move but fight"; S/Stop = "cancel all" — handled separately as AI_HARDIDLE.)
            if (CurrentState == AIState.AI_STANDING)
            {
                if (c.AutoAcquireTargetEnabled && c.CanAttack())
                {
                    // Engine attack-range test (ObjAIBase.UpdateTarget: Range.Total + both radii).
                    float aaRange = c.Stats.Range.Total + c.CollisionRadius;
                    bool currentInRange = c.TargetUnit != null && !c.TargetUnit.IsDead
                        && c.TargetUnit.Team != c.Team
                        && Vector2.DistanceSquared(c.Position, c.TargetUnit.Position)
                           <= (aaRange + c.TargetUnit.CollisionRadius) * (aaRange + c.TargetUnit.CollisionRadius);
                    if (!currentInRange)
                    {
                        // The nearest enemy in acquisition range is the nearest enemy overall, so if it
                        // is not within attack range no other one is either. Never acquire beyond attack
                        // range — Hold must not walk to close the gap.
                        AttackableUnit nearest = c.AcquireAttackMoveTarget();
                        if (nearest != null
                            && Vector2.DistanceSquared(c.Position, nearest.Position)
                               <= (aaRange + nearest.CollisionRadius) * (aaRange + nearest.CollisionRadius))
                        {
                            // Keep MoveOrder == Hold: the engine auto-attacks it in place, no chase.
                            c.SetTargetUnit(nearest, true);
                        }
                    }
                }
                return;
            }

            // The engine executes the chase while a target is set; selection only runs with no target.
            if (c.TargetUnit != null)
            {
                return;
            }

            // Attack-move ongoing: acquire the nearest enemy along the walk; reaching the destination
            // with none ends the order; an acquired target (when walking to a point) becomes a soft attack.
            if (c.MoveOrder == OrderType.AttackMove)
            {
                if (c.AttackMoveDestination != Vector2.Zero && c.IsPathEnded())
                {
                    c.AttackMoveDestination = Vector2.Zero;
                }
                if (c.IsAutoAttackOnCooldown)
                {
                    return;
                }
                AttackableUnit nextTarget = c.AcquireAttackMoveTarget();
                if (nextTarget != null && c.AttackMoveDestination != Vector2.Zero)
                {
                    SetStateAndCloseToTarget(AIState.AI_SOFTATTACK, nextTarget);
                }
                else if (nextTarget != null)
                {
                    c.SetTargetUnit(nextTarget, true);
                }
                return;
            }

            // Idle auto-acquire ("Auto Attack" option): an idle champion engages the nearest enemy in
            // acquisition range. Guards mirror the former engine block (no CC/casting/dashing/mid-walk).
            if (!c.AutoAcquireTargetEnabled || !c.CanAttack())
            {
                return;
            }
            if (c.MoveOrder == OrderType.Stop || c.MoveOrder == OrderType.Hold)
            {
                return;
            }
            if (c.IsForceMoved || c.SpellToCast != null || c.IsCasting
                || c.ChannelSpell != null || !c.IsPathEnded() || c.IsAttacking)
            {
                return;
            }

            AttackableUnit idleTarget = c.AcquireAttackMoveTarget();
            if (idleTarget != null)
            {
                c.SetTargetUnit(idleTarget, true);
                c.UpdateMoveOrder(OrderType.AttackTo, true);
            }
        }

        // Hero.lua OnOrder → AI state. Phase 1 of the Order/State split: set the state LABEL only
        // (NetSetState), in parallel to MoveOrder which still drives the combat brain. No movement is
        // (re)issued here — HandleMove still owns champion movement this phase. Phase 2 switches the
        // brain to read _aiState; Phase 4/5 retire MoveOrder as the behaviour driver.
        public override void OnOrder(OrderType order, AttackableUnit target, Vector2 pos)
        {
            switch (order)
            {
                // Explicit attack-on-unit (and taunt) = hard chase (does not drop out of range).
                case OrderType.AttackTo:
                    NetSetState(AIState.AI_HARDATTACK);
                    break;
                // Attack-move: soft-attack if a target is acquired (drops out of acquisition range),
                // otherwise push toward the ground point. (Hero.lua OnOrder ATTACKMOVE → FindTargetInAcR.)
                case OrderType.AttackMove:
                    NetSetState((target != null || FindTargetInAcR() != null)
                        ? AIState.AI_SOFTATTACK : AIState.AI_ATTACKMOVESTATE);
                    break;
                case OrderType.MoveTo:
                    NetSetState(AIState.AI_MOVE);
                    break;
                // Hold: stand still (never move/chase); auto-attack any enemy within attack range.
                case OrderType.Hold:
                    NetSetState(AIState.AI_STANDING);
                    break;
                // Stop: clear target and idle (no acquire).
                case OrderType.Stop:
                    NetSetState(AIState.AI_HARDIDLE);
                    break;
            }
        }
    }
}
