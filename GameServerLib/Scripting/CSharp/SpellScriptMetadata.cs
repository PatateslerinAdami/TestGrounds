using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using System;
using System.Numerics;

namespace LeagueSandbox.GameServer.Scripting.CSharp
{
    public class SpellScriptMetadata
    {
        public int AmmoPerCharge { get; set; } = 1;
        public string AutoAuraBuffName { get; set; } = "";
        // TODO: Replace string with empty event class.
        public string AutoBuffActivateEvent { get; set; } = "";
        /// <summary>
        //Optional per rank cooldown override. This is affected by cdr
        /// </summary>
        public float[] AutoCooldownByLevel { get; set; } = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
        public string AutoItemActivateEffect { get; set; } = "";
        /// <summary>
        /// Whether or not the caster should automatically face the end position of the spell.
        /// </summary>
        public bool AutoFaceDirection { get; set; } = true;
        /// <summary>
        /// When true, <see cref="LeagueSandbox.GameServer.GameObjects.SpellNS.Spell.Cast"/> skips
        /// the default "10-unit forward stub" that overwrites <c>CastInfo.TargetPosition</c> right
        /// before <c>NotifyNPC_CastSpellAns</c>. Lets the script write the actual landing position
        /// into <c>spell.CastInfo.TargetPosition</c> from <c>OnSpellPreCast</c> so the wire packet
        /// carries it instead of the stub.
        ///
        /// <para>Required for blink-style spells (e.g. KatarinaE) where Riot's wire shows
        /// <c>CastSpellAns.targetPosition = landing</c> (the replay-empirical match). Note this is
        /// purely a wire-shape concern; the actual post-blink position-sync runs through a separate
        /// <c>WaypointGroup</c> with <c>HasTeleportID=true</c> packet (= server-side
        /// <c>NotifyTeleport</c>, broadcast to all teammates with vision).
        /// Both are needed for full fidelity.</para>
        /// </summary>
        /// TODO: Move this somewhere else
        public bool OverrideTargetPositionInScript { get; set; } = false;
        
        public float CastTime { get; set; } = 0.0f;
        public bool CastingBreaksStealth { get; set; } = false;
        /// <summary>
        /// Determines how long the spell should be channeled (overrides content based channel
        /// duration). Triggers OnSpellChannel events if value is above 0. Use for non-charge
        /// channel spells (Kat R, Recall etc.). For charge-style spells (Varus Q) use
        /// <see cref="ChargeDuration"/> instead — that activates the charge pipeline.
        /// </summary>
        public float ChannelDuration { get; set; } = 0.0f;

        /// <summary>
        /// Charge-spell bar-fill / wire-side duration override. When &gt; 0:
        /// <list type="bullet">
        ///   <item>Activates the charge pipeline (OnSpellChargeStart/Tick/Fire/Cancel events)
        ///     instead of the channel pipeline, regardless of <c>SpellData.UseChargeChanneling</c>.</item>
        ///   <item>Drives the wire packet's <c>CastInfo.DesignerTotalTime</c> — i.e. how long the
        ///     client's charge bar takes to fill. Set this to the player-visible "charged at max"
        ///     time (e.g. Varus Q: 1.5s — matches SpellTargeter*.RangeGrowthDuration in JSON).</item>
        /// </list>
        /// Server's auto-expire timer is separate — see <see cref="ChargeMaxHoldDuration"/> for
        /// spells like Varus Q that allow holding past max-charge before auto-cancel.
        /// </summary>
        public float ChargeDuration { get; set; } = 0.0f;

        /// <summary>
        /// Max server-side hold duration for charge spells. When &gt; 0, the server keeps the
        /// charge state alive until this time elapses; at that point <see cref="ChargeDuration"/>
        /// has already passed (bar full, range maxed) but the player gets extra time to release.
        /// At MaxHold timeout, the engine publishes <see cref="ApiEventManager.OnSpellChargeCancel"/>
        /// with <c>ChannelingStopSource.TimeCompleted</c> — script decides policy
        /// (refund mana / auto-fire / other).
        ///
        /// <para>Defaults to <c>0</c> which falls back to <see cref="ChargeDuration"/> — i.e. the
        /// charge auto-completes at bar-fill. Most charge spells use this default. Varus Q sets
        /// <c>ChargeMaxHoldDuration = 4f</c> (1.5s bar-fill + 2.5s held-max = 4s total before
        /// auto-cancel-with-50%-refund per spell description).</para>
        /// </summary>
        public float ChargeMaxHoldDuration { get; set; } = 0.0f;
        public bool CooldownIsAffectedByCDR { get; set; } = true;
        public bool DoOnPreDamageInExpirationOrder { get; set; } = false;
        public bool DoesntBreakShields { get; set; } = false;
        // TODO: Find a use for this.
        public bool IsDamagingSpell { get; set; } = false;
        public bool IsDeathRecapSource { get; set; } = false;
        
