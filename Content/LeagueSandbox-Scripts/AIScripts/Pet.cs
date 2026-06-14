using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using EnginePet = LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Pet;

namespace AIScripts
{
    // Controllable pet brain — faithful port of Riot's Pet.lua (4.20), rebuilt on BaseAIScript so the
    // pet joins the uniform Order/State model: the script owns SELECTION + state, the engine EXECUTES
    // (chase the set target + auto-attack). Replaces the old half-implemented IAIScript Pet.cs (which
    // set states but never actually pathed — every transition was a "TODO: Move to X").
    //
    // Two timers (0.15s each), exactly as Pet.lua:
    //   TimerScanDistance  — leash maintenance: teleport to owner when very far, return when far + idle,
    //                        stop when arrived / owner stopped.
    //   TimerFindEnemies   — acquire/attack: from a non-attacking state grab the nearest enemy in
    //                        acquisition range and switch to the matching *_ATTACKING state; recover when
    //                        the current attack target is lost (the engine drops dead/invisible targets,
    //                        so OnTargetLost is folded into this poll).
    // Hard commands (PetHard*) are the explicit player pet commands and lock out the soft auto-behavior
    // until cleared. Fear/Taunt/Charm movement is driven by the shared CrowdControlComponent; CC-end
    // resets the pet to AI_PET_IDLE. The PetCommandParticle visual is deferred (P-D).
    public class Pet : BaseAIScript
    {
        private const float FAR_MOVEMENT_DISTANCE = 1000.0f;
        private const float DEFAULT_RETURN_RADIUS = 200.0f;

        // ---- Subclass hooks (UncontrollablePet / YorickPHPet override these) ----
        // The summoner the pet belongs to. Controllable: the spawning champion (Minion.Owner).
        protected virtual ObjAIBase ResolveOwner() => (Owner as Minion)?.Owner;
        // Autonomous pets actively walk back to the owner once they drift past ActiveFollowDistance
        // (UncontrollablePet.lua). Controllable pets do not — they only RETURN from idle when far.
        protected virtual bool ActivelyFollowsOwner => false;
        protected virtual float ActiveFollowDistance => 800.0f;
        // Teleport-to-owner threshold (Pet.lua 2000; YorickPHPet 2500).
        protected virtual float TeleportDistance => 2000.0f;

        protected override void OnActivateBehavior()
        {
            // The pet brain owns combat selection — the engine must not also auto-acquire for it.
            Owner.ScriptOwnsCombatSelection = true;

            NetSetState(AIState.AI_PET_IDLE);
            InitTimer("TimerScanDistance", 0.15f, true, TimerScanDistance);
            InitTimer("TimerFindEnemies", 0.15f, true, TimerFindEnemies);

            // Crowd control is component-driven (auto-attached); resume normal behavior when it ends.
            Subscribe(AIEvent.OnFearEnd, _ => NetSetState(AIState.AI_PET_IDLE));
            Subscribe(AIEvent.OnFleeEnd, _ => NetSetState(AIState.AI_PET_IDLE));
            Subscribe(AIEvent.OnTauntEnd, _ => NetSetState(AIState.AI_PET_IDLE));
            Subscribe(AIEvent.OnCharmEnd, _ => NetSetState(AIState.AI_PET_IDLE));
        }

