using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace AIScripts
{
    // TaskKillMinion.lua: last-hit — engage the LOWEST-HP enemy minion within attack range + 500.
    // Priority 0.45 when such a minion exists. The Lua runs a manual AI_FOLLOW/AI_ATTACK approach loop
    // with TurnOn/OffAutoAttack; we let the engine drive chase + auto-attack from a single
    // SetStateAndCloseToTarget (same as the pet / lane-minion AIs). NOTE: 0.45 < Retreat's 0.75 floor,
    // so the crude bot rarely actually last-hits — faithful to the Lua's broken priority balance.
    public class TaskKillMinion : BotTask
    {
        private AttackableUnit _lastTarget;

        private LaneMinion FindNearLowestHpMinion(BotAI bot)
        {
            float engage = bot.AttackRange + 500f;
            float engageSq = engage * engage;
            LaneMinion best = null;
            float lowestHp = float.MaxValue;
            foreach (var m in bot.EnemyMinions())
            {
                if (!m.IsDead
                    && BotAI.DistSq(bot.Position, m.Position) <= engageSq
                    && m.Stats.CurrentHealth < lowestHp)
                {
                    best = m;
                    lowestHp = m.Stats.CurrentHealth;
                }
            }
            return best;
        }

        public override void UpdatePriority(BotAI bot)
        {
            Priority = FindNearLowestHpMinion(bot) != null ? 0.45f : 0.0f;
        }

        public override void BeginTask(BotAI bot)
        {
            bot.StopAttacking();
            _lastTarget = null;
            Engage(bot);
        }

        public override void Tick(BotAI bot)
        {
            Engage(bot);
        }

        // Re-target the current lowest-HP minion; only re-issue the order when the target changes (the
        // engine keeps chasing + auto-attacking the set target between ticks).
        private void Engage(BotAI bot)
        {
            LaneMinion target = FindNearLowestHpMinion(bot);
            if (target != null && target != _lastTarget)
            {
                _lastTarget = target;
                bot.CloseToTarget(AIState.AI_ATTACK, target);
            }
        }
    }
}