        public bool IsDebugMode { get; set; } = false;
        public bool IsPetDurationBuff { get; set; } = false;
        public bool IsNonDispellable { get; set; } = false;
        public MissileParameters MissileParameters { get; set; } = null;
        public bool NotSingleTargetSpell { get; set; } = false;
        // Never appears below 2?
        public int OnPreDamagePriority { get; set; } = 0;
        public bool OverrideCooldownCheck { get; set; } = false;
        public bool PermeatesThroughDeath { get; set; } = false;
        public bool PersistsThroughDeath { get; set; } = false;
        public string PopupMessage1 { get; set; } = "";
        public float SetSpellDamageRatio { get; set; } = 0.0f;
        public float SpellDamageRatio { get; set; } = 0.0f;

        public int SpellToggleSlot { get; set; } = 0;

        /// <summary>
        /// Determines whether or not the spell stops movement and triggers spell casts (and post).
        /// Usually should not be true if the spell is an item active, summoner spell, missile spell, or otherwise purely buff related spell.
        /// </summary>
        public bool TriggersSpellCasts { get; set; } = false;
    }

    /// <summary>
    /// Parameters which determine how a missile behaves.
    /// </summary>
    public class MissileParameters
    {
        /// <summary>
        /// Next-target pick rule for chain bounces. Default Random (S1 chain framework);
        /// set Nearest only with evidence (Katarina Q). See BounceSelection docs.
        /// </summary>
        public BounceSelection BounceSelection { get; set; } = BounceSelection.Random;
        /// <summary>
        /// Whether or not the missile should be able to hit something multiple times.
        /// Will only hit again if the missile has bounced to a different unit.
        /// Is overridden by CanHitSameTargetConsecutively.
        /// </summary>
        public bool CanHitSameTarget { get; set; } = false;
        /// <summary>
        /// Whether or not the missile should be able to hit something multiple times in a row,
        /// regardless of if it has bounced to another unit.
        /// Overrides CanHitSameTarget.
        /// </summary>
        public bool CanHitSameTargetConsecutively { get; set; } = false;
        /// <summary>
        /// Maximum number of hits before the chain stops bouncing (the missile itself
        /// finishes its current flight either way — S4 caps bouncing, not existence).
        /// 0 = never bounce (plain single-target missile). Flat fallback used when
        /// MaximumHitsPerLevel is not set.
        /// </summary>
        public int MaximumHits { get; set; } = 0;
        /// <summary>
        /// Per-spell-level hit budget, mirroring Riot's Lua ChainMissileParameters
        /// .MaximumHits table (-> mi_MaximumHits[6], S4 SpellDataResource.h:182).
        /// Indexed with the server's 1-based spell level like SpellData.CastRange
        /// (index 0 = unlearned). Null/empty = use the flat MaximumHits.
        /// </summary>
        public int[] MaximumHitsPerLevel { get; set; } = null;
        /// <summary>
        /// Chain target filters, mirroring Riot's ChainMissileParameters CanHitCaster /
        /// CanHitFriends / CanHitEnemies (S4 mi_CanHitSelf/-Friends/-Enemies). These are
        /// FILTERS, not budgets — there is exactly one hit counter (S4
        /// mi_numTargetsAlreadyHit). Defaults match SpellDataResource.cpp:156-160.
        /// Applied on top of SpellData.IsValidTarget in the legacy single-pool chain
        /// path only; alternating chains (both bounce names set) filter by pool.
        /// </summary>
        public bool CanHitCaster { get; set; } = false;
        /// <inheritdoc cref="CanHitCaster"/>
        public bool CanHitFriends { get; set; } = false;
        /// <inheritdoc cref="CanHitCaster"/>
        public bool CanHitEnemies { get; set; } = true;

