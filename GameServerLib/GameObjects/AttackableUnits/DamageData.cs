using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;

namespace GameServerLib.GameObjects.AttackableUnits
{
    public class DamageData
    {
        /// <summary>
        /// Unit that inflicted the damage.
        /// </summary>
        public AttackableUnit Attacker { get; set; }
        /// <summary>
        /// Unit that allied "Call For Help" aggro is credited to — Riot DamageEffect::CallForHelpAttackerID
        /// (StatsHealth.cpp Health::ApplyDamage <c>cfhAttackerID</c>), DISTINCT from <see cref="Attacker"/>
        /// (who gets damage/kill credit). Null → falls back to <see cref="Attacker"/> (the common case).
        /// There is NO automatic pet→owner redirect: raw-replay decode (2026-07) showed 4.20 turrets
        /// target pets AND clones directly, no owner-redirect. Set it EXPLICITLY only for a specific
        /// summoned-object BB spell that carries CallForHelpAttackerVar (e.g. Teemo shrooms) if/when a
        /// replay confirms that spell's ally-CFH really credits the owner.
        /// </summary>
        public AttackableUnit CallForHelpAttacker { get; set; }
        /// <summary>
        /// The raw amount of damage to be inflicted (Pre-mitigated damage)
        /// </summary>
        public float Damage { get; set; }
        /// <summary>
        /// The result of this damage (Ex. Dodged, Missed, Invulnerable or Crit)
        /// </summary>
        public DamageResultType DamageResultType { get; set; } = DamageResultType.RESULT_NORMAL;
        /// <summary>
        /// Source of the damage.
        /// </summary>
        public DamageSource DamageSource { get; set; }
        /// <summary>
        /// Type of damage received.
        /// </summary>
        public DamageType DamageType { get; set; }
        /// <summary>
        /// True only for a GENUINE basic-attack swing (set exclusively by ObjAIBase.AutoAttackHit —
        /// the real auto-attack damage-application, fired on missile arrival for ranged). On-hit
        /// SPELLS that deal DAMAGE_SOURCE_ATTACK to proc on-hit effects (Alpha Strike, Yasuo Q,
        /// Ezreal Q, ...) leave this false. Scripts that want "attack-source damage" (on-hit effects)
        /// must test <see cref="DamageSource"/> == DAMAGE_SOURCE_ATTACK, NOT this flag.
        /// </summary>
        public bool IsAutoAttack { get; set; }
        /// <summary>
        /// Mitigated amount of damage (after being reduced by Armor/MR stats) 
        /// </summary>
        public float PostMitigationDamage { get; set; }
        /// <summary>
        /// Unit that will receive the damage.
        /// </summary>
        public AttackableUnit Target { get; set; }
        /// <summary>
        /// Forces an "important" Call For Help on this damage (Riot DamageEffect::ForceCallForHelp,
        /// DamageCallback.h:0x56) — routed to <see cref="GameServerCore.Scripting.CSharp.IAIScript.OnReceiveImportantCallForHelp"/>
        /// in addition to the regular Call For Help. Drives the turret focus-lock (tower-dive aggro).
        /// A spell/buff script can set this to force the lock; champion-vs-champion damage triggers it
        /// implicitly regardless (see ObjAIBase.TakeDamage).
        /// </summary>
        public bool ForceCallForHelp { get; set; }
    }
}
