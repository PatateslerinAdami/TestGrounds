using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Packets.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Logging;
using log4net;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;

namespace AIScripts.Common
{
    /// <summary>
    /// Shared API for bot communication functionality including chat and pings.
    /// This provides a centralized way for all bots to communicate with players.
    /// </summary>
    public static class BotCommunicationAPI
    {
        private static ILog _logger = LoggerProvider.GetLogger();

        #region Toxic Ping Spam State

        /// <summary>
        /// Tracks the state of a toxic ping spam sequence for a single bot.
        /// </summary>
        public class ToxicPingSpamState
        {
            /// <summary>
            /// The bot that is spamming pings.
            /// </summary>
            public Champion Bot { get; set; }

            /// <summary>
            /// The position where pings are being spammed (e.g., dead ally's position).
            /// </summary>
            public Vector2 TargetPosition { get; set; }

            /// <summary>
            /// Number of pings remaining to send in this spam sequence.
            /// </summary>
            public int PingsRemaining { get; set; }

            /// <summary>
            /// Time when the next ping should be sent.
            /// </summary>
            public float NextPingTime { get; set; }

            /// <summary>
            /// Whether a spam sequence is currently active.
            /// </summary>
            public bool IsActive => PingsRemaining > 0;

            /// <summary>
            /// Initializes a new spam sequence.
            /// </summary>
            public void StartSpam(Champion bot, Vector2 position, int pingCount, float currentTime, float interval)
            {
                Bot = bot;
                TargetPosition = position;
                PingsRemaining = pingCount;
                NextPingTime = currentTime;
            }

            /// <summary>
            /// Processes the spam sequence. Call this in the bot's update loop.
            /// Returns true if a ping was sent this frame.
            /// </summary>
            public bool Update(float currentTime, float interval)
            {
                if (!IsActive || currentTime < NextPingTime)
                    return false;

                // Send the ping with slight position variation
                Vector2 offsetPosition = TargetPosition + new Vector2(
                    (float)(new Random().NextDouble() * 100 - 50),
                    (float)(new Random().NextDouble() * 100 - 50)
                );

                NotifyMapPing(offsetPosition, (GameServerCore.Enums.PingCategory)Pings.PING_MISSING, Bot.NetId, Bot.Team);

                PingsRemaining--;
                if (IsActive)
                {
                    NextPingTime = currentTime + interval;
                }

                return true;
            }

            /// <summary>
            /// Clears the spam state.
            /// </summary>
            public void Clear()
            {
                PingsRemaining = 0;
                Bot = null;
            }
        }

        #endregion

        #region Trash Talk Data

        private static readonly string[] _generalTaunts =
        {
            "report my team",
            "gg ez",
            "this team is holding me back",
            "hardstuck players smh",
            "i cant carry any harder",
            "anyone else on my team even trying?",
            "ff 15 my team is inting",
            "i am literally 1v9 right now",
            "where is my team lol",
            "this is why i have trust issues",
            "uninstall please",
            "my goldfish could play better",
            "i need better teammates",
            "actual bots on my team (no offense to me)",
            "alt f4 for a free skin"
        };

        private static readonly string[] _dyingTaunts =
        {
            "LAG",
            "my mouse slipped",
            "i wasnt even trying",
            "this keyboard is trash",
            "didnt want that kill anyway",
            "that was definitely a misplay on my part... jk report jungle",
            "nice hack",
            "reported",
            "my cat walked on my keyboard",
            "whatever i have 400 ping",
            "open mid",
            "OMG REPORT THIS TROLL I SWEAR ON MY MOMS LIFE I WILL FIND YOU I HAVE YOUR IP I AM A HACKER IN REAL LIFE I WORK FOR THE PENTAGON YOU ARE FINISHED DOG.",
            "My team is doing 0 dmg report them all",
            "Better nerf irelia",
            "My team is feeders",
            "You are playing a braindead champion, of course you're winning",
            "Omg this team 0 vision wtf",
            "WHAT IS THIS BULLSHIT",
            "ARE YOU KIDDING ME?",
            "Ooh I got rekt yeah yeah. Go tell your mom",
            "Wtf that was a bug, it doesn't count",
            "Just end this"
        };