        /// <summary>
        /// Resolves the hit budget for the given (1-based) spell level: per-level entry
        /// when MaximumHitsPerLevel is set, otherwise the flat MaximumHits.
        /// </summary>
        public int GetMaximumHits(int spellLevel)
        {
            if (MaximumHitsPerLevel == null || MaximumHitsPerLevel.Length == 0)
            {
                return MaximumHits;
            }
            return MaximumHitsPerLevel[Math.Clamp(spellLevel, 0, MaximumHitsPerLevel.Length - 1)];
        }
        /// <summary>
        /// What kind of behavior this missile has.
        /// </summary>
        public MissileType Type { get; set; } = MissileType.None;
        /// <summary>
        /// Position the missile should end at, only useful for Arc and Circle missile types.
        /// </summary>
        public Vector2 OverrideEndPosition { get; set; }

        /// <summary>
        /// Chain-missile bounce spells. Two modes:
        /// (1) Exactly ONE of the two fields set — legacy single-pool chain: every bounce
        ///     flies under that spell's hash/speed/radius, but SpellOrigin/Parameters stay
        ///     the PARENT's (hits keep firing in the parent script; target filter = parent
        ///     flags). Use the field matching the pool's allegiance for readability —
        ///     enemy chains (Katarina Q -> KatarinaQMis, Fiddle E ->
        ///     FiddleSticksDarkWindMissile) use Enemy, a pure heal-bounce chain would use
        ///     Ally. Behavior is identical either way.
        /// (2) BOTH fields set — alternating ally/enemy chain (Nami W): each bounce targets
        ///     the OPPOSITE allegiance of the unit just hit and performs a FULL spell switch
        ///     (SpellOrigin + MissileParameters + data of the next spell, hits fire in that
        ///     spell's script). Replay-verified wire: NamiW cast ans, then alternating
        ///     NamiWMissileAlly/NamiWMissileEnemy MISREPs per bounce.
        /// </summary>
        public string BounceSpellNameEnemy { get; set; }
        /// <inheritdoc cref="BounceSpellNameEnemy"/>
        public string BounceSpellNameAlly { get; set; }

        /// <summary>
        /// Optional SpellDataFlags override for chain target validation (bounce selection
        /// AND hit validation), replacing the spell JSON's flags. Needed when Riot's
        /// server-side bounce rule is stricter than the (client-export, read-only) JSON:
        /// Nami W JSONs allow AffectMinions|AffectNeutral, but replays show 245/245 bounce
        /// targets are champions across 3 games — the champion-only rule lived in Riot's
        /// server script layer, which this property represents. 0 = use the JSON flags.
        /// </summary>
        public SpellDataFlags BounceAffectsOverride { get; set; } = 0;

        /// <summary>
        /// Optional collision radius for the missile, overriding SpellData.LineWidth
        /// from the JSON. Useful for spells where the visible hit area doesn't match the
        /// JSON's LineWidth (often 0 for non-line-shaped missiles), like Diana W's orbs
        /// (visual radius ~175u, but DianaOrbsMissile.json has LineWidth=0). Set to null
        /// to use the JSON value.
        /// </summary>
        public int? CollisionRadius { get; set; }

        /// <summary>
        /// Optional override for SpellData.MissileTargetHeightAugment (the "fly height" above
        /// ground carried in the spawn MissileReplication). Needed when Riot's server zeroes or
        /// changes the augment relative to the (read-only) client-export JSON — e.g. Aatrox E:
        /// both cone-missile JSONs say 100, but the replay shows the SIDE missiles
        /// (AatroxEConeMissile, 294 packets) flying at ground level (StartY-CasterY=0, velY=0)
        /// while only the center missile (AatroxEConeMissile2) carries the +100. Null = use JSON.
        /// </summary>
        public float? OverrideHeightAugment { get; set; }

        /// <summary>
        /// Scheduled one-shot speed change: after TimedSpeedDeltaTime seconds of flight the
        /// missile's speed changes by TimedSpeedDelta — replicated in the spawn
        /// MissileReplication so the client mirrors it natively (replay-verified wire shape:
        /// spawn carries delta+time, vision-acquire after the change carries FLT_MAX).
        /// 0 = no change. Example: Jinx R (Characters/Jinx/R.cs) — rocket boost +500 after
        /// 0.75s, with the booster FX triggered off missile.TimedSpeedChangeApplied.
        /// </summary>
        public float TimedSpeedDelta { get; set; }
        public float TimedSpeedDeltaTime { get; set; }
    }
}
