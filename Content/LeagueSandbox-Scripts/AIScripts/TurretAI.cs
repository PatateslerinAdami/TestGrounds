using GameServerCore.Enums;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;

namespace AIScripts
{
    // Faithful 1:1 port of Scripts/Turret.lua onto BaseAIScript (Phase T1 of
    // docs/TURRET_AI_PORT_PLAN.md). Replaces the old bare-IAIScript CheckForTargets() with Riot's
    // turret state machine: AI_HARDIDLE <-> AI_HARDIDLE_ATTACKING (+ AI_TAUNTED / AI_HALTED), a 0.15s
    // TimerFindEnemies sweep, and FindTargetInAcR + ClassifyTarget targeting shared with every other
    // archetype.
    //
    // The engine already owns turret combat EXECUTION: ObjAIBase.UpdateTarget drops invalid targets
    // (firing OnTargetLost), and auto-attacks TargetUnit whenever it is set and in range. Turrets never
    // move (BaseTurret.RefreshWaypoints is a no-op), so "engine attacks the script-set target in range"
    // is exactly Riot's model (Turret.lua calls TurnOnAutoAttack, the engine swings) — this is NOT the
    // H1 chase-loop divergence, which only affects mobile units. So this script's whole job is to
    // SET / HOLD / CLEAR TargetUnit per the Lua; the engine does the rest.
    //
    // Phase T2 (DONE): turretTargetList focus-lock + the important-CallForHelp channel. When an enemy
    // champion damages an allied champion in range, the engine routes an important CFH
    // (OnReceiveImportantCallForHelp) and the turret PUTS the attacker in turretTargetList. That list
    // takes absolute priority over FindTargetInAcR in HARDIDLE, so the turret sticks on the diver over
    // minions until the diver leaves attack range (UpdateTargetList prunes it) — the classic
    // tower-dive aggro lock. The regular CFH still does the immediate switch; the list makes it sticky.
    public class TurretAI : BaseAIScript
    {
        public TurretAI()
        {
            // Required so the engine's CallForHelp broadcast (ObjAIBase.TakeDamage) routes to this AI;
            // the old bare-IAIScript turret left this false and so received NO calls for help at all.
            AIScriptMetaData.HandlesCallsForHelp = true;
        }

        // Turret.lua InitTimer("TimerFindEnemies", 0.15, true).
        private const float DELAY_FIND_ENEMIES = 0.15f;

        private BaseTurret _turret;

        // ObjAIBase.UpdateTarget fires OnTargetLost from INSIDE SetTargetUnit(null) — a synchronous
        // publish that happens BEFORE TargetUnit is actually nulled (ObjAIBase.cs:2274 vs :2277). A
        // listener that re-acquired with SetTargetUnit there would be clobbered by the outer null
        // assignment. So OnTargetLost only flags; the re-acquire runs at the next safe point (OnTick,
        // which precedes this tick's UpdateTarget). Net effect = next-tick re-acquire, matching the
        // Lua's immediate OnTargetLost without the re-entrancy hazard.
        private bool _reacquireRequested;

        // Turret.lua turretTargetList — units forced onto the turret via important CallForHelp (a diver
        // attacking an allied champion). Entries here outrank FindTargetInAcR while in HARDIDLE.
        private readonly List<AttackableUnit> _turretTargetList = new List<AttackableUnit>();

        // ---------------- Lifecycle (Turret.lua OnInit) ----------------

        protected override void OnActivateBehavior()
        {
            _turret = Owner as BaseTurret;
            if (_turret == null)
            {
                return;
            }

            // Auto-attack firing is driven by the shared AutoAttackComponent (attached by BaseAIScript):
            // it turns auto-attack on/off by range, exactly like Turret.lua's explicit
            // TurnOnAutoAttack/TurnOffAutoAttack. This script keeps only target SELECTION + state.

            // SetState(AI_HARDIDLE) + InitTimer("TimerFindEnemies", 0.15, true).
            InitTimer("TimerFindEnemies", DELAY_FIND_ENEMIES, true, TimerFindEnemies);

            // Engine-initiated target drops (target died / went untargetable / left vision). See
            // _reacquireRequested for why this defers instead of re-acquiring inline.
            ApiEventManager.OnTargetLost.AddListener(this, _turret, OnOwnerTargetLost, false);

            // Turret.lua OnTauntEnd — taunt BEGIN is handled by the shared CrowdControlComponent
            // (it sets AI_TAUNTED + targets the taunter); we only need to leave the TAUNTED state and
            // re-acquire when the taunt ends, or the state machine would stay locked on the taunter.
            Subscribe(AIEvent.OnTauntEnd, _ => OnTauntEnd());

            NetSetState(AIState.AI_HARDIDLE);
        }

