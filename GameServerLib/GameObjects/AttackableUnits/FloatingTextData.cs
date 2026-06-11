using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;

namespace GameServerLib.GameObjects.AttackableUnits
{
    /// <summary>
    /// Data for a server-driven floating text (DisplayFloatingText packet, id 025).
    /// In 4.20 the FloatTextType selects a CLIENT-side animation/style profile
    /// (GamePermanent.cfg [FloatingText]); the server only picks the type and supplies operands and
    /// cannot change the animation per cast. Note: damage/dodge/miss/invuln float text is normally
    /// generated client-side from the damage packet, so this explicit packet is mainly for custom wording.
    /// </summary>
    public class FloatingTextData
    {
        /// <summary>Unit the text floats over (packet TargetNetID).</summary>
        public GameObject Target { get; }
        /// <summary>
        /// Animation/style profile + color variant. The Enemy* variants are normally chosen
        /// client-side (= the local viewer is the target taking damage); only set them here if you
        /// specifically need that styling.
        /// </summary>
        public FloatTextType FloatTextType { get; }
        /// <summary>Text to display (or a localization key).</summary>
        public string Message { get; }
        /// <summary>
        /// Packet "param1" (int32) = the number the client substitutes for the "@IntParam1@" token in
        /// the (localized) <see cref="Message"/>. Per the mac decomp, Tooltip::Replace translates the
        /// message then swaps "@IntParam1@" for param1 written as a decimal (it even supports an
        /// "@IntParam1*multiplier@" form). This is used by mode-specific float text — e.g. Ascension
        /// "game_Asc_points_text" = "+@IntParam1@ Points", or the Nexus-health modes
        /// "@IntParam1@ Your Nexus Health" — where Param is the points/health number. For messages with
        /// no "@IntParam1@" token (plain text like "Blocked!", and the standard SR/ARAM popups), Param
        /// is ignored — pass 0. It is NOT a NetID and NOT an enum.
        /// </summary>
        public int Param { get; }
        public FloatingTextData(GameObject target, string message, FloatTextType floatTextType, int param)
        {
            Target = target;
            Message = message;
            FloatTextType = floatTextType;
            Param = param;
        }
    }
}
