using System;
using System.Collections.Generic;
using GameServerCore.Enums;

namespace AIScripts
{
    // Walk back toward the regroup point (own fountain). DEVIATES from TaskRetreat.lua's unconditional
    // 0.75 priority floor — that floor is a crude generic default (it makes the bot retreat almost always
    // and never farm/fight). Instead we reconstruct Riot's PER-CHAMPION behavior-tree logic
    // (bt.<Champion>_HighThreatManagement, which gates RetreatHighThreat on HealthRatio + Threat): retreat
    // priority is LOW when safe (so KillMinion/PushLane win), and HIGH only when HP is low or enemy
    // champions are near. Thresholds are tuned heuristics — the BT's exact values are Dominion-era and not
    // 4.20-authoritative, so the MECHANISM is faithful, the numbers are ours. The emergency HP-drop bump
    // (>110 HP lost in 2s → 1.0) is kept from the Lua.
    public class TaskRetreat : BotTask
    {
        private const float CRITICAL_HP_RATIO = 0.30f;  // below → escape regardless of threat
        private const float THREAT_RANGE = 1400.0f;      // enemy champion within this = threat
        private const float SAFE_PRIORITY = 0.05f;       // no HP danger + no threat → let farm/push win
        private const float EMERGENCY_HP_DROP = 110.0f;

        // HP sampled per integer-second bucket (Riot keys this table by os.time()), pruned to a 2s window.
        private readonly Dictionary<long, float> _hpHistory = new Dictionary<long, float>();

        public override void UpdatePriority(BotAI bot)
        {
            float hpRatio = bot.MaxHp > 0f ? bot.Hp / bot.MaxHp : 1.0f;
            int threats = bot.EnemyChampionsNear(THREAT_RANGE);

            if (hpRatio < CRITICAL_HP_RATIO)
            {
                Priority = 0.95f;
            }
            else if (threats > 0)
            {
                // Urgency rises with the number of nearby enemies and with missing HP.
                float threat = Math.Min(1.0f, threats * 0.5f);
                Priority = 0.30f + 0.60f * threat * (1.0f - hpRatio);
            }
            else
            {
                Priority = SAFE_PRIORITY;
            }

            // Emergency-retreat tracking: keep one HP sample per second over a 2s window; if HP two
            // seconds ago minus current HP exceeds the threshold, force max priority.
            long sec = (long)bot.Now;
            var stale = new List<long>();
            foreach (var kv in _hpHistory)
            {
                if (kv.Key < sec - 2)
                {
                    stale.Add(kv.Key);
                }
            }
            foreach (long k in stale)
            {
                _hpHistory.Remove(k);
            }
            if (!_hpHistory.ContainsKey(sec))
            {
                _hpHistory[sec] = bot.Hp;
            }
            if (_hpHistory.TryGetValue(sec - 2, out float hpThen) && hpThen - bot.Hp > EMERGENCY_HP_DROP)
            {
                Priority = 1.0f;
            }
        }

        public override void BeginTask(BotAI bot)
        {
            bot.StopAttacking();
            bot.MoveToPoint(AIState.AI_RETREAT, bot.RegroupPos);
        }
    }
}
