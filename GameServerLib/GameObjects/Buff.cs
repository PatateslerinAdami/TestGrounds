using System;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.Other;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Scripting.CSharp;
using log4net;
using static GameServerCore.Content.HashFunctions;

namespace LeagueSandbox.GameServer.GameObjects
{
    public class Buff : Stackable, IEventSource
    {
        private static readonly ILog _logger = LoggerProvider.GetLogger();

        // Crucial Vars.
        private readonly Game _game;

        // Function Vars.
        public bool Remove;

        /// <summary>
        /// Wire duration used for "infinite" buffs. The S4 client renders a buff as permanent
        /// (no decaying timer ring) once its span (mEndTime - mStartTime) is >= 20000s
        /// (BuffInstance.cpp:256, kPermanentDuration). We use a margin above that threshold so
        /// float rounding of (Duration - TimeElapsed) + TimeElapsed can never dip below it.
        /// Server-side the buff is plain finite at this value (~7h) and is expected to be removed
        /// explicitly long before it elapses.
        /// </summary>
        public const float PERMANENT_DURATION = 25000f;

        public Buff(
            Game game,
            string buffName,
            float duration,
            int stacks,
            Spell originSpell,
            AttackableUnit onto,
            ObjAIBase from,
            bool infiniteDuration = false,
            IEventSource parent = null,
            BuffVariables buffVariables = null,
            bool skipTenacity = false
        )
        {
            if (duration < 0)
            {
                throw new ArgumentException("Error: Duration was set to under 0.");
            }

            _game = game;
            Remove = false;
            Name = buffName;

            ParentScript = parent;
            LoadScript();
            ScriptNameHash = HashString(Name);

            BuffAddType = BuffScript.BuffMetaData.BuffAddType;
            var isStackableType = BuffAddType == BuffAddType.STACKS_AND_RENEWS
                || BuffAddType == BuffAddType.STACKS_AND_CONTINUE
                || BuffAddType == BuffAddType.STACKS_AND_OVERLAPS;
            if (isStackableType && BuffScript.BuffMetaData.MaxStacks < 2)
            {
                throw new ArgumentException("Error: Tried to create Stackable Buff, but MaxStacks was less than 2.");
            }

            BuffType = BuffScript.BuffMetaData.BuffType;
            // "Infinite" buffs are just buffs with a permanent-on-the-wire duration; the client needs
            // span >= 20000s to drop the decaying timer ring (see PERMANENT_DURATION). There is no
            // separate server-side never-expire flag anymore — these are removed explicitly by scripts.
            var effectiveDuration = infiniteDuration ? PERMANENT_DURATION : duration;

            // Tenacity: shorten reducible CC durations by the target's aggregated PercentCCReduction
            // (effectiveDuration = base * (1 - tenacity)), matching the 4.20 model — the reduction is
            // applied once at buff creation and the shortened duration goes out on the wire; the client
            // does not re-reduce (it uses mPercentCCReduction only to draw the LoC bar). See
            // docs/TENACITY_IMPLEMENTATION_PLAN.md. skipTenacity avoids double-applying on internal
            // buff reconstructions (STACKS_AND_CONTINUE) whose durations derive from already-reduced buffs.
            if (!skipTenacity && !infiniteDuration && BuffType.IsTenacityReducible() && onto != null)
            {
                var tenacity = onto.Stats.PercentCCReduction;
                if (tenacity > 0f)
                {
                    effectiveDuration *= 1f - tenacity;
                }
            }

            Duration = effectiveDuration;
            Hidden = BuffScript.BuffMetaData.IsHidden;
            if (BuffScript.BuffMetaData.MaxStacks > 254 && BuffType != BuffType.COUNTER)
            {
                MaxStacks = 254;
            }
            else
            {
                MaxStacks = Math.Min(BuffScript.BuffMetaData.MaxStacks, int.MaxValue);
            }
            OriginSpell = originSpell;
            if (onto.HasBuff(Name) && BuffAddType == BuffAddType.STACKS_AND_OVERLAPS)
            {
                // Put parent buff data into children buffs
                StackCount = onto.GetBuffWithName(Name).StackCount;
                Slot = onto.GetBuffWithName(Name).Slot;
            }
            else
            {
                StackCount = stacks;
                Slot = onto.GetNewBuffSlot(this);
            }

            SourceUnit = from;
            TimeElapsed = 0;
            TargetUnit = onto;

            ToolTipData = new ToolTipData(TargetUnit, null, this);
            Variables = buffVariables ?? new BuffVariables();
        }

        public BuffAddType BuffAddType { get; }
        public BuffType BuffType { get; } /// TODO: Add comments to BuffType enum.
        public float Duration { get; }
        public bool Hidden { get; set; }
        public bool IsHidden => Hidden;
        public string Name { get; }
        public Spell OriginSpell { get; }
        public byte Slot { get; private set; }
        public ObjAIBase SourceUnit { get; }
        public AttackableUnit TargetUnit { get; }
        public float TimeElapsed { get; private set; }
        public BuffVariables Variables { get; }

        /// <summary>
        /// Script instance for this buff. Casting to a specific buff class gives access its functions and variables.
        /// </summary>
        public IBuffGameScript BuffScript { get; private set; }
        public uint ScriptNameHash { get; }
        public IEventSource ParentScript { get; }

        public StatusFlags StatusEffectsToEnable { get; private set; }
        public StatusFlags StatusEffectsToDisable { get; private set; }
        /// <summary>
        /// Used to update player buff tool tip values.
        /// </summary>
        public ToolTipData ToolTipData { get; protected set; }

