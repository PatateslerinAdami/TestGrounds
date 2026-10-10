using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.Scripting.CSharp
{
    public class BuffScriptMetaData
    {
        public BuffType BuffType { get; set; } = BuffType.INTERNAL;
        public BuffAddType BuffAddType { get; set; } = BuffAddType.RENEW_EXISTING;
        public int MaxStacks { get; set; } = 1;
        public bool IsHidden { get; set; } = false;
        /// <summary>Send a hidden client buff, rather than keeping it server only.</summary>
        public bool SendHiddenToClient { get; set; } = false;
        public bool UpdateInfinite { get; set; } = false;
        /// <summary>Native client resource package, zero selects global item buffs. Null keeps the unit package.</summary>
        public uint? ClientPackageHash { get; set; }
    }
}
