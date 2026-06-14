using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;

namespace AIScripts
{
    // Faithful port of Riot's Scripts/Leashed.lua (jungle camp AI) on top of BaseAIScript.
    // Replaces the old "EXTREMELY basic" BasicJungleMonsterAI (hardcoded 800u leash, instant
    // full-HP reset). Behavior reproduced:
    //  - Camp starts passive (RoamState Inactive) — jungle monsters do NOT aggro on proximity,
    //    only when hit (OnTakeDamage) or called by a camp-mate (OnCallForHelp).
    //  - Once hostile, it chases its target but is tethered to its spawn ("leashed") position:
    //    drifting past LEASH_RADIUS forces an AI_RETREAT back to camp; a leash counter caps how far
    //    it can be kited before it gives up and returns.
    //  - On retreat / leash it gradually heals via OutOfCombatRegenComponent (12.5%/s — the HP bar
    //    ticks up, not an instant reset), restores its leash orientation on arrival, and goes
    //    passive again.
    //  - Fear/Flee/Taunt/Charm are handled by the auto-attached CrowdControlComponent (Leashed.lua
    //    attaches DefaultFear/Flee/TauntBehavior); the timers below yield while RunInFear/retreating.
    public class JungleMonsterAI : BaseAIScript
    {
        public JungleMonsterAI()
        {
            // ObjAIBase.TakeDamage broadcasts OnCallForHelp to same-team units in acquisition range —
            // this is how a struck camp-mate aggros the rest of the camp.
            AIScriptMetaData.HandlesCallsForHelp = true;
        }

        // Leashed.lua constants.
        private const float LEASH_RADIUS = 850f;
        private const float LEASH_PROTECTION_RADIUS = 750f;
        private const float INNER_RELEASH_RADIUS = 750f;
        private const float RELEASH_RADIUS = 1150f;
        private const int LEASH_COUNTER_THRESHOLD = 10;
        // Distance to the retreat destination at which we count as "arrived" (Leashed GetDistToRetreat < 100).
        private const float RETREAT_ARRIVE_DISTANCE = 100f;
        // The +25u hysteresis Leashed applies before switching to a closer target (anti-jitter).
        private const float RETARGET_HYSTERESIS = 25f;

        private Monster _monster;
        private Vector2 _leashedPos;
        private Vector3 _leashedOrientation;
        private int _leashCounter;
        private MinionRoamState _originalRoamState;
        private OutOfCombatRegenComponent _regen;
        private bool _leashOrientationCaptured;

        // ---------------- Lifecycle (Leashed.lua OnInit) ----------------

        protected override void OnActivateBehavior()
        {
            _monster = Owner as Monster;
            if (_monster == null)
            {
                return;
            }

            _leashedPos = _monster.Position;
            // NOTE: do NOT capture the spawn orientation here. OnActivate runs inside the ObjAIBase
            // base ctor, BEFORE the Monster ctor applies FaceDirection(faceDirection) — Direction is
            // still the default at this point. Captured on the first OnTick instead (below).
            _originalRoamState = MinionRoamState.Inactive;
            _monster.RoamState = MinionRoamState.Inactive;

            _regen = AddComponent(new OutOfCombatRegenComponent());

            // OnInit SetState(AI_ATTACK): logical state is AI_ATTACK, but with RoamState Inactive the
            // monster acquires nothing until woken — the timers gate on RoamState.
            NetSetState(AIState.AI_ATTACK);

            InitTimer("TimerRetreat", 0.5f, true, TimerRetreat);
            InitTimer("TimerAttack", 0f, true, TimerAttack);

            ApiEventManager.OnTakeDamage.AddListener(this, _monster, OnDamaged, false);
            ApiEventManager.OnTargetLost.AddListener(this, _monster, OnOwnerTargetLost, false);

            // CrowdControlComponent drives fear/flee/taunt/charm movement; just re-acquire on CC end.
            Subscribe(AIEvent.OnFearEnd, _ => FindNewTarget());
            Subscribe(AIEvent.OnTauntEnd, _ => FindNewTarget());
            Subscribe(AIEvent.OnCharmEnd, _ => FindNewTarget());
        }

        // Capture the spawn facing on the first tick — by now the Monster ctor has run
        // FaceDirection(faceDirection), so Direction holds the real spawn orientation (it was still
        // the default during OnActivate). Restored on leash return in OnStoppedMoving.
        protected override void OnTick(float diff)
        {
            if (!_leashOrientationCaptured && _monster != null)
            {
                _leashedOrientation = _monster.Direction;
                _leashOrientationCaptured = true;
            }
        }

        // ---------------- Helpers ----------------

        private float DistToLeashed => Vector2.Distance(_monster.Position, _leashedPos);
        private float DistToLeashed_Of(AttackableUnit u) => Vector2.Distance(u.Position, _leashedPos);
        private bool MovementStopped => _monster.IsPathEnded();
        private bool Roaming(MinionRoamState s) => _monster.RoamState == s;