        // State-gated auto-attack (option C, Turret.lua TimerCheckAttack / TurnOnAutoAttack): a turret
        // only swings while attacking — AI_HARDIDLE_ATTACKING (acquired / CFH target) or AI_TAUNTED. In
        // AI_HARDIDLE / AI_HALTED it has no target anyway, so this is behaviour-identical (the turret sets
        // the attacking state BEFORE handing the engine a target, lines 123/124 + 177/178 + CC taunt), and
        // makes the firing faithfully state-driven instead of merely target+range driven.
        public override bool AutoAttackStatePermits()
        {
            return CurrentState == AIState.AI_HARDIDLE_ATTACKING
                || CurrentState == AIState.AI_TAUNTED;
        }

        // ---------------- Per-tick (deferred re-acquire) ----------------

        protected override void OnTick(float diff)
        {
            if (_reacquireRequested)
            {
                _reacquireRequested = false;
                TimerFindEnemies();
            }
        }

        // ---------------- Core loop (Turret.lua TimerFindEnemies, 0.15s) ----------------

        private void TimerFindEnemies()
        {
            if (_turret == null || _turret.IsDead || CurrentState == AIState.AI_HALTED)
            {
                return;
            }

            // Turret.lua UpdateTargetList(): drop focus-lock entries that left attack range / went invalid.
            UpdateTargetList();

            // HARDIDLE: no target — pick the focus-lock target (turretTargetList[0]) if any, else the best
            // enemy in acquisition range. Turret AcquisitionRange == AttackRange (both 750 in 4.20 data),
            // so FindTargetInAcR (acquisition) and the attack gate below agree. Targeting goes through the
            // shared ClassifyTarget priority + filters. Note: FindTargetInAcR is consulted ONLY in HARDIDLE
            // — once attacking, the turret keeps its current target until it dies/leaves range (turret
            // target persistence; no per-scan switch).
            if (CurrentState == AIState.AI_HARDIDLE)
            {
                AttackableUnit newTarget = _turretTargetList.Count > 0 ? _turretTargetList[0] : FindTargetInAcR();
                if (newTarget == null)
                {
                    ClearTarget();
                    return;
                }

                NetSetState(AIState.AI_HARDIDLE_ATTACKING);
                SetTarget(newTarget);
            }

            // HARDIDLE_ATTACKING / TAUNTED: keep the current target while it is in attack range (the
            // shared AutoAttackComponent does the actual TurnOnAutoAttack firing); once it leaves range /
            // dies, drop to HARDIDLE + clear the target so the next sweep re-acquires (Turret.lua:
            // NetSetState(HARDIDLE) + TurnOffAutoAttack(MOVING)).
            if (CurrentState == AIState.AI_HARDIDLE_ATTACKING || CurrentState == AIState.AI_TAUNTED)
            {
                AttackableUnit target = _turret.TargetUnit;
                if (target == null || target.IsDead || !TargetInAttackRange(target))
                {
                    NetSetState(AIState.AI_HARDIDLE);
                    ClearTarget();
                }
            }
        }

        // ---------------- Event hooks ----------------

        // Turret.lua OnTargetLost — the engine dropped our target. Defer to OnTick (see
        // _reacquireRequested). Pre-set HARDIDLE here (safe: it does not touch TargetUnit) so the single
        // deferred sweep re-acquires a replacement in one tick instead of two.
        private void OnOwnerTargetLost(ObjAIBase owner, AttackableUnit lostTarget, TargetLostReason reason)
        {
            if (_turret == null || _turret.IsDead || CurrentState == AIState.AI_HALTED)
            {
                return;
            }

            if (CurrentState == AIState.AI_HARDIDLE_ATTACKING)
            {
                NetSetState(AIState.AI_HARDIDLE);
            }

            _reacquireRequested = true;
        }

        // Turret.lua OnCallForHelp(_, attacker): switch onto the attacker while idle or already
        // attacking. Our engine convention is OnCallForHelp(attacker, victim) with attacker FIRST
        // (ObjAIBase.cs:2592), so we engage `attacker` — same as LaneMinionAI. The engine's CFH
        // broadcast already gated the attacker to within the turret's range + cfh_TurretRadius, so the
        // Lua handler does no range check and neither do we (TimerFindEnemies drops it next sweep if it
        // is somehow out of attack range). The state guard naturally excludes TAUNTED / HALTED.
        protected override void OnCallForHelpBehavior(AttackableUnit attacker, AttackableUnit victim)
        {
            if (_turret == null || _turret.IsDead || attacker == null || attacker.IsDead)
            {
                return;
            }

            if (CurrentState == AIState.AI_HARDIDLE || CurrentState == AIState.AI_HARDIDLE_ATTACKING)
            {
                NetSetState(AIState.AI_HARDIDLE_ATTACKING);
                SetTarget(attacker);
            }
        }