        private static readonly string[] _killingTaunts =
        {
            "too easy",
            "did you even go to practice tool?",
            "LOL",
            "get rekt",
            "skill diff",
            "come back when you hit gold",
            "you should switch to a simpler game",
            "EZ Clap",
            "i was shopping that fight btw",
            "delete this game bro",
            "go play animal crossing",
            "thank you for the gold very generous",
            "you are finished dog",
            "your father left because of your positioning",
            "stick to minecraft",
            "try turning on your monitor",
            "nice flash",
            "ez",
            "I've seen better mechanics in a 2008 Honda Civic.",
            "thanks for the free lp",
            "That was a nice attempt. Emphasis on attempt.",
            "I see you're playing the new tutorial difficulty",
            "You're doing great! ...at feeding",
            "Your mechanics are sponsored by PowerPoint",
            "You mad cuz bad",
            "Git gud",
            "Do you have are stupid",
            "RIP bozo",
            "Get pwned n00b",
            "This is the worst team I've ever had in my entire life",
            "You are so bad it's not even funny LMAO",
            "Boosted bonobo",
            "I'm not like the rest of you. I'm stronger, I'm smarter, I'm better",
            "Survival instinct of an orange cat",
            "Sell your items, it gives you more damage btw",
            "Do you even know how to move?",
            "lmao skill issue",
            "Your positioning is avant-garde",
            "your skills are trash",
            "Impressive skill issue",
            "I respect the confidence more than the execution"
        };

        private static readonly string[] _lowHealthTaunts =
        {
            "YOLO",
            "this is fine",
            "i play better when im tilted",
            "dont worry i meant to do that",
            "low health = more damage everyone knows this",
        };

        private static readonly string[] _generalGameTaunts =
        {
            "mid diff",
            "jungle diff",
            "support diff",
            "this is a skill based game and it shows",
            "i peaked plat and it wasnt even hard",
            "diamond is literally elo hell",
            "bro think he's faker",
            "just ward bro its not that hard",
            "how are you level 4 right now",
            "you built that?",
        };

        #endregion

        #region Serious Talk Data (Placeholder for future expansion)

        private static readonly string[] _seriousStrategyCalls =
        {
            "group mid",
            "dragon soon",
            "baron in 30",
            "ward river",
            "enemy jungle spotted",
            "play safe",
            "push top",
            "defend base"
        };

        private static readonly string[] _seriousKillAcknowledgments =
        {
            "nice",
            "good kill",
            "well played",
            "clean"
        };

        private static readonly string[] _seriousDeathAcknowledgments =
        {
            "my bad",
            "sorry",
            "should have backed",
            "got greedy"
        };

        #endregion

        #region Chat Methods

        /// <summary>
        /// Sends a trash talk message from the bot.
        /// </summary>
        /// <param name="bot">The champion sending the message</param>
        /// <param name="settings">Bot settings to check if enabled</param>
        /// <param name="pool">Optional specific message pool to use</param>
        /// <param name="allChat">Whether to send to all chat or team only</param>
        public static void SendTrashTalk(Champion bot, BotSettings settings, string[] pool = null, bool allChat = true)
        {
            if (!settings.EnableChat || !settings.EnableTrashTalk || bot == null)
                return;

            if (pool == null)
                pool = _generalTaunts;

            string message = pool[new Random().Next(pool.Length)];
            SendChatMessage(bot, message, allChat);
        }

        /// <summary>
        /// Sends a serious strategic message from the bot.
        /// </summary>
        /// <param name="bot">The champion sending the message</param>
        /// <param name="settings">Bot settings to check if enabled</param>
        /// <param name="context">Context for the message (e.g., "kill", "death", "strategy")</param>
        /// <param name="allChat">Whether to send to all chat or team only</param>
        public static void SendSeriousTalk(Champion bot, BotSettings settings, string context = "strategy", bool allChat = false)
        {
            if (!settings.EnableChat || !settings.EnableSeriousTalk || bot == null)
                return;

            string[] pool;
            switch (context.ToLower())
            {
                case "kill":
                    pool = _seriousKillAcknowledgments;
                    break;
                case "death":
                    pool = _seriousDeathAcknowledgments;
                    break;
                default:
                    pool = _seriousStrategyCalls;
                    break;
            }

            string message = pool[new Random().Next(pool.Length)];
            SendChatMessage(bot, message, allChat);
        }

        /// <summary>
        /// Sends a contextual trash talk message based on game state.
        /// </summary>
        public static void SendContextualTrashTalk(Champion bot, BotSettings settings, float healthPercentage, int killCount, int deathCount)
        {
            if (!settings.EnableChat || !settings.EnableTrashTalk || bot == null)
                return;

            string[] pool;

            if (healthPercentage < 0.2f)
                pool = _lowHealthTaunts;
            else if (killCount >= 2)
                pool = _killingTaunts;
            else if (deathCount >= 2)
                pool = _dyingTaunts;
            else
                pool = new Random().NextDouble() < 0.5 ? _generalTaunts : _generalGameTaunts;

            string message = pool[new Random().Next(pool.Length)];
            SendChatMessage(bot, message, true);
        }

        /// <summary>
        /// Sends a kill reaction message (trash talk).
        /// </summary>
        public static void SendKillTrashTalk(Champion bot, BotSettings settings)
        {
            if (!settings.EnableChat || !settings.EnableTrashTalk || bot == null)
                return;

            string message = _killingTaunts[new Random().Next(_killingTaunts.Length)];
            SendChatMessage(bot, message, true);
        }