        // ---- Pet.lua OnOrder: translate the player's pet command into a pet state ----
        public override void OnOrder(OrderType order, AttackableUnit target, Vector2 pos)
        {
            AIState state = CurrentState;
            if (state == AIState.AI_HALTED || IsCcState(state))
            {
                return;
            }

            // A hard command in progress ignores soft orders until cleared by another hard command.
            if (IsHardState(state)
                && (order == OrderType.AttackTo || order == OrderType.MoveTo
                    || order == OrderType.AttackMove || order == OrderType.Stop))
            {
                return;
            }

            ObjAIBase owner = OwnerOrDie();
            if (owner == null)
            {
                return;
            }

            switch (order)
            {
                // Soft orders (only reach the pet if the owner's orders are forwarded — see plan).
                case OrderType.AttackTo:
                    if (target == null) return;
                    Engage(AIState.AI_PET_ATTACK, target);
                    break;
                case OrderType.MoveTo:
                    if (Vector2.DistanceSquared(Owner.Position, pos) > FAR_MOVEMENT_DISTANCE * FAR_MOVEMENT_DISTANCE
                        || state == AIState.AI_PET_HOLDPOSITION || state == AIState.AI_PET_HOLDPOSITION_ATTACKING)
                    {
                        Follow(AIState.AI_PET_MOVE, owner);
                    }
                    break;
                case OrderType.AttackMove:
                    Follow(AIState.AI_PET_ATTACKMOVE, owner);
                    break;
                case OrderType.Stop:
                    // Soft stop just clears the command indicator (cosmetic; deferred).
                    break;
                case OrderType.Hold:
                    Stand(AIState.AI_PET_HOLDPOSITION);
                    break;

                // Hard pet commands (these reach the pet via HandleMove → pet.IssueOrder).
                case OrderType.PetHardStop:
                    Stand(AIState.AI_PET_HARDSTOP);
                    break;
                case OrderType.PetHardAttack:
                    if (target == null) return;
                    Engage(AIState.AI_PET_HARDATTACK, target);
                    break;
                case OrderType.PetHardMove:
                    MoveToPoint(AIState.AI_PET_HARDMOVE, pos);
                    break;
                case OrderType.PetHardReturn:
                    Follow(AIState.AI_PET_HARDRETURN, owner);
                    break;
            }
        }

        // ---- Pet.lua TimerScanDistance: leash / return / arrival maintenance ----
        private void TimerScanDistance()
        {
            AIState state = CurrentState;
            if (state == AIState.AI_HALTED || IsCcState(state))
            {
                return;
            }

            ObjAIBase owner = OwnerOrDie();
            if (owner == null)
            {
                return;
            }

            float distToOwner = Vector2.Distance(Owner.Position, owner.Position);

            // Too far → teleport back to the owner and idle.
            if (distToOwner > TeleportDistance)
            {
                TeleportTo(Owner, owner.Position.X, owner.Position.Y);
                Stand(AIState.AI_PET_IDLE);
                return;
            }

            // Autonomous pets (UncontrollablePet.lua) actively walk to the owner once they drift past
            // the follow distance, regardless of what they were doing (leash). The owner position is
            // always known server-side, so the >vision "blind move to last-known" branch collapses to
            // the same path-to-owner. Controllable pets skip this (ActivelyFollowsOwner = false).
            if (ActivelyFollowsOwner && state != AIState.AI_PET_MOVE && distToOwner > ActiveFollowDistance)
            {
                Follow(AIState.AI_PET_MOVE, owner);
                return;
            }

            bool noEnemiesNearby = FindTargetInAcR() == null;

            // Idle and drifted past the return radius with nothing to fight → walk back to the owner.
            if (state == AIState.AI_PET_IDLE && distToOwner > ReturnRadius() && noEnemiesNearby)
            {
                Follow(AIState.AI_PET_RETURN, owner);
                return;
            }

            // Arrived back at the owner → idle.
            if ((state == AIState.AI_PET_RETURN || state == AIState.AI_PET_HARDRETURN
                 || state == AIState.AI_PET_MOVE)
                && distToOwner <= ReturnRadius())
            {
                Stand(AIState.AI_PET_IDLE);
                return;
            }

            // Owner stopped while we were following (and no enemy for attack-move) → idle.
            if (!OwnerIsMoving(owner)
                && (state == AIState.AI_PET_MOVE
                    || (state == AIState.AI_PET_ATTACKMOVE && noEnemiesNearby)))
            {
                Stand(AIState.AI_PET_IDLE);
                return;
            }

            // Still returning / following the owner: the engine can't chase an ally, so re-path to the
            // owner's CURRENT position whenever we reach the (now stale) point we were walking to. This
            // tracks a moving owner without spamming a path every tick.
            if ((state == AIState.AI_PET_RETURN || state == AIState.AI_PET_HARDRETURN
                 || state == AIState.AI_PET_MOVE || state == AIState.AI_PET_ATTACKMOVE)
                && Owner.IsPathEnded())
            {
                Follow(state, owner);
                return;
            }

            // Reached a hard-move destination → hard idle.
            if (Owner.IsPathEnded() && state == AIState.AI_PET_HARDMOVE)
            {
                Stand(AIState.AI_PET_HARDIDLE);
                return;
            }

            if (state == AIState.AI_PET_SPAWNING)
            {
                NetSetState(AIState.AI_PET_IDLE);
            }
        }