        // Turret.lua OnTauntEnd — once the taunt ends GetTauntTarget() is nil, so it falls through to
        // HARDIDLE + TimerFindEnemies (re-acquire). The deferred sweep picks the taunter back up if it
        // is still the best in-range target.
        private void OnTauntEnd()
        {
            if (_turret == null || _turret.IsDead || CurrentState == AIState.AI_HALTED)
            {
                return;
            }

            NetSetState(AIState.AI_HARDIDLE);
            _reacquireRequested = true;
        }

        // Turret.lua OnReceiveImportantCallForHelp(_, attacker): a diver attacking an allied champion —
        // lock onto the attacker by adding it to turretTargetList (priority over FindTargetInAcR). The
        // engine already gated the attacker to within turret range; we re-check ObjectInAttackRange as
        // the Lua does. Request a re-acquire so the lock takes effect immediately, not on the next sweep.
        protected override void OnImportantCallForHelpBehavior(AttackableUnit attacker, AttackableUnit victim)
        {
            if (_turret == null || _turret.IsDead || CurrentState == AIState.AI_HALTED || attacker == null)
            {
                return;
            }

            if (IsValidListTarget(attacker))
            {
                PutTargetInTargetList(attacker);
                _reacquireRequested = true;
            }
        }

        // ---------------- turretTargetList (Turret.lua) ----------------

        // Turret.lua PutTargetInTargetList: append if not already present.
        private void PutTargetInTargetList(AttackableUnit target)
        {
            if (!_turretTargetList.Contains(target))
            {
                _turretTargetList.Add(target);
            }
        }

        // Turret.lua UpdateTargetList: drop entries no longer in attack range (also dead / untargetable /
        // out of vision, so a stale lock can't pin the turret onto an invalid unit and busy-loop the
        // engine's drop-and-re-set).
        private void UpdateTargetList()
        {
            for (int i = _turretTargetList.Count - 1; i >= 0; i--)
            {
                if (!IsValidListTarget(_turretTargetList[i]))
                {
                    _turretTargetList.RemoveAt(i);
                }
            }
        }

        private bool IsValidListTarget(AttackableUnit u)
        {
            return u != null && !u.IsDead
                && u.IsTargetableByUnit(_turret)
                && u.IsVisibleByTeam(_turret.Team)
                && TargetInAttackRange(u); // Turret.lua ObjectInAttackRange
        }

        // ---------------- Lua verb helpers ----------------

        // Turret.lua SetTarget: hand the engine a target (selection). The AutoAttackComponent then turns
        // firing on/off by range. Re-set only on an actual change so we never reset a committed
        // auto-attack windup (SetTargetUnit cancels the swing on a switch, ObjAIBase.cs:2281).
        private void SetTarget(AttackableUnit target)
        {
            if (_turret.TargetUnit != target)
            {
                _turret.SetTargetUnit(target, true);
            }
        }

        // Drop the current target (selection). The AutoAttackComponent stops firing once TargetUnit is
        // null/changed, and the engine cancels any in-flight windup in UpdateTarget — so this alone ends
        // the attack; no explicit TurnOffAutoAttack toggle call is needed here.
        private void ClearTarget()
        {
            if (_turret.TargetUnit != null)
            {
                _turret.SetTargetUnit(null, true);
            }
        }

        // Turret.lua TargetInAttackRange() / ObjectInAttackRange(): edge-based — auto-attack range plus
        // both collision radii (the same expansion the engine's attack gate uses). Center-to-center.
        private bool TargetInAttackRange(AttackableUnit target)
        {
            float range = _turret.Stats.Range.Total + target.CollisionRadius + _turret.CollisionRadius;
            return Vector2.DistanceSquared(_turret.Position, target.Position) <= range * range;
        }

        // Turret.lua HaltAI: stop scanning, cancel the attack, park in AI_HALTED. Triggers (turret
        // invuln / disable) are not yet wired in our server; the guards above keep this correct if one
        // calls it later.
        public void HaltAI()
        {
            StopTimer("TimerFindEnemies");
            _turret?.TurnOffAutoAttack(AutoAttackStopReason.OtherImmediately); // STOPREASON_IMMEDIATELY
            _turret?.SetTargetUnit(null, true);
            NetSetState(AIState.AI_HALTED);
        }
    }
}
