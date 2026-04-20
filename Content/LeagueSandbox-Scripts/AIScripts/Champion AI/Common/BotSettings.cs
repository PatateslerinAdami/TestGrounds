using System;

namespace AIScripts.Common
{
    /// <summary>
    /// Configuration settings for bot behavior including chat, trash talk, and ping settings.
    /// This allows fine-grained control over bot communication behaviors.
    /// </summary>
    public class BotSettings
    {
        // Chat settings
        public bool EnableChat { get; set; }
        public bool EnableTrashTalk { get; set; }
        public bool EnableSeriousTalk { get; set; }

        // Ping settings
        public bool EnablePings { get; set; }
        public bool EnableToxicPings { get; set; }
        public bool EnableSeriousPings { get; set; }

        // Ping cooldowns (in seconds)
        public float PingCooldown { get; set; } = 5.0f;
        public float ToxicPingCooldown { get; set; } = 10.0f;

        // Toxic ping settings
        public float ToxicPingChance { get; set; } = 0.40f; // 5% chance
        public int MaxToxicPingSpam { get; set; } = 5; // Maximum number of missing pings to spam
        public float ToxicPingSpamInterval { get; set; } = 1.0f; // Interval between spam pings

        // Serious ping trigger distances
        public float AllyNearbyDistance { get; set; } = 1500f; // Distance to consider ally "nearby" for serious pings
        public float EnemyNearbyDistance { get; set; } = 1200f; // Distance to check for enemies

        // Chat cooldowns
        public float TrashTalkCooldownMin { get; set; } = 25f;
        public float TrashTalkCooldownMax { get; set; } = 45f;
        public float ReactionDelayMin { get; set; } = 3f;
        public float ReactionDelayMax { get; set; } = 8f;

        /// <summary>
        /// Creates a default bot settings instance with all features enabled.
        /// </summary>
        public static BotSettings Default => new BotSettings
        {
            EnableChat = true,
            EnableTrashTalk = true,
            EnableSeriousTalk = true,
            EnablePings = true,
            EnableToxicPings = true,
            EnableSeriousPings = true
        };

        /// <summary>
        /// Creates a bot settings instance with only serious behaviors enabled.
        /// </summary>
        public static BotSettings SeriousOnly => new BotSettings
        {
            EnableChat = true,
            EnableTrashTalk = false,
            EnableSeriousTalk = true,
            EnablePings = true,
            EnableToxicPings = false,
            EnableSeriousPings = true
        };

        /// <summary>
        /// Creates a bot settings instance with only toxic behaviors enabled.
        /// </summary>
        public static BotSettings ToxicOnly => new BotSettings
        {
            EnableChat = true,
            EnableTrashTalk = true,
            EnableSeriousTalk = false,
            EnablePings = true,
            EnableToxicPings = true,
            EnableSeriousPings = false
        };

        /// <summary>
        /// Creates a bot settings instance with all communication disabled.
        /// </summary>
        public static BotSettings Silent => new BotSettings
        {
            EnableChat = false,
            EnableTrashTalk = false,
            EnableSeriousTalk = false,
            EnablePings = false,
            EnableToxicPings = false,
            EnableSeriousPings = false
        };
    }
}