        // Leashed.lua Retreat() = SetStateAndMoveToLeashedPos(AI_RETREAT).
        private void Retreat()
        {
            // Enter AI_RETREAT BEFORE clearing the target. SetTargetUnit(null) synchronously publishes
            // OnTargetLost (before it even nulls TargetUnit — see ObjAIBase.SetTargetUnit), which calls
            // our OnOwnerTargetLost -> FindNewTarget; with the state already AI_RETREAT that path
            // short-circuits, otherwise it recurses Retreat -> SetTargetUnit -> OnTargetLost -> ...
            // (stack overflow). Clearing the target also stops ObjAIBase.UpdateTarget from chasing it
            // toward attack range regardless of our MoveTo order (its enemy-chase branch only exempts
            // CastSpell — ObjAIBase.cs:2491); the same override the CrowdControlComponent neutralises
            // for flee. Only clear once (re-issues during the retreat keep TargetUnit null already).
            NetSetState(AIState.AI_RETREAT);
            if (_monster.TargetUnit != null)
            {
                _monster.CancelAutoAttack(reset: true, fullCancel: true);
                _monster.SetTargetUnit(null);
            }
            SetStateAndMove(AIState.AI_RETREAT, _leashedPos);
        }

        // ---------------- Wake / aggro (Leashed.lua OnTakeDamage) ----------------

        private void OnDamaged(DamageData damageData)
        {
            if (_monster == null || _monster.IsDead || CurrentState == AIState.AI_HALTED)
            {
                return;
            }

            AttackableUnit attacker = damageData.Attacker;
            if (attacker == null || attacker.IsDead)
            {
                return;
            }

            Engage(attacker);
            AlertCampMates(attacker);
        }

        // Wake the whole camp when any one of its monsters is hit. ObjAIBase.TakeDamage's built-in
        // OnCallForHelp broadcast is gated on the victim's acquisition range to BOTH victim and
        // attacker, so attacking from range (or a spread-out camp) can miss some mates — but a jungle
        // camp aggros as a unit regardless of where the attacker stands. Engage is idempotent for
        // mates already engaged. (CallForHelp is still used for releash + non-camp allies.)
        private void AlertCampMates(AttackableUnit attacker)
        {
            if (_monster.Camp == null)
            {
                return;
            }

            foreach (Monster mate in _monster.Camp.Monsters)
            {
                if (mate == _monster || mate.IsDead)
                {
                    continue;
                }

                if (mate.AIScript is JungleMonsterAI mateAI)
                {
                    mateAI.Engage(attacker);
                }
            }
        }

        // Leashed.lua LeashedCallForHelp: a camp-mate was hit — aggro onto the attacker (and re-engage
        // out of a retreat if the attacker is still within the (re)leash window).
        protected override void OnCallForHelpBehavior(AttackableUnit attacker, AttackableUnit victim)
        {
            if (_monster == null || _monster.IsDead || CurrentState == AIState.AI_HALTED)
            {
                return;
            }

            if (attacker == null || attacker.IsDead)
            {
                return;
            }

            Engage(attacker);

            // Re-engage from a retreat if we (and the attacker) are still within the leash/releash radii.
            if (CurrentState == AIState.AI_RETREAT && _leashCounter < LEASH_COUNTER_THRESHOLD)
            {
                float attackerDist = DistToLeashed_Of(attacker);
                bool inLeash = attackerDist <= LEASH_RADIUS;
                bool inReleash = DistToLeashed <= INNER_RELEASH_RADIUS && attackerDist <= RELEASH_RADIUS;
                if (inLeash || inReleash)
                {
                    _leashCounter++;
                    _regen.Stop();
                    SetStateAndCloseToTarget(AIState.AI_ATTACK, attacker);
                    _monster.RoamState = MinionRoamState.Hostile;
                }
            }
        }

        // Shared wake + anti-kite re-target body of OnTakeDamage / LeashedCallForHelp.
        private void Engage(AttackableUnit attacker)
        {
            AIState state = CurrentState;

            // Leashed.lua OnTakeDamage: the engage target is FindTargetNearPosition(GetMyPos(),
            // LEASH_RADIUS) — the nearest valid unit within leash range of the monster's CURRENT
            // position — and the damage-source/help attacker is only the fallback when that finds
            // nothing. (So a camp peels onto the closest body near it, e.g. an adjacent pet/minion,
            // not necessarily the ranged hero that poked it.)
            AttackableUnit candidate = FindTargetNear(_monster.Position, LEASH_RADIUS) ?? attacker;
            if (candidate == null)
            {
                return;
            }

            // First hit on a dormant camp: wake and chase.
            if (Roaming(MinionRoamState.Inactive)
                && state != AIState.AI_RETREAT && state != AIState.AI_TAUNTED
                && state != AIState.AI_FEARED && state != AIState.AI_FLEEING)
            {
                _regen.Stop();
                SetStateAndCloseToTarget(AIState.AI_ATTACK, candidate);
                _monster.RoamState = MinionRoamState.Hostile;
                return;
            }

            // Already engaged: switch to a meaningfully-closer candidate near the camp; each switch
            // burns a leash-counter charge, and once exhausted the monster gives up and retreats
            // (anti-kite). Distances are measured from the monster's current position, matching the Lua.
            if (Roaming(MinionRoamState.Hostile) && state == AIState.AI_ATTACK)
            {
                _regen.Stop();
                AttackableUnit current = _monster.TargetUnit;
                if (current == null)
                {
                    return;
                }

                if (candidate != current
                    && Vector2.Distance(candidate.Position, _monster.Position) + RETARGET_HYSTERESIS
                       < Vector2.Distance(current.Position, _monster.Position))
                {
                    SetStateAndCloseToTarget(AIState.AI_ATTACK, candidate);
                    _monster.RoamState = MinionRoamState.Hostile;
                    _leashCounter++;
                    if (_leashCounter > LEASH_COUNTER_THRESHOLD)
                    {
                        Retreat();
                    }
                }
            }
        }

