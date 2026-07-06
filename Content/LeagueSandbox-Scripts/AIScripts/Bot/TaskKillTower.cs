using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace AIScripts
{
    // TaskKillTower.lua: attack the nearest enemy structure (turret / inhibitor / nexus) within attack
    // range + 800. Priority 0.4 normally, rising toward 1.0 as the structure drops below 20% HP (commit
    // to finishing it). Engine drives chase + auto-attack (CloseToTarget), like KillMinion. NOTE: 0.4 is
    // below Retreat-when-threatened, so the bot only sieges when it's safe.
    public class TaskKillTower : BotTask
    {
        private AttackableUnit _lastTarget;

        private AttackableUnit FindNearestTower(BotAI bot)
        {
            float engageSq = (bot.AttackRange + 800.0f) * (bot.AttackRange + 800.0f);
            AttackableUnit best = null;
            float bestSq = bot.MaxTravelDistSquared;
            foreach (var s in bot.EnemyStructures())
            {
                float d = BotAI.DistSq(s.Position, bot.Position);
                if (!s.IsDead && d < engageSq && d < bestSq)
                {
                    bestSq = d;
                    best = s;
                }
            }
            return best;
        }

        public override void UpdatePriority(BotAI bot)
        {
            Priority = 0.0f;
            AttackableUnit tower = FindNearestTower(bot);
            if (tower == null)
            {
                return;
            }
            float max = tower.Stats.HealthPoints.Total;
            float ratio = max > 0.0f ? tower.Stats.CurrentHealth / max : 1.0f;
            Priority = ratio < 0.2f ? 1.0f - ratio : 0.4f;
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

        private void Engage(BotAI bot)
        {
            AttackableUnit tower = FindNearestTower(bot);
            if (tower != null && tower != _lastTarget)
            {
                _lastTarget = tower;
                bot.CloseToTarget(AIState.AI_ATTACK, tower);
            }
        }
    }
}
