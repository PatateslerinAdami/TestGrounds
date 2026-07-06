using System;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace AIScripts
{
    // TaskPushLane.lua: follow the friendly minion wave ON A SPECIFIC LANE, attack-moving with it
    // (AI_SOFTATTACK engages enemies in range while advancing). Catch up when the wave pulls ahead
    // (> 300u), stop when close (< 150u). Riot builds one of these PER LANE; the scheduler holds a
    // TaskPushLane for LANE_R/C/L (BotAI.BuildTasks), and anti-stacking (AlliedBotsOnTask) lowers a lane's
    // priority when another bot already pushes it so bots spread out. (LaneMinion now stores its Lane, so
    // GetMinions(team, lane) filters per lane — the earlier single-lane simplification is gone.)
    public class TaskPushLane : BotTask
    {
        // Which lane this task pushes (set in BotAI.BuildTasks).
        public Lane LaneID;

        private const float MAX_DIST_TO_LANE = 500.0f;
        private const float MIN_FOLLOW_DIST_SQ = 5625.0f;   // 75²  — only follow minions a bit ahead
        private const float MIN_FIGHT_DIST_SQ = 22500.0f;   // 150² — close enough, stop
        private const float MAX_FIGHT_DIST_SQ = 90000.0f;   // 300² — fell behind, catch up

        // Nearest friendly lane minion that is on its lane and ahead of the bot (Lua FindNearestFriendly-
        // Minion). distSq returns the chosen minion's squared distance (or MaxTravel if none).
        private LaneMinion FindNearestFriendlyMinion(BotAI bot, out float distSq)
        {
            distSq = bot.MaxTravelDistSquared;
            LaneMinion best = null;
            foreach (var m in bot.FriendlyMinionsOnLane(LaneID))
            {
                float d = BotAI.DistSq(bot.Position, m.Position);
                if (BotAI.MinionDistanceToLane(m) < MAX_DIST_TO_LANE
                    && d > MIN_FOLLOW_DIST_SQ && d < distSq)
                {
                    distSq = d;
                    best = m;
                }
            }
            return best;
        }

        public override void UpdatePriority(BotAI bot)
        {
            if (!bot.AreAllBarracksStarted)
            {
                Priority = 0.0f;
                return;
            }
            LaneMinion minion = FindNearestFriendlyMinion(bot, out float distSq);
            if (minion == null)
            {
                Priority = 0.0f;
                return;
            }
            float maxTravel = (float)Math.Sqrt(bot.MaxTravelDistSquared);
            float dist = (float)Math.Sqrt(distSq);
            Priority = 0.3f * (1.0f - dist / maxTravel) * 0.2f + 0.24f;
            // Anti-stacking: drop priority for each OTHER allied bot already pushing this lane, so bots
            // spread across lanes instead of clumping (Lua counts heroes whose active task == this Name).
            Priority -= 0.10f * bot.AlliedBotsOnTask(Name);
            if (Priority < 0.0f)
            {
                Priority = 0.0f;
            }
        }

        public override void BeginTask(BotAI bot)
        {
            bot.StopAttacking();
            FollowClosestMinion(bot);
        }

        public override void Tick(BotAI bot)
        {
            LaneMinion minion = FindNearestFriendlyMinion(bot, out float distSq);
            if (minion == null)
            {
                return;
            }
            if (!bot.BotIsMoving && distSq > MAX_FIGHT_DIST_SQ)
            {
                FollowClosestMinion(bot);
            }
            else if (bot.BotIsMoving && distSq < MIN_FIGHT_DIST_SQ)
            {
                bot.StopMoving();
            }
        }

        private void FollowClosestMinion(BotAI bot)
        {
            LaneMinion minion = FindNearestFriendlyMinion(bot, out _);
            if (minion != null)
            {
                bot.MoveToPoint(AIState.AI_SOFTATTACK, minion.Position);
            }
        }
    }
}