        /// <summary>
        /// Sends a death reaction message (trash talk).
        /// </summary>
        public static void SendDeathTrashTalk(Champion bot, BotSettings settings)
        {
            if (!settings.EnableChat || !settings.EnableTrashTalk || bot == null)
                return;

            string message = _dyingTaunts[new Random().Next(_dyingTaunts.Length)];
            SendChatMessage(bot, message, true);
        }

        /// <summary>
        /// Internal method to send a chat message using the API.
        /// </summary>
        private static void SendChatMessage(Champion bot, string message, bool allChat)
        {
            if (bot == null) return;

            ApiFunctionManager.PrintPlayerChat(
                bot.Name,
                bot.Model,
                bot.Team,
                message,
                allChat
            );

            _logger.Debug($"[BotChat] {bot.Name} ({bot.Model}): {message}");
        }

        #endregion

        #region Ping Methods

        /// <summary>
        /// Sends a serious ping (Attack, OnMyWay, RequestHelp) at the specified position.
        /// Used for coordinating with allies during engagements.
        /// </summary>
        /// <param name="bot">The champion sending the ping</param>
        /// <param name="settings">Bot settings to check if enabled</param>
        /// <param name="position">Position to ping</param>
        /// <param name="pingType">Type of serious ping</param>
        public static void SendSeriousPing(Champion bot, BotSettings settings, Vector2 position, Pings pingType)
        {
            if (!settings.EnablePings || !settings.EnableSeriousPings || bot == null)
                return;

            NotifyMapPing(position, (GameServerCore.Enums.PingCategory)pingType, bot.NetId, bot.Team);
            _logger.Debug($"[BotPing] {bot.Name} sent serious ping {pingType} at {position}");
        }

        /// <summary>
        /// Sends an attack ping at the enemy position.
        /// Call this when engaging enemies with allies nearby.
        /// </summary>
        public static void SendAttackPing(Champion bot, BotSettings settings, Vector2 enemyPosition)
        {
            SendSeriousPing(bot, settings, enemyPosition, Pings.PING_ATTACK);
        }

        /// <summary>
        /// Sends an "On My Way" ping.
        /// Call this when moving to assist allies.
        /// </summary>
        public static void SendOnMyWayPing(Champion bot, BotSettings settings, Vector2 targetPosition)
        {
            SendSeriousPing(bot, settings, targetPosition, Pings.PING_ON_MY_WAY);
        }

        /// <summary>
        /// Sends a request assistance ping.
        /// Call this when needing help from allies.
        /// </summary>
        public static void SendRequestHelpPing(Champion bot, BotSettings settings, Vector2 position)
        {
            SendSeriousPing(bot, settings, position, Pings.PING_ASSIST);
        }

        /// <summary>
        /// Sends a single toxic ping (Missing ping) at the specified position.
        /// </summary>
        public static void SendToxicPing(Champion bot, BotSettings settings, Vector2 position)
        {
            if (!settings.EnablePings || !settings.EnableToxicPings || bot == null)
                return;

            NotifyMapPing(position, (GameServerCore.Enums.PingCategory)Pings.PING_MISSING, bot.NetId, bot.Team);
            _logger.Debug($"[BotPing] {bot.Name} sent toxic ping at {position}");
        }

        /// <summary>
        /// Starts a toxic ping spam sequence at a position.
        /// Used when an ally dies to spam ping their corpse.
        /// Call this when initiating a new spam sequence, then call ProcessToxicPingSpam in your update loop.
        /// </summary>
        /// <param name="bot">The champion sending the pings</param>
        /// <param name="settings">Bot settings to check if enabled</param>
        /// <param name="position">Position to spam ping</param>
        /// <param name="currentTime">Current game time</param>
        /// <param name="lastPingTime">Reference to the last ping time (will be updated when spam completes)</param>
        /// <param name="spamState">The spam state object to track this sequence</param>
        public static void SpamToxicPings(Champion bot, BotSettings settings, Vector2 position, float currentTime, ref float lastPingTime, ToxicPingSpamState spamState)
        {
            if (!settings.EnablePings || !settings.EnableToxicPings || bot == null)
                return;

            // Check cooldown
            if (currentTime - lastPingTime < settings.ToxicPingCooldown)
                return;

            // Check chance
            if (new Random().NextDouble() > settings.ToxicPingChance)
                return;

            // Start the spam sequence with a random amount between 1 and MaxToxicPingSpam
            int spamCount = new Random().Next(1, settings.MaxToxicPingSpam + 1);
            spamState.StartSpam(bot, position, spamCount, currentTime, settings.ToxicPingSpamInterval);
            _logger.Debug($"[BotPing] {bot.Name} started toxic ping spam sequence ({spamCount} pings at {settings.ToxicPingSpamInterval}s intervals) at {position}");
        }

