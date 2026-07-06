using GameServerCore.Enums;
using System.Numerics;

namespace AIScripts
{
    // TaskWander.lua: the floor/fallback task (priority 0.001). Picks a random point within 250u every
    // few seconds and strolls to it — moves until within 80u, stops, then strolls to the next point.
    public class TaskWander : BotTask
    {
        private const float WANDER_RADIUS = 250.0f;
        private const float ARRIVE_DIST = 80.0f;
        // Riot re-picks at random(2,4)s; we use a fixed mid value (no per-task RNG needed for a floor task).
        private const float WANDER_RETIME = 3.0f;

        private float _nextWanderTime;
        private Vector2 _wanderPoint;

        public override void UpdatePriority(BotAI bot)
        {
            Priority = 0.001f;
        }

        public override void BeginTask(BotAI bot)
        {
            bot.StopAttacking();
        }

        public override void Tick(BotAI bot)
        {
            if (bot.Now >= _nextWanderTime)
            {
                _nextWanderTime = bot.Now + WANDER_RETIME;
                _wanderPoint = bot.WanderPoint(WANDER_RADIUS);
            }

            if (bot.State == AIState.AI_MOVE && BotAI.Dist(_wanderPoint, bot.Position) < ARRIVE_DIST)
            {
                bot.StopMoving();
                bot.SetBotState(AIState.AI_STOP);
            }

            if (bot.State == AIState.AI_STOP && BotAI.Dist(_wanderPoint, bot.Position) >= ARRIVE_DIST)
            {
                bot.MoveToPoint(AIState.AI_MOVE, _wanderPoint);
            }
        }
    }
}
