using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.Scripting.CSharp
{
    public class BuffScriptMetaData
    {
        public BuffType BuffType { get; set; } = BuffType.INTERNAL;
        public BuffAddType BuffAddType { get; set; } = BuffAddType.RENEW_EXISTING;
        public int MaxStacks { get; set; } = 1;
        public bool IsHidden { get; set; } = false;
        
        // TODO: remove this, this is not riot accurate
        public bool UpdateInfinite { get; set; } = false;
        
        public bool IsNonDispellable { get; set; } = false; // This is important for things like cleanse or self cc's that should not be removed
        
        public bool PersistsThroughDeath { get; set; } = true;
        
        public bool PermeatesThroughDeath { get; set; } = false;

        public bool IsDeathRecapSource { get; set; } = false;

        public int OnPreDamagePriority { get; set; } = 0;

        public bool DoOnPreDamageInExpirationOrder { get; set; } = false;
    }
}
