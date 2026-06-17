using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Scripting.CSharp;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior
{
    /// <summary>
    /// Base class for the rebuilt entity AI (minion / monster / pet / bot), mirroring Riot's Lua AI
    /// runtime: a 0.25s "AI Priority List" decision sweep plus event-driven immediate reevaluation,
    /// a per-unit event bus (mirrors EventSystem), and a stack of pluggable <see cref="IAIComponent"/>s
    /// (mirrors AddComponent). Concrete AIs override <see cref="OnActivateBehavior"/> (register
    /// components) and <see cref="OnDecisionTick"/> (the per-sweep decision). The SetState* helpers
    /// map Riot's state verbs onto our existing ObjAIBase order / movement / target API
    /// (OrderType is already our Riot-aligned state vocabulary).
    /// </summary>
    public abstract class BaseAIScript : IAIScript
    {
        public AIScriptMetaData AIScriptMetaData { get; set; } = new AIScriptMetaData();

        /// <summary>Riot's AI Priority List interval (0.25s = 250ms).</summary>
        public const float DECISION_INTERVAL = 250.0f;

        protected ObjAIBase Owner { get; private set; }

        /// <summary>The AI's current logical state (Riot AI_* state), backed by ObjAIBase._aiState.</summary>
        public AIState CurrentState => Owner.GetAIState();

        /// <summary>When true, the shared CrowdControlComponent handles taunt the non-aggressive way
        /// (Riot AIComponentNonAggressiveTauntBehavior): the unit STOPS instead of running to and
        /// attacking the taunter. Default false = DefaultTauntBehavior (walk to + attack). The Scuttle
        /// Crab (RiverCrabAI) overrides this to true.</summary>
        public virtual bool NonAggressiveTaunt => false;

        private readonly List<IAIComponent> _components = new List<IAIComponent>();
        private readonly List<AITimer> _timers = new List<AITimer>();
        private readonly Dictionary<AIEvent, List<Action<AttackableUnit>>> _eventHandlers = new();
        private float _decisionTimer;
        private bool _reevaluateRequested;
        private bool _halted;
        // CC bits we poll each tick to emit begin/end events (mirrors Riot's OnTaunt/Fear/CharmBegin/End).
        private const StatusFlags CC_FLAGS = StatusFlags.Taunted | StatusFlags.Feared | StatusFlags.Charmed;
        private StatusFlags _lastCC;

        // ---------------- IAIScript ----------------

        public void OnActivate(ObjAIBase owner)
        {
            Owner = owner;
            // IAIScript has no OnDeactivate, so halt components on death (detach + ComponentHalt).
            ApiEventManager.OnDeath.AddListener(this, owner, OnOwnerDeath);
            // Every BaseAIScript-derived AI (minion, champion HeroAI, monster, pet) gets the shared
            // crowd-control driver by default, so fear/flee/taunt is AI-driven uniformly across unit
            // types (Riot's model). Added before OnActivateBehavior so its event subscriptions run
            // first (CC drives, then the concrete AI re-acquires on CC-end). See CrowdControlComponent.
            AddComponent(new CrowdControlComponent());
            OnActivateBehavior();
            Emit(AIEvent.ComponentInit);
        }

        private void OnOwnerDeath(DeathData data)
        {
            Halt();
        }

        public void OnUpdate(float diff)
        {
            if (Owner == null || Owner.IsDead)
            {
                return;
            }

            LocalTime += diff / 1000f;

            // Process CC transitions (emits OnFearBegin/End etc.) BEFORE the component/timer update
            // logic, so a component reacting to those events sees the correct state THIS tick. If the
            // components ran first, a component would act one extra tick on stale state right after CC
            // ends — e.g. the CrowdControlComponent issued a spurious post-flee move (to a random
            // wander point, since the buff already cleared CrowdControlSource): the "unit walks to a
            // mystery spot after flee ends" bug.
            PollCrowdControl();

            for (int i = 0; i < _components.Count; i++)
            {
                _components[i].OnUpdate(diff);
            }

            UpdateTimers(diff);
            OnTick(diff);

            _decisionTimer += diff;
            if (_reevaluateRequested || _decisionTimer >= DECISION_INTERVAL)
            {
                _reevaluateRequested = false;
                _decisionTimer = 0f;
                OnDecisionTick(diff);
            }
        }

        public void OnCallForHelp(AttackableUnit attacker, AttackableUnit victim)
        {
            Emit(AIEvent.OnCallForHelp, attacker);
            OnCallForHelpBehavior(attacker, victim);
        }

        // ---------------- Subclass hooks ----------------

        /// <summary>Called once on activation — register components and init state here.</summary>
        protected virtual void OnActivateBehavior() { }

        /// <summary>
        /// Runs every server tick (before the decision gate). Per-tick bookkeeping and the cheap
        /// "should I reevaluate now?" event checks go here; call <see cref="RequestReevaluation"/>
        /// to force the decision sweep this tick (target died, call-for-help, ...).
        /// </summary>
        protected virtual void OnTick(float diff) { }

        /// <summary>
        /// The 0.25s decision sweep (or an event-forced reevaluation via RequestReevaluation).
        /// The recommended place for core decision logic in new AIs. Optional: an AI that manages
        /// its own sweep timer inside <see cref="OnTick"/> can leave this empty.
        /// </summary>
        protected virtual void OnDecisionTick(float diff) { }

        protected virtual void OnCallForHelpBehavior(AttackableUnit attacker, AttackableUnit victim) { }

        /// <summary>
        /// Force the decision sweep to run on the next tick instead of waiting for the 0.25s timer
        /// — Riot reevaluates immediately on CC / target death / collision.
        /// </summary>
        protected void RequestReevaluation()
        {
            _reevaluateRequested = true;
        }

        // ---------------- Crowd-control event emission ----------------

        /// <summary>
        /// Polls the owner's CC status each tick and emits begin/end events on transitions, then
        /// forces an immediate reevaluation (Riot reevaluates the moment CC starts/ends). The
        /// OnUnitCrowdControlled dispatcher isn't reliably published, so we poll StatusFlags.
        /// </summary>
        private void PollCrowdControl()
        {
            StatusFlags current = Owner.Status;
            PollCCFlag(current, StatusFlags.Taunted, AIEvent.OnTauntBegin, AIEvent.OnTauntEnd);
            PollCCFlag(current, StatusFlags.Feared, AIEvent.OnFearBegin, AIEvent.OnFearEnd);
            PollCCFlag(current, StatusFlags.Charmed, AIEvent.OnCharmBegin, AIEvent.OnCharmEnd);
            _lastCC = current & CC_FLAGS;
        }

        private void PollCCFlag(StatusFlags current, StatusFlags flag, AIEvent begin, AIEvent end)
        {
            bool now = current.HasFlag(flag);
            bool was = _lastCC.HasFlag(flag);
            if (now == was)
            {
                return;
            }

            Emit(now ? begin : end);
            RequestReevaluation();
        }

        // ---------------- Components (AddComponent) ----------------

        protected T AddComponent<T>(T component) where T : IAIComponent
        {
            _components.Add(component);
            component.OnAttach(this, Owner);
            return component;
        }

        /// <summary>Stops all components (Riot ComponentHalt) — call on death/despawn.</summary>
        public void Halt()
        {
            if (_halted)
            {
                return;
            }

            _halted = true;
            Emit(AIEvent.ComponentHalt);
            for (int i = 0; i < _components.Count; i++)
            {
                _components[i].OnDetach();
            }
        }

        // ---------------- Event bus (RegisterForEvent / Event) ----------------

        public void Subscribe(AIEvent ev, Action<AttackableUnit> handler)
        {
            if (!_eventHandlers.TryGetValue(ev, out var list))
            {
                list = new List<Action<AttackableUnit>>();
                _eventHandlers[ev] = list;
            }

            list.Add(handler);
        }

        public void Emit(AIEvent ev, AttackableUnit unit = null)
        {
            if (_eventHandlers.TryGetValue(ev, out var list))
            {
                // Snapshot count; handlers don't add/remove during dispatch in normal use.
                for (int i = 0; i < list.Count; i++)
                {
                    list[i](unit);
                }
            }
        }

        // ---------------- Named timers (InitTimer / StopTimer / ResetAndStartTimer) ----------------

        private sealed class AITimer
        {
            public string Name;
            public float Interval;   // seconds
            public bool Repeating;
            public Action Callback;
            public bool Running;
            public float Elapsed;    // seconds
        }

        private AITimer FindTimer(string name)
        {
            for (int i = 0; i < _timers.Count; i++)
            {
                if (_timers[i].Name == name)
                {
                    return _timers[i];
                }
            }

            return null;
        }

        /// <summary>Register (or reconfigure) a named timer and start it. Mirrors Riot's InitTimer.
        /// An interval of 0 fires every tick. <paramref name="callback"/> runs when it elapses.</summary>
        protected void InitTimer(string name, float intervalSeconds, bool repeating, Action callback)
        {
            AITimer t = FindTimer(name);
            if (t == null)
            {
                t = new AITimer { Name = name };
                _timers.Add(t);
            }

            t.Interval = intervalSeconds;
            t.Repeating = repeating;
            t.Callback = callback;
            t.Running = true;
            t.Elapsed = 0f;
        }

        protected void StopTimer(string name)
        {
            AITimer t = FindTimer(name);
            if (t != null)
            {
                t.Running = false;
            }
        }

        protected void StartTimer(string name)
        {
            AITimer t = FindTimer(name);
            if (t != null)
            {
                t.Running = true;
            }
        }

        protected void ResetAndStartTimer(string name)
        {
            AITimer t = FindTimer(name);
            if (t != null)
            {
                t.Elapsed = 0f;
                t.Running = true;
            }
        }

        private void UpdateTimers(float diffMs)
        {
            float diffSec = diffMs / 1000f;
            // Capture count up front: callbacks may InitTimer (append) — those run next tick, not
            // mid-iteration. StopTimer/ResetAndStartTimer mutate existing entries in place (safe).
            int count = _timers.Count;
            for (int i = 0; i < count; i++)
            {
                AITimer t = _timers[i];
                if (!t.Running)
                {
                    continue;
                }

                t.Elapsed += diffSec;
                if (t.Elapsed >= t.Interval)
                {
                    if (t.Repeating)
                    {
                        t.Elapsed = 0f;
                    }
                    else
                    {
                        t.Running = false;
                    }

                    t.Callback();
                }
            }
        }

        // ---------------- Targeting / ignore / movement helpers ----------------

        private static readonly Random _rng = new Random();
        private readonly Dictionary<uint, float> _ignored = new Dictionary<uint, float>();

        /// <summary>Seconds since this AI activated — drives the ignore list, wander timing, etc.</summary>
        protected float LocalTime { get; private set; }

        /// <summary>Riot AddToIgnore(time): temporarily ignore the current target when re-acquiring.</summary>
        protected void AddToIgnore(float seconds)
        {
            if (Owner.TargetUnit != null)
            {
                _ignored[Owner.TargetUnit.NetId] = LocalTime + seconds;
            }
        }

        protected bool IsIgnored(AttackableUnit u)
        {
            return _ignored.TryGetValue(u.NetId, out float until) && until > LocalTime;
        }

        /// <summary>Riot IsMoving(): the unit has an active path it hasn't finished.</summary>
        protected bool IsMoving()
        {
            return !Owner.IsPathEnded();
        }

        /// <summary>
        /// Riot FindTargetInAcR(): the best enemy in the owner's acquisition range — lowest
        /// ClassifyUnit priority value first, then nearest. Skips dead / not-visible /
        /// not-targetable / ignored units. Returns null if none.
        /// </summary>
        protected virtual AttackableUnit FindTargetInAcR()
        {
            return FindTargetNear(Owner.Position, Owner.Stats.AcquisitionRange.Total);
        }

        /// <summary>
        /// Riot FindTargetNearPosition(pos, range): the best enemy within <paramref name="range"/> of
        /// an arbitrary <paramref name="center"/> (not the owner's position) — same priority/distance
        /// ranking and skip filters as <see cref="FindTargetInAcR"/>. The jungle Leashed AI uses this
        /// with its leashed (camp) position so it only acquires targets near the camp, not wherever it
        /// has wandered. Distance is measured from <paramref name="center"/>.
        /// </summary>
        protected AttackableUnit FindTargetNear(Vector2 center, float range)
        {
            AttackableUnit best = null;
            int bestPriority = int.MaxValue;
            float bestDistSq = range * range;

            foreach (var obj in ApiFunctionManager.EnumerateUnitsInRange(center, range, true))
            {
                if (obj is not AttackableUnit u || u.IsDead || u.Team == Owner.Team)
                {
                    continue;
                }

                if (!u.IsVisibleByTeam(Owner.Team) || !u.Status.HasFlag(StatusFlags.Targetable) || IsIgnored(u))
                {
                    continue;
                }

                float effective = range + u.CollisionRadius;
                float distSq = Vector2.DistanceSquared(center, u.Position);
                if (distSq >= effective * effective)
                {
                    continue;
                }

                if (!IsAcquirableTarget(u))
                {
                    continue;
                }

                int priority = GetTargetPriority(u);
                if (best == null || priority < bestPriority || (priority == bestPriority && distSq < bestDistSq))
                {
                    best = u;
                    bestPriority = priority;
                    bestDistSq = distSq;
                }
            }

            return best;
        }

        /// <summary>
        /// Candidate filter applied before the ranking tournament in <see cref="FindTargetNear"/> —
        /// Riot's <c>FindTargetInAcRWithFilter</c>: an AI restricts the candidate set to whole unit
        /// classes (e.g. BaronMinionAI acquires <c>AI_TARGET_MINIONS</c> only). Default accepts every
        /// enemy. NOTE: a normal lane minion does NOT filter here — it relies on the ranking so a lone
        /// champion is still picked as a last resort (4.20 behaviour), so LaneMinionAI keeps the default.
        /// </summary>
        protected virtual bool IsAcquirableTarget(AttackableUnit target)
        {
            return true;
        }

        /// <summary>
        /// Target ranking for <see cref="FindTargetInAcR"/> (lower = preferred; ties break on
        /// distance). Defaults to the engine <see cref="ObjAIBase.ClassifyTarget"/> ordering;
        /// per-type AIs override to match their <c>IsBetterThanGivenTarget</c> tournament.
        /// </summary>
        protected virtual int GetTargetPriority(AttackableUnit target)
        {
            return (int)Owner.ClassifyTarget(target);
        }

        /// <summary>Riot MakeWanderPoint(center, dist): a random point within dist of center.</summary>
        public Vector2 MakeWanderPoint(Vector2 center, float distance)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            float r = (float)(_rng.NextDouble() * distance);
            return center + new Vector2((float)Math.Cos(angle) * r, (float)Math.Sin(angle) * r);
        }

        /// <summary>Riot MakeFleePoint(): a point directly away from a threat position.</summary>
        public Vector2 MakeFleePoint(Vector2 awayFrom, float distance)
        {
            Vector2 dir = Owner.Position - awayFrom;
            if (dir.LengthSquared() < 0.001f)
            {
                dir = new Vector2(1f, 0f);
            }

            dir = Vector2.Normalize(dir);
            return Owner.Position + dir * distance;
        }

        // ---------------- SetState* helpers (Riot verbs -> AIState + ObjAIBase API) ----------------
        // These take the Riot AIState so CurrentState stays the source of truth, and map onto our
        /// <summary>
        /// Riot Hero.lua/Aggro.lua OnOrder: translate an incoming order (player/script intent) into AI
        /// state. Part 1 of the Order/State split (docs/AI_ORDER_STATE_SPLIT_PLAN.md). Default is a
        /// no-op — order handling stays where it is today (HandleMove for champions); only AIs that
        /// override this (HeroAI) react. Phase 1 is label-only (sets _aiState in parallel, the brain
        /// still reads MoveOrder), so this must NOT (re)issue movement.
        /// </summary>
        public virtual void OnOrder(OrderType order, AttackableUnit target, Vector2 pos)
        {
        }

        // movement/target/order API. NetSetState changes state without (re)issuing a move order.

        /// <summary>Set the logical AI state without issuing a movement order (Riot NetSetState).</summary>
        public void NetSetState(AIState state)
        {
            Owner.SetAIState(state);
        }

        /// <summary>Set the AI state and issue the matching move order (Riot SetState).</summary>
        protected void SetState(AIState state, OrderType order)
        {
            Owner.SetAIState(state);
            Owner.UpdateMoveOrder(order);
        }

        /// <summary>Riot SetStateAndCloseToTarget(state, target): target it and move/attack toward it.</summary>
        public void SetStateAndCloseToTarget(AIState state, AttackableUnit target)
        {
            Owner.SetAIState(state);
            Owner.SetTargetUnit(target);
            Owner.UpdateMoveOrder(OrderType.AttackTo);
        }

        /// <summary>Riot SetStateAndMove(state, pos): path to a destination and move.</summary>
        public void SetStateAndMove(AIState state, Vector2 destination)
        {
            Owner.SetAIState(state);
            var path = ApiFunctionManager.GetPath(Owner.Position, destination, Owner.PathfindingRadius);
            if (path != null && path.Count > 0)
            {
                Owner.SetWaypoints(path);
            }

            Owner.UpdateMoveOrder(OrderType.MoveTo);
        }

        /// <summary>
        /// Target a unit and attack it IN PLACE — the engine still auto-attacks a target in range but
        /// issues no chase path while MoveOrder is Hold (Riot's pet HOLDPOSITION/HARDIDLE attacking
        /// states use SetTarget without close-to-target). Sets the state AFTER UpdateMoveOrder so the
        /// internal Hold→AI_IDLE coherence sync (UpdateMoveOrder, for ScriptOwnsCombatSelection units)
        /// does not clobber the requested state.
        /// </summary>
        public void SetTargetNoChase(AIState state, AttackableUnit target)
        {
            Owner.SetTargetUnit(target);
            Owner.UpdateMoveOrder(OrderType.Hold);
            Owner.SetAIState(state);
        }
    }
}
