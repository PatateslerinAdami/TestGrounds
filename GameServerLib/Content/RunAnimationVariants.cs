using System;
using System.Collections.Generic;

namespace LeagueSandbox.GameServer.Content
{
    /// <summary>
    /// Per-champion speed-state RUN animation variants, extracted from the 4.20 client's
    /// base-skin .blnd animation slot names (strings in DATA/Characters/*/Skins/Base/*.blnd).
    /// Consumed by the run-animation watcher in ObjAIBase: Riot's server swaps the RUN slot
    /// via S2C_SetAnimStates depending on movement state this is replay-verified (630b7ceb):
    /// run_haste while a HASTE-type buff is active, run_fast while above base move speed
    /// (boots alone qualify), run_slow while slowed (champs without the variant keep
    /// run_fast/RUN). Form-prefixed variants (R_run_fast etc.) belong to the form-swap
    /// scripts, not this table. Client matches the names case-insensitively.
    /// GENERATED from the client install so regenerate rather than hand-edit.
    /// </summary>
    public static class RunAnimationVariants
    {
        public sealed class Variants
        {
            public string Haste;
            public string Fast;
            public string Slow;
            public string SlowBack;
            // Played while inside a brush (replay: sent at above-base MS values too, so it
            // outranks Fast). 4.20 inventory: exactly one champion (Khazix).
            public string Brush;
        }

        private static readonly Dictionary<string, Variants> _byModel = new Dictionary<string, Variants>(StringComparer.OrdinalIgnoreCase)
        {
            ["Aatrox"] = new Variants { Haste = "Run_Haste" },
            ["Azir"] = new Variants { Haste = "Run_Haste", Fast = "Run_FAST" },
            ["Braum"] = new Variants { Haste = "run_haste", Slow = "run_slow" },
            ["Diana"] = new Variants { Haste = "run_haste", Fast = "Run_fast", Slow = "Run_slow", SlowBack = "run_slow_back" },
            ["Garen"] = new Variants { Haste = "Run_HASTE", Fast = "Run_FAST" },
            ["Heimerdinger"] = new Variants { Haste = "Run_HASTE", Fast = "Run_FAST" },
            ["Jayce"] = new Variants { Fast = "Run_fast" },
            ["Jinx"] = new Variants { Haste = "run_haste", Fast = "Run_fast" },
            ["Kalista"] = new Variants { Haste = "Run_Haste" },
            ["Karthus"] = new Variants { Haste = "Run_HASTE" },
            ["Khazix"] = new Variants { Haste = "Run_haste", Fast = "Run_fast", Brush = "Run_in_brush" },
            ["Lissandra"] = new Variants { Haste = "Run_HASTE", Fast = "Run_FAST" },
            ["Lucian"] = new Variants { Haste = "Run_HASTE", Fast = "Run_FAST" },
            ["MasterYi"] = new Variants { Haste = "Run_HASTE", Fast = "Run_FAST" },
            ["Nasus"] = new Variants { Haste = "Run_HASTE", Fast = "Run_FAST" },
            ["Quinn"] = new Variants { Haste = "Run_Haste", Fast = "Run_fast" },
            ["Sion"] = new Variants { Haste = "Run_Haste", Fast = "Run_Fast", Slow = "Run_Slow" },
            ["Sivir"] = new Variants { Haste = "Run_Haste", Fast = "Run_Fast" },
            ["Thresh"] = new Variants { Haste = "Run_HASTE", Fast = "Run_Fast" },
            ["Twitch"] = new Variants { Haste = "Run_HASTE", Fast = "Run_FAST" },
            ["Velkoz"] = new Variants { Haste = "Run_Haste" },
            ["Yasuo"] = new Variants { Haste = "Run_Haste", Fast = "Run_FAST" },
            ["Zac"] = new Variants { Haste = "run_HASTE", Fast = "run_FAST" },
            ["Zed"] = new Variants { Haste = "Run_haste", Fast = "Run_fast" },
        };

        public static Variants Get(string model)
        {
            return model != null && _byModel.TryGetValue(model, out var v) ? v : null;
        }
    }
}
