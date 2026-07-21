using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.Scripting.CSharp
{
    public class BuffScriptMetaData
    {
        public BuffType BuffType { get; set; } = BuffType.INTERNAL;
        public BuffAddType BuffAddType { get; set; } = BuffAddType.RENEW_EXISTING;
        public int MaxStacks { get; set; } = 1;
        /// <summary>
        /// Wire IsHidden flag: the client hides the HUD icon but still processes the buff fully
        /// (4.17 decomp: BuffManager SetIsHidden is display-only; the OnBuffActivate/OnBuffCast
        /// audio events fire regardless). Hidden buffs ARE replicated — this is NOT a
        /// "server-internal" switch.
        /// </summary>
        public bool IsHidden { get; set; } = false;
        
        public bool IsNonDispellable { get; set; } = false; //this is important for things like cleanse or self cc's that should not be removed
        
        // Riot default is false (LuaScriptBaseBuff::mPersistsThroughDeath(false)): a buff is
        // removed when its holder dies UNLESS it opts in. Buffs that must survive death — revives
        // (Guardian Angel, Morde COTG) and persist-across-respawn passives (Caitlyn headshot,
        // Wukong/Shyvana passives, mushroom managers) — must set this to true explicitly.
        public bool PersistsThroughDeath { get; set; } = false;

        public bool IsDeathRecapSource { get; set; } = false;

        // Riot BuffData.mDeathRecapPriority (config key [BuffData] DeathRecapPriority, BuffData.cpp:26):
        // int, DEFAULT 4 (a 0/absent value falls back to 4). Orders which sources a death recap credits
        // when several contributed — lower/higher priority decides display precedence. Config-driven in
        // Riot (not a Lua/BB API); modelled here so a buff can carry its priority to the death-recap layer.
        public int DeathRecapPriority { get; set; } = 4;

        public int OnPreDamagePriority { get; set; } = 0;

        public bool DoOnPreDamageInExpirationOrder { get; set; } = false;
    }
}