        /// <summary>
        /// Processes an active toxic ping spam sequence. Call this in your bot's update loop.
        /// Returns true if the spam sequence completed this frame.
        /// </summary>
        /// <param name="settings">Bot settings</param>
        /// <param name="currentTime">Current game time</param>
        /// <param name="lastPingTime">Reference to the last ping time (will be updated when spam completes)</param>
        /// <param name="spamState">The spam state object tracking this sequence</param>
        public static bool ProcessToxicPingSpam(BotSettings settings, float currentTime, ref float lastPingTime, ToxicPingSpamState spamState)
        {
            if (!settings.EnablePings || !settings.EnableToxicPings || !spamState.IsActive)
                return false;

            // Process the spam - sends one ping per interval
            spamState.Update(currentTime, settings.ToxicPingSpamInterval);

            // Check if spam just completed
            if (!spamState.IsActive)
            {
                lastPingTime = currentTime;
                _logger.Debug($"[BotPing] {spamState.Bot?.Name} completed toxic ping spam sequence");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks for dead allies in global range and starts toxic ping spam sequences if appropriate.
        /// Call this in the bot's OnUpdate method. Also call ProcessToxicPingSpam in your update loop
        /// to handle the actual ping timing with intervals.
        /// </summary>
        /// <param name="bot">The champion checking for dead allies</param>
        /// <param name="settings">Bot settings</param>
        /// <param name="currentTime">Current game time</param>
        /// <param name="lastToxicPingTime">Reference to last toxic ping time</param>
        /// <param name="trackedDeadAllies">HashSet of NetIds of allies already pinged</param>
        /// <param name="spamState">The spam state object to track active spam sequences</param>
        public static void CheckForDeadAlliesAndToxicPing(
            Champion bot,
            BotSettings settings,
            float currentTime,
            ref float lastToxicPingTime,
            HashSet<uint> trackedDeadAllies,
            ToxicPingSpamState spamState)
        {
            if (!settings.EnablePings || !settings.EnableToxicPings || bot == null)
                return;

            // Don't start new spam sequences while one is already active
            if (spamState.IsActive)
                return;

            // Check cooldown for starting new spam sequences
            if (currentTime - lastToxicPingTime < settings.ToxicPingCooldown)
                return;

            // Get all champions in the game
            var allChampions = ApiFunctionManager.GetAllChampions();

            foreach (var champion in allChampions)
            {
                // Skip self and enemies
                if (champion.NetId == bot.NetId || champion.Team != bot.Team)
                    continue;

                // Check if ally is dead and we haven't pinged them yet
                if (champion.IsDead && !trackedDeadAllies.Contains(champion.NetId))
                {
                    // Check chance
                    if (new Random().NextDouble() <= settings.ToxicPingChance)
                    {
                        // Start a spam sequence at dead ally's position
                        SpamToxicPings(bot, settings, champion.Position, currentTime, ref lastToxicPingTime, spamState);
                    }

                    // Mark as tracked so we don't ping them multiple times for the same death
                    trackedDeadAllies.Add(champion.NetId);
                }
                else if (!champion.IsDead && trackedDeadAllies.Contains(champion.NetId))
                {
                    // Ally respawned, remove from tracked
                    trackedDeadAllies.Remove(champion.NetId);
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets a random message from the specified pool.
        /// </summary>
        public static string GetRandomMessage(string[] pool)
        {
            if (pool == null || pool.Length == 0)
                return "";
            return pool[new Random().Next(pool.Length)];
        }

        /// <summary>
        /// Checks if there are any allies nearby the bot.
        /// </summary>
        public static bool HasNearbyAllies(Champion bot, float range)
        {
            if (bot == null) return false;

            var allChampions = ApiFunctionManager.GetAllChampions();
            return allChampions.Any(c =>
                c.NetId != bot.NetId &&
                c.Team == bot.Team &&
                !c.IsDead &&
                Vector2.Distance(bot.Position, c.Position) <= range);
        }

        /// <summary>
        /// Gets the nearest ally champion within range.
        /// </summary>
        public static Champion GetNearestAlly(Champion bot, float range)
        {
            if (bot == null) return null;

            var allChampions = ApiFunctionManager.GetAllChampions();
            return allChampions
                .Where(c =>
                    c.NetId != bot.NetId &&
                    c.Team == bot.Team &&
                    !c.IsDead &&
                    Vector2.Distance(bot.Position, c.Position) <= range)
                .OrderBy(c => Vector2.Distance(bot.Position, c.Position))
                .FirstOrDefault();
        }

        #endregion
    }
}
