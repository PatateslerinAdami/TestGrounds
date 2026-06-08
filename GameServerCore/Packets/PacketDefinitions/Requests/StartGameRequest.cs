using GameServerCore.Enums;

namespace GameServerCore.Packets.PacketDefinitions.Requests
{
    /// <summary>
    /// Maps Riot's PKT_C2S_ClientReady (82): the client's ready-signal at game start.
    /// Carries the client's Tips::Config (verified vs S4 mac decomp,
    /// UX/Tips/Tips.h). TipID/ColorID/DurationID are NOT enums - they are int8 indices
    /// into the client's startup-tip config data; Riot encodes errors by negating an ID
    /// (Tips::Config::SetAsError, so negative = error-marked). Only Flags is a real
    /// bitfield, see <see cref="TipFlags"/>. (The wire packet also carries
    /// detectedHackModule hash/size/timestamp anti-cheat fields we do not surface.)
    /// </summary>
    public class StartGameRequest : ICoreRequest
    {
        /// <summary>Index of the startup tip in the client's tip data (negative = error-marked).</summary>
        public sbyte TipID { get; }
        /// <summary>Index of the tip color in the client's tip data (negative = error-marked).</summary>
        public sbyte ColorID { get; }
        /// <summary>Index of the tip duration in the client's tip data (negative = error-marked).</summary>
        public sbyte DurationID { get; }
        public TipFlags Flags { get; }

        public StartGameRequest(sbyte tipId, sbyte colorId, sbyte durationId, sbyte flags)
        {
            TipID = tipId;
            ColorID = colorId;
            DurationID = durationId;
            Flags = (TipFlags)(byte)flags;
        }
    }
}
