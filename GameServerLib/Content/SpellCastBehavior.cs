using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.Content
{
    public sealed class SpellCastBehavior
    {
        public bool InstantCast { get; }
        public bool BelongsToAvatar { get; }
        public bool DoesntBreakChannels { get; }
        public bool IsToggleSpell { get; }
        public bool? SubjectToGlobalCooldown { get; }
        public bool? TriggersGlobalCooldown { get; }

        public SpellCastBehavior(SpellData data, ContentFile? file = null)
        {
            InstantCast = data.Flags.HasFlag(SpellDataFlags.InstantCast);
            BelongsToAvatar = data.BelongsToAvatar;
            DoesntBreakChannels = data.DoesntBreakChannels;
            IsToggleSpell = data.IsToggleSpell;
            SubjectToGlobalCooldown = file?.GetObject("SpellData", "SubjectToGlobalCooldown") != null
                ? data.SubjectToGlobalCooldown
                : null;
            TriggersGlobalCooldown = file?.GetObject("SpellData", "TriggersGlobalCooldown") != null
                ? data.TriggersGlobalCooldown
                : null;
        }
    }
}
