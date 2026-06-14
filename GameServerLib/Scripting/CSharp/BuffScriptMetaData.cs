using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.Scripting.CSharp
{
    public class BuffScriptMetaData
    {
        public BuffType BuffType { get; set; } = BuffType.INTERNAL;
        public BuffAddType BuffAddType { get; set; } = BuffAddType.RENEW_EXISTING;
        public int MaxStacks { get; set; } = 1;
        public bool IsHidden { get; set; } = false;
        public bool UpdateInfinite { get; set; } = false;
        
        public bool IsNonDispellable { get; set; } = false; //this is important for things like cleanse or self cc's that should not be removed
        
        // Riot default is false (LuaScriptBaseBuff::mPersistsThroughDeath(false)): a buff is
        // removed when its holder dies UNLESS it opts in. Buffs that must survive death — revives
        // (Guardian Angel, Morde COTG) and persist-across-respawn passives (Caitlyn headshot,
        // Wukong/Shyvana passives, mushroom managers) — must set this to true explicitly.
        public bool PersistsThroughDeath { get; set; } = false;
        
        public bool PermeatesThroughDeath { get; set; } = false;

        public bool IsDeathRecapSource { get; set; } = false;

        public int OnPreDamagePriority { get; set; } = 0;

        public bool DoOnPreDamageInExpirationOrder { get; set; } = false;
    }
}