        // ---------------- Leash enforcement (Leashed.lua TimerRetreat, 0.5s) ----------------

        private void TimerRetreat()
        {
            if (_monster == null || _monster.IsDead
                || Roaming(MinionRoamState.Inactive) || Roaming(MinionRoamState.RunInFear))
            {
                return;
            }

            AIState state = CurrentState;
            if (state == AIState.AI_HALTED)
            {
                return;
            }

            float distToLeash = DistToLeashed;
            AttackableUnit target = _monster.TargetUnit;
            float targetDistToLeash = target != null ? DistToLeashed_Of(target) : LEASH_RADIUS + 1f;

            // In the protection ring with the target dragged out past the leash → try to peel onto a
            // closer target near the camp instead of following; each attempt burns a leash charge.
            if (distToLeash > LEASH_PROTECTION_RADIUS && distToLeash < LEASH_RADIUS
                && targetDistToLeash > LEASH_RADIUS
                && state != AIState.AI_RETREAT && _leashCounter < LEASH_COUNTER_THRESHOLD)
            {
                FindNewTarget();
                if (_monster.TargetUnit != null)
                {
                    _leashCounter++;
                }
            }
            // Dragged fully past the leash → start regenerating and retreat to camp.
            else if (distToLeash > LEASH_RADIUS && state != AIState.AI_RETREAT)
            {
                _regen.Start();
                Retreat();
            }

            // Standing still with nothing to do → re-acquire.
            if (state == AIState.AI_ATTACK && MovementStopped && _monster.CanMove())
            {
                FindNewTarget();
            }

            // Reached the camp on a retreat → finish; otherwise keep re-issuing the walk back.
            if (state == AIState.AI_RETREAT && MovementStopped)
            {
                if (distToLeash < RETREAT_ARRIVE_DISTANCE)
                {
                    OnStoppedMoving();
                }
                else
                {
                    Retreat();
                }
            }
        }

        // Leashed.lua OnStoppedMoving: arrived back at camp from a retreat.
        private void OnStoppedMoving()
        {
            if (CurrentState != AIState.AI_RETREAT)
            {
                return;
            }

            // Smoothly turn back to the spawn orientation (Leashed SetLeashOrientation) rather than snap.
            _monster.FaceDirection(_leashedOrientation, isInstant: false);
            _leashCounter = 0;
            NetSetState(AIState.AI_ATTACK);
            _monster.RoamState = _originalRoamState;
            // Regen keeps running (clamped at max HP) until the next engage Stops it — faithful to lua.
        }

        // ---------------- Target maintenance ----------------

        // Leashed.lua TimerAttack (every tick): keep a valid target; range-based auto-attack on/off is
        // engine-driven (target + AttackTo), so this only re-acquires when the target is gone.
        private void TimerAttack()
        {
            if (_monster == null || _monster.IsDead
                || Roaming(MinionRoamState.Inactive) || Roaming(MinionRoamState.RunInFear)
                || CurrentState == AIState.AI_RETREAT)
            {
                return;
            }

            if (CurrentState == AIState.AI_ATTACK || CurrentState == AIState.AI_TAUNTED)
            {
                if (_monster.TargetUnit == null)
                {
                    FindNewTarget();
                }
            }
        }

        // Leashed.lua FindNewTarget: a target near the camp → engage; otherwise regen + retreat.
        private void FindNewTarget()
        {
            if (_monster == null || _monster.IsDead
                || Roaming(MinionRoamState.Inactive) || Roaming(MinionRoamState.RunInFear)
                || CurrentState == AIState.AI_RETREAT)
            {
                return;
            }

            AttackableUnit target = FindTargetNear(_leashedPos, LEASH_RADIUS);
            if (target != null && DistToLeashed_Of(target) <= LEASH_RADIUS)
            {
                _regen.Stop();
                SetStateAndCloseToTarget(AIState.AI_ATTACK, target);
            }
            else
            {
                _regen.Start();
                Retreat();
            }
        }

        // Engine dropped our target (died / invalid) — re-acquire immediately.
        private void OnOwnerTargetLost(AttackableUnit lostTarget)
        {
            if (_monster == null || _monster.IsDead || CurrentState == AIState.AI_HALTED)
            {
                return;
            }

            FindNewTarget();
        }
    }
}
