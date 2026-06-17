using System;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace AIScripts
{
    // TaskKillHero.lua: ONE task per enemy hero — chase + attack it. Priority 0.5 within attackRange+500,
    // falling off linearly to 0 at 2× that range. The engine drives chase + auto-attack from a single
    // CloseToTarget (AI_ATTACK_HERO), like KillMinion — the Lua's manual FOLLOW/ATTACK + TurnOn/Off loop
    // folds into that.
    //
    // Anti-kite: the Lua's AntiKiteTimer set a `ReducePriority` flag that UpdatePriority then NEVER read
    // (vestigial / broken). We COMPLETE the intent (flagged deviation): if the hero takes ~no damage for
    // 2s while we're still chasing, halve this task's priority so the bot stops over-committing to an
    // uncatchable kiter.
    public class TaskKillHero : BotTask
    {
        public Champion Target;

        private const float CHECK_INTERVAL = 2.0f;
        private float _nextKiteCheck;
        private float _lastTargetHp;
        private bool _kiting;

        public override void UpdatePriority(BotAI bot)
        {
            Priority = 0.0f;
            if (Target == null || Target.IsDead)
            {
                return;
            }
            float engage = bot.AttackRange + 500.0f;
            float dist = Math.Max(BotAI.Dist(bot.Position, Target.Position), 1.0f);
            if (dist < engage)
            {
                Priority = 0.5f;
            }
            else if (dist < engage * 2.0f)
            {
                Priority = 0.5f * (1.0f - (dist - engage) / engage);
            }
            if (_kiting)
            {
                Priority *= 0.5f;   // anti-kite (completing the Lua's vestigial ReducePriority)
            }
        }

        public override void BeginTask(BotAI bot)
        {
            if (Target == null)
            {
                return;
            }
            bot.CloseToTarget(AIState.AI_ATTACK_HERO, Target);
            _lastTargetHp = Target.Stats.CurrentHealth;
            _nextKiteCheck = bot.Now + CHECK_INTERVAL;
            _kiting = false;
        }

        public override void Tick(BotAI bot)
        {
            if (Target == null || Target.IsDead)
            {
                return;
            }
            // The AttackTo order from BeginTask persists (engine keeps chasing + attacking); we only
            // re-issue if the engine lost the target, and run the 2s anti-kite check.
            if (bot.CurrentTargetNetId != Target.NetId)
            {
                bot.CloseToTarget(AIState.AI_ATTACK_HERO, Target);
            }
            if (bot.Now >= _nextKiteCheck)
            {
                _nextKiteCheck = bot.Now + CHECK_INTERVAL;
                _kiting = Target.Stats.CurrentHealth >= _lastTargetHp - 10.0f && bot.BotIsMoving;
                _lastTargetHp = Target.Stats.CurrentHealth;
            }
        }
    }
}
