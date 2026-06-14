using System;
using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI.Behavior
{
    /// <summary>
    /// Faithful port of Riot's <c>AIComponentOutOfCombatRegen.lua</c> (attached by the jungle Leashed
    /// AI). While running, heals the owner <see cref="REGEN_PERCENT_PER_SECOND"/> of its max HP every
    /// second — the gradual camp heal-back you see as the HP bar ticks up after a monster leashes
    /// (NOT an instant full reset). The host AI calls <see cref="Start"/> when it begins leashing /
    /// retreating and <see cref="Stop"/> the moment it (re)engages a target.
    ///
    /// Self-paced via its own accumulator in <see cref="OnUpdate"/> (BaseAIScript ticks every
    /// component each frame), so it needs no access to the host's named-timer API. Reusable by other
    /// out-of-combat regenerators (e.g. river crab) later.
    /// </summary>
    public class OutOfCombatRegenComponent : IAIComponent
    {
        // AIComponentOutOfCombatRegen.lua: REGEN_PERCENT_PER_SECOND default 0.125 (12.5%/s),
        // TimerRegen interval 1s.
        private const float REGEN_PERCENT_PER_SECOND = 0.125f;
        private const float REGEN_INTERVAL = 1.0f;

        private ObjAIBase _owner;
        private bool _running;
        private float _elapsed;

        public void OnAttach(BaseAIScript ai, ObjAIBase owner)
        {
            _owner = owner;
            // lua ComponentInit -> OutOfCombatRegen:Stop(): starts disabled until the AI leashes.
            _running = false;
            _elapsed = 0f;
        }

        /// <summary>Riot OutOfCombatRegen:Start() (ResetAndStartTimer): begin regenerating from now.</summary>
        public void Start()
        {
            _running = true;
            _elapsed = 0f;
        }

        /// <summary>Riot OutOfCombatRegen:Stop(): halt regeneration (called on (re)engage).</summary>
        public void Stop()
        {
            _running = false;
        }

        public void OnUpdate(float diff)
        {
            if (!_running || _owner == null || _owner.IsDead || _owner.GetAIState() == AIState.AI_HALTED)
            {
                return;
            }

            _elapsed += diff / 1000f;
            while (_elapsed >= REGEN_INTERVAL)
            {
                _elapsed -= REGEN_INTERVAL;

                // lua TimerRegen: SetHP(GetHP() + GetMaxHP() * REGEN_PERCENT_PER_SECOND), only while alive.
                float max = _owner.Stats.HealthPoints.Total;
                float current = _owner.Stats.CurrentHealth;
                if (current > 0)
                {
                    _owner.Stats.CurrentHealth = Math.Min(max, current + max * REGEN_PERCENT_PER_SECOND);
                }
            }
        }

        public void OnDetach()
        {
            _running = false;
        }
    }
}
