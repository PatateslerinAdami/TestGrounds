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

            // Lost-target re-acquisition (Hero.lua OnReachedDestinationForGoingToLastLocation): the champion
            // walked to a hard target's last-known position without re-sighting it → give up and idle (the
            // OnTick idle-acquire then resumes normal target selection). The walk + resume-on-sight are
            // driven in OnTick from Owner.IsTargetLost(). See docs/LOST_TARGET_REACQUISITION_PLAN.md.
            Subscribe(AIEvent.OnReachedDestinationForGoingToLastLocation, _ =>
            {
                Owner.ClearLostTarget();
                NetSetState(AIState.AI_IDLE);
            });

            // Order/State split Phase 2: the champion brain owns combat SELECTION (Hero.lua
            // TimerDistanceScan): idle auto-acquire + attack-move acquire / soft-drop / resume. The
            // matching engine UpdateTarget blocks are skipped for HeroAI units; the engine keeps only
            // the EXECUTION (chase the set target to range + auto-attack).
            Owner.ScriptOwnsCombatSelection = true;
            // Auto-attack firing is driven by the shared AutoAttackComponent (attached by BaseAIScript): it
            // toggles the swing on/off by range against the player-/scan-selected target. The engine fires
            // only when the toggle is on, never merely because a target is in range. Orb-walk / stutter-step
            // are unaffected (move-/CC-driven windup cancels are separate); the engine's idealRange geometry
            // still binds WHERE the swing fires.

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

            // Hold position (player H): the champion holds its EXACT position and never moves — it does
            // not chase and does not walk to close a gap. Hold is the ORDER (MoveOrder == Hold ⇒ Riot
            // IsHoldingPosition), NOT an AI state; the state is the turret-like stationary-attacker pair
            // AI_HARDIDLE ↔ AI_HARDIDLE_ATTACKING (NOT AI_STANDING, which is Riot's soft-idle). With the
            // "Auto Attack" option on it auto-attacks any enemy already within auto-ATTACK range: keep the
            // current target while in range, else grab the nearest enemy already in attack range. MoveOrder
            // stays Hold, so the engine attacks in place (RefreshWaypoints suppressed for Hold) but never
            // chases. Gated on the AI_HARDIDLE(_ATTACKING) state alongside MoveOrder==Hold so the internal
            // cast/channel-end Hold fallbacks (which land on AI_IDLE via UpdateMoveOrder, not HARDIDLE) keep
            // their no-acquire behaviour. (S/Stop = "cancel all" = AI_HARDIDLE but MoveOrder==Stop ⇒ excluded.)
            if (c.MoveOrder == OrderType.Hold
                && (CurrentState == AIState.AI_HARDIDLE || CurrentState == AIState.AI_HARDIDLE_ATTACKING))
            {
                if (c.AutoAcquireTargetEnabled && c.CanAttack())
                {
                    // Engine attack-range test (ObjAIBase.UpdateTarget: Range.Total + both radii).
                    float aaRange = c.Stats.Range.Total + c.CollisionRadius;
                    bool inRange = c.TargetUnit != null && !c.TargetUnit.IsDead
                        && c.TargetUnit.Team != c.Team
                        && Vector2.DistanceSquared(c.Position, c.TargetUnit.Position)
                           <= (aaRange + c.TargetUnit.CollisionRadius) * (aaRange + c.TargetUnit.CollisionRadius);
                    if (!inRange)
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
                            inRange = true;
                        }
                    }
                    // Reflect attacking-status in the state (turret HARDIDLE ↔ HARDIDLE_ATTACKING). Today
                    // behaviour-neutral (auto-attack is still TargetUnit-driven); this readies the C
                    // state-gate (AI_HARDIDLE_ATTACKING is an auto-attack-permitting state, AI_HARDIDLE not).
                    NetSetState(inRange ? AIState.AI_HARDIDLE_ATTACKING : AIState.AI_HARDIDLE);
                }
                return;
            }

            // The engine executes the chase while a target is set; selection only runs with no target.
            if (c.TargetUnit != null)
            {
                return;
            }

            // Lost-target re-acquisition (Hero.lua go-to-last-known). The engine remembered a HARD target
            // that left vision (Owner.IsTargetLost(); soft-acquired targets are excluded engine-side): walk
            // to its last-known position, resume the hard chase the moment it reappears, and give up on
            // arrival (OnReachedDestinationForGoingToLastLocation → IDLE). Runs before idle-acquire so the
            // champion pursues the lost target instead of grabbing a new one.
            if (c.IsTargetLost())
            {
                AttackableUnit seen = c.GetLostTargetIfVisible();
                if (seen != null)
                {
                    SetStateAndCloseToTarget(AIState.AI_HARDATTACK, seen);
                    return;
                }
                if (CurrentState != AIState.AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION)
                {
                    SetStateAndMove(AIState.AI_ATTACK_GOING_TO_LAST_KNOWN_LOCATION, c.LostTargetLastKnownPosition);
                }
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
                    // Attack-move acquired a target → soft attack (so the C state-gate permits the swing
                    // and the soft-drop above applies). Matches the dest != Zero branch's SOFTATTACK.
                    NetSetState(AIState.AI_SOFTATTACK);
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
                // Enter an attack state so the auto-attack state-gate (option C) permits the swing — this
                // acquire previously left _aiState at AI_IDLE while attacking. AI_HARDATTACK matches the
                // hard AttackTo chase set just above (no soft-drop). (Hero.lua idle-acquire uses SOFTATTACK
                // = soft/droppable; our hard variant is a pre-existing divergence, not changed here.)
                NetSetState(AIState.AI_HARDATTACK);
            }
        }

        // State-gated auto-attack (option C, Hero.lua TimerCheckAttack): the champion only auto-attacks
        // while in an ATTACKING state — right-click hard attack (AI_HARDATTACK), attack-move / idle
        // acquired (AI_SOFTATTACK), Hold attacking in place (AI_HARDIDLE_ATTACKING), or a taunt
        // (AI_TAUNTED). Charm (AI_CHARMED) carries no target so it never fires, but is listed to match
        // Hero.lua's attack-state set. Every champion auto-attack path is made to enter one of these
        // states (OnOrder + OnTick acquires), so this gates OUT only the "target set but not engaged"
        // cases (Riot: a target merely set — e.g. a HUD selection or a move-to-cast — does not auto-fire).
        public override bool AutoAttackStatePermits()
        {
            switch (CurrentState)
            {
                case AIState.AI_HARDATTACK:
                case AIState.AI_SOFTATTACK:
                case AIState.AI_HARDIDLE_ATTACKING:
                case AIState.AI_TAUNTED:
                case AIState.AI_CHARMED:
                    return true;
                default:
                    return false;
            }
        }

        // Hero.lua OnOrder → AI state. Phase 1 of the Order/State split: set the state LABEL only
        // (NetSetState), in parallel to MoveOrder which still drives the combat brain. No movement is
        // (re)issued here — HandleMove still owns champion movement this phase. Phase 2 switches the
        // brain to read _aiState; Phase 4/5 retire MoveOrder as the behaviour driver.
        public override void OnOrder(OrderType order, AttackableUnit target, Vector2 pos)
        {
            // Any explicit player order overrides an in-progress go-to-last-known pursuit (otherwise the
            // remembered lost target would re-trigger the walk next OnTick and fight the new order).
            Owner.ClearLostTarget();

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
                // Hold (player H): stand still (never move/chase); auto-attack any enemy in attack range.
                // Hold is an ORDER concept (MoveOrder == Hold ⇒ Riot obj_AI_Hero::IsHoldingPosition checks
                // savedOrderCmd == AI_HOLD), NOT an AI state — so it uses the turret-like stationary-attacker
                // state AI_HARDIDLE (↔ AI_HARDIDLE_ATTACKING while attacking, set in OnTick), the same pair
                // Turret.lua uses. It deliberately does NOT use AI_STANDING: in Hero.lua AI_STANDING is the
                // post-combat soft-idle that auto-acquires → SOFTATTACK, a different concept.
                case OrderType.Hold:
                    NetSetState(AIState.AI_HARDIDLE);
                    break;
                // Stop: clear target and idle (no acquire).
                case OrderType.Stop:
                    NetSetState(AIState.AI_HARDIDLE);
                    break;
            }
        }
    }
}
