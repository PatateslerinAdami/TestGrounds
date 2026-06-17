using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior;
using LeagueSandbox.GameServer.API;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace AIScripts
{
    // Champion-bot brain — faithful port of Riot's utility-task bot (Bot.lua + Task*.lua, 4.20), rebuilt
    // on BaseAIScript so bots join the uniform Order/State model and own combat selection
    // (ScriptOwnsCombatSelection = true). Replaces the legacy BasicAI/EzrealBot once complete.
    //
    // Scheduler (Bot.lua): a list of BotTask objects; each tick every task scores itself
    // (UpdatePriority), the highest-priority task becomes active (BeginTask on switch, hysteresis: only
    // takes over if it strictly beats the current one), and the active task Ticks. Done tasks are dropped.
    // Task set is built on a 1s delayed init (Riot TimerHackDelayedInit) so the map (structures/heroes)
    // exists first.
    //
    // NOTE on fidelity: this is Riot's actual 4.20 bot script, which is crude by design — the Retreat
    // task has a 0.75 priority floor that dominates, so bots lean heavily toward retreating. We port it
    // faithfully (incl. that quirk) rather than "fixing" it. This is the first slice: scheduler + Wander
    // + Retreat. PushLane/KillMinion/KillHero/CastSpell/structures/economy follow.
    public class BotAI : BaseAIScript
    {
        // Bot leash radius used to normalise distance terms in the task priority formulas
        // (dist / sqrt(MaxTravelDistSquared)). Riot exposes this as GetMaxTravelDistSquared(); the real
        // value is NOT recovered from the decomp — this is an UNVERIFIED placeholder (~SR lane length).
        // It only scales priorities (doesn't gate behaviour), but tune/verify it against the live server.
        private const float MAX_TRAVEL_DIST = 13000.0f;

        private readonly List<BotTask> _tasks = new List<BotTask>();
        private BotTask _activeTask;
        private bool _tasksBuilt;

        protected override void OnActivateBehavior()
        {
            Owner.ScriptOwnsCombatSelection = true;
            NetSetState(AIState.AI_IDLE);

            // Delayed init (Riot TimerHackDelayedInit, 1s, one-shot) — build the task set once the map is up.
            InitTimer("BotDelayedInit", 1.0f, false, BuildTasks);
            // Scheduler tick (Bot.lua TimerUpdate). 0.25s = Riot's decision cadence.
            InitTimer("BotSchedule", 0.25f, true, RunScheduler);
        }

        // Bot.lua TimerHackDelayedInit: assemble the task list. First slice = the two singletons that
        // need no map data; PushLane (per-lane), DefendStructure (per-structure), KillHero (per-enemy)
        // get added in later phases.
        private void BuildTasks()
        {
            _tasks.Add(new TaskWander { Name = "Wander" });
            _tasks.Add(new TaskRetreat { Name = "Retreat" });
            _tasks.Add(new TaskKillMinion { Name = "KillLowHPMinion" });
            // One PushLane per lane (Riot builds per-lane push tasks); anti-stacking spreads bots across them.
            _tasks.Add(new TaskPushLane { Name = "Push Lane R", LaneID = Lane.LANE_R });
            _tasks.Add(new TaskPushLane { Name = "Push Lane C", LaneID = Lane.LANE_C });
            _tasks.Add(new TaskPushLane { Name = "Push Lane L", LaneID = Lane.LANE_L });
            // One KillHero per enemy champion (Riot builds per-hero tasks in delayed init).
            foreach (var hero in GetHeroes(Owner.Team.GetEnemyTeam()))
            {
                _tasks.Add(new TaskKillHero { Name = "Kill " + hero.Name, Target = hero });
            }
            _tasks.Add(new TaskCastSpell { Name = "CastSpell" });
            _tasks.Add(new TaskKillTower { Name = "KillNearbyStruct" });
            _tasks.Add(new TaskBuyItem { Name = "Buy Item" });
            // TaskDefendStructure is intentionally omitted: Riot disabled it in 4.20 — its priority in
            // TaskDefendStructure.lua is `0 * (...)` (always 0, never activates), so it would be a dead task.
            _tasksBuilt = true;
        }

        // Bot.lua TimerUpdate: score all tasks, drop Done, pick the max, switch on strict improvement,
        // then Tick the active task.
        private void RunScheduler()
        {
            if (!_tasksBuilt)
            {
                return;
            }

            BotTask best = null;
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                BotTask t = _tasks[i];
                t.UpdatePriority(this);
                if (t.Done)
                {
                    if (_activeTask == t)
                    {
                        _activeTask = null;
                    }
                    _tasks.RemoveAt(i);
                    continue;
                }
                if (best == null || t.Priority > best.Priority)
                {
                    best = t;
                }
            }

            // Hysteresis (Bot.lua): only switch when the best STRICTLY beats the active task's priority,
            // so equal-priority tasks don't thrash the active selection every tick.
            if (best != null && best != _activeTask
                && (_activeTask == null || best.Priority > _activeTask.Priority))
            {
                _activeTask = best;
                best.BeginTask(this);
            }

            _activeTask?.Tick(this);
        }

        // ---- Task-facing "bot context" (the Lua globals: GetPos/GetRegroupPos/GetMaxTravelDistSquared/…),
        //      wrapping BaseAIScript's protected movement/targeting helpers. ----

        public Vector2 Position => Owner.Position;
        public float Hp => Owner.Stats.CurrentHealth;
        public AIState State => CurrentState;
        public float Now => LocalTime;                       // seconds (BaseAIScript LocalTime)
        public bool BotIsMoving => IsMoving();
        public Vector2 RegroupPos => GetFountainPosition(Owner.Team);
        public float MaxTravelDistSquared => MAX_TRAVEL_DIST * MAX_TRAVEL_DIST;
        public TeamId OtherTeam => Owner.Team.GetEnemyTeam();

        public static float Dist(Vector2 a, Vector2 b) => Vector2.Distance(a, b);
        public Vector2 WanderPoint(float distance) => MakeWanderPoint(Owner.Position, distance);

        public void MoveToPoint(AIState state, Vector2 pos) => SetStateAndMove(state, pos);
        public void CloseToTarget(AIState state, AttackableUnit target) => SetStateAndCloseToTarget(state, target);
        public void SetBotState(AIState state) => NetSetState(state);
        public void StopMoving() => Owner.StopMovement();
        // Riot TurnOffAutoAttack(reason): for these tasks "stop fighting" = drop the combat target.
        public void StopAttacking() => Owner.SetTargetUnit(null);

        // ---- combat target / spell-cast helpers (TaskKillHero / TaskCastSpell) ----

        public uint CurrentTargetNetId => Owner.TargetUnit?.NetId ?? 0u;

        public bool HasEnemyTarget =>
            Owner.TargetUnit != null && Owner.TargetUnit.Team != Owner.Team && !Owner.TargetUnit.IsDead;

        public float ManaRatio
        {
            get
            {
                float max = Owner.Stats.ManaPoints.Total;
                return max > 0f ? Owner.Stats.CurrentMana / max : 0.0f;
            }
        }

        // Generic bot spell use (TaskCastSpell): cast the first ready + castable ability (Q/W/E/R = slots
        // 0-3) at the current target through the normal cast pipeline. Spell.Cast validates mana/range/
        // targeting and no-ops (or queues) if invalid, so calling it blindly is safe. One cast per call.
        public void TryCastReadyAbilityAtTarget()
        {
            AttackableUnit target = Owner.TargetUnit;
            if (target == null)
            {
                return;
            }
            Vector2 pos = target.Position;
            for (short slot = 0; slot <= 3; slot++)
            {
                if (Owner.Spells.TryGetValue(slot, out var spell)
                    && spell != null
                    && spell.State == SpellState.STATE_READY
                    && Owner.CanCast(spell))
                {
                    spell.Cast(pos, pos, target);
                    return;
                }
            }
        }

        // ---- minion queries (GetMinions / GetMinionDistanceToLane / GetAreAllBarracksStarted) ----

        public TeamId Team => Owner.Team;
        public float AttackRange => Owner.Stats.Range.Total;
        public float MaxHp => Owner.Stats.HealthPoints.Total;
        public float Gold => Owner.Stats.Gold;
        public static float DistSq(Vector2 a, Vector2 b) => Vector2.DistanceSquared(a, b);

        // ---- structures / economy (TaskKillTower / TaskBuyItem) ----
        public List<AttackableUnit> EnemyStructures() => GetStructures(Owner.Team.GetEnemyTeam());
        public int ItemPrice(int itemId) => GetItemData(itemId)?.TotalPrice ?? 0;
        public bool BuyItem(int itemId) => ApiFunctionManager.BuyItem(Owner, itemId);

        // Number of alive ENEMY champions within range of the bot (threat estimate for TaskRetreat).
        public int EnemyChampionsNear(float range)
        {
            TeamId enemy = Owner.Team.GetEnemyTeam();
            int count = 0;
            foreach (var champ in GetChampionsInRange(Owner.Position, range, true))
            {
                if (champ.Team == enemy)
                {
                    count++;
                }
            }
            return count;
        }

        // Riot GetAreAllBarracksStarted: proxy = friendly lane minions exist (waves are out).
        public bool AreAllBarracksStarted => GetMinions(Owner.Team).Count > 0;
        public List<LaneMinion> FriendlyMinions() => GetMinions(Owner.Team);
        public List<LaneMinion> EnemyMinions() => GetMinions(Owner.Team.GetEnemyTeam());
        public List<LaneMinion> FriendlyMinionsOnLane(Lane lane) => GetMinions(Owner.Team, lane);

        // Name of the task this bot is currently running (Riot GetActiveTaskName) — used cross-bot for
        // PushLane anti-stacking.
        public string ActiveTaskName => _activeTask?.Name ?? "";

        // How many OTHER allied bots are currently running the task with this name (PushLane uses it to
        // spread bots across lanes — a lane already covered by another bot scores lower).
        public int AlliedBotsOnTask(string taskName)
        {
            int count = 0;
            foreach (var champ in GetHeroes(Owner.Team))
            {
                if (!ReferenceEquals(champ, Owner) && (champ.AIScript as BotAI)?.ActiveTaskName == taskName)
                {
                    count++;
                }
            }
            return count;
        }

        // Riot GetMinionDistanceToLane: shortest distance from the minion to its own lane polyline
        // (LaneMinion.PathingWaypoints). Used to confirm a minion is actually on its lane.
        public static float MinionDistanceToLane(LaneMinion minion)
        {
            var wp = minion.PathingWaypoints;
            if (wp == null || wp.Count == 0)
            {
                return float.MaxValue;
            }
            Vector2 p = minion.Position;
            if (wp.Count == 1)
            {
                return Vector2.Distance(p, wp[0]);
            }
            float best = float.MaxValue;
            for (int i = 0; i < wp.Count - 1; i++)
            {
                best = Math.Min(best, DistPointToSegment(p, wp[i], wp[i + 1]));
            }
            return best;
        }

        private static float DistPointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.LengthSquared();
            if (len2 < 1e-6f)
            {
                return Vector2.Distance(p, a);
            }
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
