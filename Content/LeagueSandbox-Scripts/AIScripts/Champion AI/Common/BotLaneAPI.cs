using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Logging;
using log4net;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace AIScripts.Common
{
    /// <summary>
    /// Shared API for bot lane selection and management.
    /// This provides a centralized way for bots to choose lanes and handle lane overflow.
    /// </summary>
    public static class BotLaneAPI
    {
        private static ILog _logger = LoggerProvider.GetLogger();

        /// <summary>
        /// Represents the different lanes available in the game
        /// </summary>
        public enum BotLane
        {
            Top,
            Mid,
            Bottom,
            None
        }

        // Track which bots are currently recalling to prevent multiple recalls at once
        private static HashSet<uint> _botsCurrentlyRecalling = new HashSet<uint>();

        // Track lane assignments per team - this is where bots are ASSIGNED to go, not where they currently are
        private static Dictionary<TeamId, Dictionary<uint, BotLane>> _laneAssignments = new Dictionary<TeamId, Dictionary<uint, BotLane>>();

        /// <summary>
        /// Determines if minions have spawned yet
        /// </summary>
        public static bool AreMinionsSpawned()
        {
            return IsMinionSpawnEnabled();
        }

        /// <summary>
        /// Determines if lane selection should be active (before minions spawn)
        /// </summary>
        public static bool ShouldSelectLanes()
        {
            return !AreMinionsSpawned();
        }

        /// <summary>
        /// Gets the current game time in seconds
        /// </summary>
        private static float GetGameTimeSeconds()
        {
            return GameTime() / 1000f;
        }

        /// <summary>
        /// Gets or creates the lane assignment dictionary for a team
        /// </summary>
        private static Dictionary<uint, BotLane> GetTeamAssignments(TeamId team)
        {
            if (!_laneAssignments.ContainsKey(team))
            {
                _laneAssignments[team] = new Dictionary<uint, BotLane>();
            }
            return _laneAssignments[team];
        }

        /// <summary>
        /// Assigns a bot to a specific lane
        /// </summary>
        public static void AssignBotToLane(Champion bot, BotLane lane)
        {
            if (bot == null) return;

            var assignments = GetTeamAssignments(bot.Team);
            assignments[bot.NetId] = lane;
            _logger.Debug($"Assigned bot {bot.Name} (NetId: {bot.NetId}) to lane {lane}");
        }

        /// <summary>
        /// Gets the assigned lane for a bot
        /// </summary>
        public static BotLane GetAssignedLane(Champion bot)
        {
            if (bot == null) return BotLane.None;

            var assignments = GetTeamAssignments(bot.Team);
            if (assignments.TryGetValue(bot.NetId, out BotLane lane))
            {
                return lane;
            }
            return BotLane.None;
        }

        /// <summary>
        /// Gets the number of bots assigned to a specific lane for a team
        /// </summary>
        private static int GetAssignedCountInLane(TeamId team, BotLane lane)
        {
            var assignments = GetTeamAssignments(team);
            return assignments.Values.Count(l => l == lane);
        }

        /// <summary>
        /// Gets the lane based on a champion's position (for informational purposes only)
        /// </summary>
        public static BotLane GetLaneFromPosition(Vector2 position)
        {
            // Simple lane detection based on map position
            // Top lane: higher Y value (north on map)
            // Bottom lane: lower Y value (south on map)
            // Mid lane: middle Y value

            if (position.Y > 10000)
            {
                return BotLane.Top;
            }
            else if (position.Y < 4000)
            {
                return BotLane.Bottom;
            }
            else
            {
                return BotLane.Mid;
            }
        }

        /// <summary>
        /// Gets the optimal lane for a bot based on team composition
        /// Standard composition: 2 top, 1 mid, 2 bottom
        /// </summary>
        /// <param name="bot">The bot champion</param>
        /// <returns>The lane the bot should go to</returns>
        public static BotLane GetOptimalLane(Champion bot)
        {
            if (bot == null)
                return BotLane.Mid;

            TeamId team = bot.Team;

            // Get current lane assignments (not positions - assignments!)
            int topCount = GetAssignedCountInLane(team, BotLane.Top);
            int midCount = GetAssignedCountInLane(team, BotLane.Mid);
            int bottomCount = GetAssignedCountInLane(team, BotLane.Bottom);

            // Standard composition: 2 top, 1 mid, 2 bottom
            // Find the most unbalanced lane to go to
            // Prioritize lanes that are under the target count

            // Check if mid lane needs a champion (target: 1)
            if (midCount < 1)
            {
                return BotLane.Mid;
            }

            // Check if top lane needs a champion (target: 2)
            if (topCount < 2)
            {
                return BotLane.Top;
            }

            // Check if bottom lane needs a champion (target: 2)
            if (bottomCount < 2)
            {
                return BotLane.Bottom;
            }

            // If all lanes are at or above target, find the lane with the most space
            // (prefer lanes with fewer champions)
            if (topCount <= bottomCount)
            {
                return BotLane.Top;
            }
            else
            {
                return BotLane.Bottom;
            }
        }

        /// <summary>
        /// Checks if a bot should recall due to lane overflow
        /// Returns true if the bot's current lane has more champions than the target
        /// </summary>
        /// <param name="bot">The bot champion</param>
        /// <returns>True if the bot should recall</returns>
        public static bool ShouldRecallDueToOverflow(Champion bot)
        {
            if (bot == null || bot.IsDead)
                return false;

            // Only check overflow before minions spawn
            if (AreMinionsSpawned())
                return false;

            TeamId team = bot.Team;
            var assignedLane = GetAssignedLane(bot);

            // If bot hasn't been assigned to a lane yet, no overflow
            if (assignedLane == BotLane.None)
                return false;

            // Get current lane assignments
            int topCount = GetAssignedCountInLane(team, BotLane.Top);
            int midCount = GetAssignedCountInLane(team, BotLane.Mid);
            int bottomCount = GetAssignedCountInLane(team, BotLane.Bottom);

            // Target lane counts
            int targetTop = 2;
            int targetMid = 1;
            int targetBottom = 2;

            // Check if assigned lane is over target
            switch (assignedLane)
            {
                case BotLane.Top:
                    return topCount > targetTop;
                case BotLane.Mid:
                    return midCount > targetMid;
                case BotLane.Bottom:
                    return bottomCount > targetBottom;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the lane that needs a champion most (most unbalanced)
        /// </summary>
        /// <param name="team">The team to check</param>
        /// <returns>The lane that needs a champion</returns>
        public static BotLane GetLaneNeedingChampion(TeamId team)
        {
            int topCount = GetAssignedCountInLane(team, BotLane.Top);
            int midCount = GetAssignedCountInLane(team, BotLane.Mid);
            int bottomCount = GetAssignedCountInLane(team, BotLane.Bottom);

            // Target: 2 top, 1 mid, 2 bottom
            // Check which lane is furthest from its target
            int topDeficit = 2 - topCount;
            int midDeficit = 1 - midCount;
            int bottomDeficit = 2 - bottomCount;

            // Find the lane with the largest deficit (most needs a champion)
            if (topDeficit >= midDeficit && topDeficit >= bottomDeficit && topDeficit > 0)
            {
                return BotLane.Top;
            }
            else if (midDeficit >= topDeficit && midDeficit >= bottomDeficit && midDeficit > 0)
            {
                return BotLane.Mid;
            }
            else if (bottomDeficit > 0)
            {
                return BotLane.Bottom;
            }

            // If all lanes are at or above target, return the lane with fewest champions
            if (topCount <= midCount && topCount <= bottomCount)
            {
                return BotLane.Top;
            }
            else if (midCount <= topCount && midCount <= bottomCount)
            {
                return BotLane.Mid;
            }
            else
            {
                return BotLane.Bottom;
            }
        }

        /// <summary>
        /// Gets the target position for a specific lane
        /// </summary>
        /// <param name="lane">The lane to get position for</param>
        /// <param name="team">The team (for determining spawn side)</param>
        /// <returns>The target position for the lane</returns>
        public static Vector2 GetLanePosition(BotLane lane, TeamId team)
        {
            // Standard Summoner's Rift lane positions
            // Blue team spawns bottom-left, Purple team spawns top-right

            if (team == TeamId.TEAM_BLUE)
            {
                return lane switch
                {
                    BotLane.Top => new Vector2(2000f, 12000f),
                    BotLane.Mid => new Vector2(5000f, 7500f),
                    BotLane.Bottom => new Vector2(12000f, 2000f),
                    _ => new Vector2(5000f, 7500f)
                };
            }
            else
            {
                return lane switch
                {
                    BotLane.Top => new Vector2(12000f, 2000f),
                    BotLane.Mid => new Vector2(10000f, 7500f),
                    BotLane.Bottom => new Vector2(2000f, 12000f),
                    _ => new Vector2(10000f, 7500f)
                };
            }
        }

        /// <summary>
        /// Checks if a bot is currently in the process of recalling
        /// </summary>
        /// <param name="botNetId">The bot's network ID</param>
        /// <returns>True if the bot is recalling</returns>
        public static bool IsBotRecalling(uint botNetId)
        {
            return _botsCurrentlyRecalling.Contains(botNetId);
        }

        /// <summary>
        /// Marks a bot as currently recalling
        /// </summary>
        /// <param name="bot">The bot champion</param>
        public static void MarkBotAsRecalling(Champion bot)
        {
            if (bot != null)
            {
                _botsCurrentlyRecalling.Add(bot.NetId);
            }
        }

        /// <summary>
        /// Clears the recalling status for a bot
        /// </summary>
        /// <param name="bot">The bot champion</param>
        public static void ClearBotRecallingStatus(Champion bot)
        {
            if (bot != null)
            {
                _botsCurrentlyRecalling.Remove(bot.NetId);
            }
        }

        /// <summary>
        /// Checks if any ally is currently recalling
        /// </summary>
        /// <param name="bot">The bot to check against</param>
        /// <returns>True if an ally (not self) is recalling</returns>
        public static bool IsAllyRecalling(Champion bot)
        {
            if (bot == null)
                return false;

            var allChampions = GetAllChampions();
            foreach (var champion in allChampions)
            {
                if (champion.NetId != bot.NetId && champion.Team == bot.Team)
                {
                    if (_botsCurrentlyRecalling.Contains(champion.NetId))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the total number of champions on a team (including players)
        /// </summary>
        /// <param name="team">The team to check</param>
        /// <returns>Total number of champions on the team</returns>
        public static int GetTeamChampionCount(TeamId team)
        {
            var allChampions = GetAllChampions();
            return allChampions.Count(c => c.Team == team && !c.IsDead);
        }

        /// <summary>
        /// Checks if the team has more than 5 champions (standard team size)
        /// If so, lane overflow enforcement is disabled
        /// </summary>
        /// <param name="team">The team to check</param>
        /// <returns>True if team has more than 5 champions</returns>
        public static bool HasExcessChampions(TeamId team)
        {
            return GetTeamChampionCount(team) > 5;
        }

        /// <summary>
        /// Gets the target lane for a bot based on current team composition
        /// </summary>
        /// <param name="bot">The bot champion</param>
        /// <returns>The lane the bot should go to</returns>
        public static BotLane GetTargetLane(Champion bot)
        {
            if (bot == null)
                return BotLane.Mid;

            TeamId team = bot.Team;

            // If team has more than 5 champions, don't enforce lane limits
            if (HasExcessChampions(team))
            {
                // Still prefer mid lane if available, otherwise go to lane with fewest champions
                return GetOptimalLane(bot);
            }

            // Standard lane selection
            return GetOptimalLane(bot);
        }

        /// <summary>
        /// Gets the position the bot should move to based on its lane
        /// </summary>
        /// <param name="bot">The bot champion</param>
        /// <returns>The target position for the bot</returns>
        public static Vector2 GetTargetPosition(Champion bot)
        {
            if (bot == null)
                return new Vector2(5000f, 7500f);

            var targetLane = GetTargetLane(bot);
            return GetLanePosition(targetLane, bot.Team);
        }

        /// <summary>
        /// Clears all recalling statuses (useful for game reset)
        /// </summary>
        public static void ClearAllRecallingStatuses()
        {
            _botsCurrentlyRecalling.Clear();
        }
    }
}