        // ---- Pet.lua TimerFindEnemies (+ OnTargetLost): acquire enemies / recover lost target ----
        private void TimerFindEnemies()
        {
            AIState state = CurrentState;
            if (state == AIState.AI_HALTED || IsCcState(state))
            {
                return;
            }

            if (OwnerOrDie() == null)
            {
                return;
            }

            // Moving states do not acquire.
            if (state == AIState.AI_PET_MOVE || state == AIState.AI_PET_HARDMOVE
                || state == AIState.AI_PET_HARDSTOP)
            {
                return;
            }

            // Non-attacking states: pick up the nearest enemy in acquisition range and engage it.
            if (state == AIState.AI_PET_IDLE || state == AIState.AI_PET_RETURN
                || state == AIState.AI_PET_ATTACKMOVE || state == AIState.AI_PET_HARDIDLE
                || state == AIState.AI_PET_HOLDPOSITION)
            {
                AttackableUnit newTarget = FindTargetInAcR();
                if (newTarget == null)
                {
                    return;
                }

                switch (state)
                {
                    case AIState.AI_PET_IDLE: Engage(AIState.AI_PET_ATTACK, newTarget); break;
                    case AIState.AI_PET_RETURN: Engage(AIState.AI_PET_RETURN_ATTACKING, newTarget); break;
                    case AIState.AI_PET_ATTACKMOVE: Engage(AIState.AI_PET_ATTACKMOVE_ATTACKING, newTarget); break;
                    // Hard-idle / hold attack IN PLACE (no chase).
                    case AIState.AI_PET_HARDIDLE: SetTargetNoChase(AIState.AI_PET_HARDIDLE_ATTACKING, newTarget); break;
                    case AIState.AI_PET_HOLDPOSITION: SetTargetNoChase(AIState.AI_PET_HOLDPOSITION_ATTACKING, newTarget); break;
                }
                return;
            }

            // Attacking states: the engine drops a dead/invisible target (TargetUnit == null). Recover
            // here (Pet.lua OnTargetLost): re-acquire if another enemy is in range, else fall back to
            // the matching non-attacking state.
            if (Owner.TargetUnit == null && IsAttackingState(state))
            {
                AttackableUnit newTarget = FindTargetInAcR();
                if (newTarget != null)
                {
                    switch (state)
                    {
                        case AIState.AI_PET_ATTACK:
                        case AIState.AI_PET_HARDATTACK:
                            Engage(AIState.AI_PET_ATTACK, newTarget); break;
                        case AIState.AI_PET_RETURN_ATTACKING:
                            Engage(AIState.AI_PET_RETURN_ATTACKING, newTarget); break;
                        case AIState.AI_PET_ATTACKMOVE_ATTACKING:
                            Engage(AIState.AI_PET_ATTACKMOVE_ATTACKING, newTarget); break;
                        case AIState.AI_PET_HARDIDLE_ATTACKING:
                            SetTargetNoChase(AIState.AI_PET_HARDIDLE_ATTACKING, newTarget); break;
                        case AIState.AI_PET_HOLDPOSITION_ATTACKING:
                            SetTargetNoChase(AIState.AI_PET_HOLDPOSITION_ATTACKING, newTarget); break;
                    }
                }
                else
                {
                    ObjAIBase owner = OwnerOrDie();
                    switch (state)
                    {
                        case AIState.AI_PET_HARDIDLE_ATTACKING: Stand(AIState.AI_PET_HARDIDLE); break;
                        case AIState.AI_PET_HOLDPOSITION_ATTACKING: Stand(AIState.AI_PET_HOLDPOSITION); break;
                        case AIState.AI_PET_RETURN_ATTACKING:
                            if (owner != null) Follow(AIState.AI_PET_RETURN, owner); break;
                        case AIState.AI_PET_ATTACKMOVE_ATTACKING:
                            if (owner != null) Follow(AIState.AI_PET_ATTACKMOVE, owner); break;
                        default: Stand(AIState.AI_PET_IDLE); break; // ATTACK / HARDATTACK
                    }
                }
            }
        }