        public void LoadScript()
        {
            ApiEventManager.RemoveAllListenersForOwner(BuffScript);
            BuffScript = CSharpScriptEngine.CreateObjectStatic<IBuffGameScript>("Buffs", Name) ?? new BuffScriptEmpty();
        }

        public void ActivateBuff()
        {
            Remove = false;

            // Tag the script's StatsModifier with its owning buff BEFORE OnActivate runs
            // (scripts call AddStatModifier inside OnActivate) — the slow registry in Stats
            // derives the named-effect key from this.
            if (BuffScript.StatsModifier != null)
            {
                BuffScript.StatsModifier.SourceBuff = this;
            }

            try
            {
                using var _scope = Profiler.Scope($"buff:{Name}.OnActivate", "scripts");
                BuffScript.OnActivate(TargetUnit, this, OriginSpell);
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }

            // Apply this buff's status effects (explicit SetStatusEffect + BuffType-derived CC state)
            // the same tick it activates, instead of waiting for the target's next UpdateBuffs.
            TargetUnit?.RecomputeBuffEffects();
        }

        public void DeactivateBuff()
        {
            if (Remove)
            {
                return;
            }
            Remove = true; // To prevent infinite loop with OnDeactivate calling events

            try
            {
                using var _scope = Profiler.Scope($"buff:{Name}.OnDeactivate", "scripts");
                BuffScript.OnDeactivate(TargetUnit, this, OriginSpell);
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }

            ApiEventManager.RemoveAllListenersForOwner(BuffScript);

            if (BuffScript.StatsModifier != null)
            {
                TargetUnit.RemoveStatModifier(BuffScript.StatsModifier);
            }

            ApiEventManager.OnBuffDeactivated.Publish(this);
            ApiEventManager.OnUnitBuffDeactivated.Publish(TargetUnit, this);
        }

        public bool Elapsed()
        {
            return Remove;
        }

        public StatsModifier GetStatsModifier()
        {
            return BuffScript.StatsModifier;
        }

        public void SetStatusEffect(StatusFlags flag, bool enabled)
        {
            if (enabled)
            {
                StatusEffectsToEnable |= flag;
                StatusEffectsToDisable &= ~flag;
            }
            else
            {
                StatusEffectsToDisable |= flag;
                StatusEffectsToEnable &= ~flag;
            }
        }

        public void SetToolTipVar<T>(int tipIndex, T value) where T : struct
        {
            ToolTipData.Update(tipIndex, value);

            if (TargetUnit is Champion champ)
            {
                champ.AddToolTipChange(ToolTipData);
            }
        }

        public bool IsBuffInfinite()
        {
            return Duration >= PERMANENT_DURATION;
        }

        public bool IsBuffSame(string buffName)
        {
            return buffName == Name;
        }

        public void ResetTimeElapsed()
        {
            TimeElapsed = 0;
        }

        public void SetSlot(byte slot)
        {
            Slot = slot;
            ToolTipData?.SetSlot(slot);
        }
        public void SetToExpired()
        {
            TimeElapsed = Duration;
        }

        public override bool IncrementStackCount() => IncrementStackCount(true);

        public bool IncrementStackCount(bool sendPacket)
        {
            if (BuffAddType == BuffAddType.STACKS_AND_RENEWS ||
                BuffAddType == BuffAddType.RENEW_EXISTING ||
                BuffAddType == BuffAddType.STACKS_AND_CONTINUE)
            {
                ResetTimeElapsed();
            }

            var result = base.IncrementStackCount();

            if (result && sendPacket)
            {
                // Client computes mEndTime = now + duration (BuffInstance::ResizeClient), so the wire
                // `duration` must be REMAINING time, not full — otherwise a mid-life stack change resets
                // the timer ring to full. Renewal paths reset TimeElapsed first, so remaining == full there.
                _game.PacketNotifier.NotifyNPC_BuffUpdateCount(this, Math.Max(0f, Duration - TimeElapsed), TimeElapsed);
            }
            return result;
        }

        public override bool DecrementStackCount() => DecrementStackCount(true);

        public bool DecrementStackCount(bool sendPacket)
        {
            var result = base.DecrementStackCount();

            if (result)
            {
                if (StackCount <= 0)
                {
                    DeactivateBuff();
                }
                else if (sendPacket)
                {
                    // Remaining duration, not full — see IncrementStackCount.
                    _game.PacketNotifier.NotifyNPC_BuffUpdateCount(this, Math.Max(0f, Duration - TimeElapsed), TimeElapsed);
                }
            }
            return result;
        }

        public override void SetStacks(int newStacks)
        {
            SetStacks(newStacks, true);
        }

        public void SetStacks(int newStacks, bool sendPacket = true)
        {
            base.SetStacks(newStacks);
            if (sendPacket)
            {
                // Remaining duration, not full — see IncrementStackCount.
                _game.PacketNotifier.NotifyNPC_BuffUpdateCount(this, Math.Max(0f, Duration - TimeElapsed), TimeElapsed);
            }
        }

        public void Refresh()
        {
            ResetTimeElapsed();
            if (!IsHidden)
            {
                _game.PacketNotifier.NotifyNPC_BuffReplace(this);
            }
        }
        public void Update(float diff)
        {
            TimeElapsed += diff / 1000.0f;

            if (!(Math.Abs(Duration) > Extensions.COMPARE_EPSILON))
            {
                DeactivateBuff();
                return;
            }

            try
            {
                using var _scope = Profiler.Scope($"buff:{Name}.OnUpdate", "scripts");
                BuffScript.OnUpdate(diff);
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }

            if (TimeElapsed >= Duration)
            {
                DeactivateBuff();
            }
        }
    }
}
