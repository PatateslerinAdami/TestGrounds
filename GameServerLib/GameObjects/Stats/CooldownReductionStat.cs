using System;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public class CooldownReductionStat : Stat
    {
        // Keep excess bonuses so selling an item restores the correct total below the cap.
        // Cooldown reduction is stored as a negative cooldown modifier.
        public override float Total => Math.Max(-0.4f, base.Total);
    }
}