        // ---- helpers ----

        // Chase + attack an enemy (sets target + AttackTo; the engine chases and auto-attacks).
        // Only valid for ENEMY targets — the engine's chase runs inside its `Team != Team` gate.
        private void Engage(AIState state, AttackableUnit enemy) => SetStateAndCloseToTarget(state, enemy);

        // Follow / return to a friendly unit (the owner). The engine only chases ENEMY targets, so an
        // ally is followed by explicitly pathing to its CURRENT position (re-issued by TimerScanDistance
        // as the owner moves). Clears any combat target so the pet does not attack while relocating.
        private void Follow(AIState state, AttackableUnit owner)
        {
            Owner.SetTargetUnit(null);
            SetStateAndMove(state, owner.Position);
        }

        // Walk to a fixed ground point (hard-move): clear the combat target so it does not keep
        // attacking, then path there.
        private void MoveToPoint(AIState state, Vector2 pos)
        {
            Owner.SetTargetUnit(null);
            SetStateAndMove(state, pos);
        }

        // Stand still at the current position in `state`: drop the chase target and stop moving.
        private void Stand(AIState state)
        {
            Owner.SetTargetUnit(null);
            Owner.StopMovement();
            NetSetState(state);
        }

        private float ReturnRadius() => (Owner as EnginePet)?.GetReturnRadius() ?? DEFAULT_RETURN_RADIUS;

        private static bool OwnerIsMoving(ObjAIBase owner) => !owner.IsPathEnded();

        // The pet's owner (summoner). null (owner gone) → kill the pet, mirroring Pet.lua's GetOwner-nil.
        private ObjAIBase OwnerOrDie()
        {
            ObjAIBase owner = ResolveOwner();
            if (owner == null)
            {
                Owner.Die(CreateDeathData(false, 0, Owner, Owner, DamageType.DAMAGE_TYPE_TRUE,
                    DamageSource.DAMAGE_SOURCE_INTERNALRAW, 0.0f));
            }
            return owner;
        }

        private static bool IsCcState(AIState s)
            => s == AIState.AI_TAUNTED || s == AIState.AI_FEARED
               || s == AIState.AI_CHARMED || s == AIState.AI_FLEEING;

        private static bool IsHardState(AIState s)
            => s == AIState.AI_PET_HARDATTACK || s == AIState.AI_PET_HARDMOVE
               || s == AIState.AI_PET_HARDIDLE || s == AIState.AI_PET_HARDIDLE_ATTACKING
               || s == AIState.AI_PET_HARDRETURN || s == AIState.AI_PET_HARDSTOP;

        private static bool IsAttackingState(AIState s)
            => s == AIState.AI_PET_ATTACK || s == AIState.AI_PET_HARDATTACK
               || s == AIState.AI_PET_RETURN_ATTACKING || s == AIState.AI_PET_ATTACKMOVE_ATTACKING
               || s == AIState.AI_PET_HARDIDLE_ATTACKING || s == AIState.AI_PET_HOLDPOSITION_ATTACKING;
    }
}
