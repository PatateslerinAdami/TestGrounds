using System;
using GameServerCore;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
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
            VariableTable variableTable = null,
            bool skipTenacity = false,
            // BB BBSpellBuffAdd call-site overrides (Riot passes these per-add). Null → fall back to
            // the buff script's BuffMetaData default, so existing script-driven adds are unchanged.
            BuffAddType? buffAddType = null,
            BuffType? buffType = null,
            int? maxStacks = null,
            bool? isHidden = null
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

            BuffAddType = buffAddType ?? BuffScript.BuffMetaData.BuffAddType;
            var resolvedMaxStacks = maxStacks ?? BuffScript.BuffMetaData.MaxStacks;
            var isStackableType = BuffAddType == BuffAddType.STACKS_AND_RENEWS
                || BuffAddType == BuffAddType.STACKS_AND_CONTINUE
                || BuffAddType == BuffAddType.STACKS_AND_OVERLAPS;
            if (isStackableType && resolvedMaxStacks < 2)
            {
                throw new ArgumentException("Error: Tried to create Stackable Buff, but MaxStacks was less than 2.");
            }

            BuffType = buffType ?? BuffScript.BuffMetaData.BuffType;
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
            Hidden = isHidden ?? BuffScript.BuffMetaData.IsHidden;
            if (resolvedMaxStacks > 254 && BuffType != BuffType.COUNTER)
            {
                MaxStacks = 254;
            }
            else
            {
                MaxStacks = Math.Min(resolvedMaxStacks, int.MaxValue);
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
            BuffVars = variableTable ?? new VariableTable();
        }

        public BuffAddType BuffAddType { get; }
        public BuffType BuffType { get; }
        /// <summary>Script-friendly alias for <see cref="Stackable.StackCount"/> (int) — lets scripts read
        /// stacks/counter without touching the wire byte width (e.g. EditBuff(b, b.Stacks - 1)).</summary>
        public int Stacks => StackCount;
        public float Duration { get; }
        public bool Hidden { get; set; }
        public bool IsHidden => Hidden;
        public string Name { get; }
        public Spell OriginSpell { get; }
        public byte Slot { get; private set; }
        // Internal setter for the SetBuffCasterUnit script API (Riot BBSetBuffCasterUnit):
        // scripts re-attribute a running buff's caster, e.g. hand a DoT's credit to another unit.
        public ObjAIBase SourceUnit { get; internal set; }
        public AttackableUnit TargetUnit { get; }
        public float TimeElapsed { get; private set; }
        public VariableTable BuffVars { get; }

        /// <summary>
        /// Script instance for this buff. Casting to a specific buff class gives access its functions and variables.
        /// </summary>
        public IBuffGameScript BuffScript { get; private set; }
        public uint ScriptNameHash { get; }
        public IEventSource ParentScript { get; }

        // Buff-only opt-in (BuffScriptMetaData): credit this buff in the death recap when its damage
        // resolves through the ambient script stack, over the enclosing root frame.
        public bool IsDeathRecapSource => BuffScript?.BuffMetaData?.IsDeathRecapSource ?? false;

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
                using (ScriptContext.Enter(this)) BuffScript.OnActivate(TargetUnit, this, OriginSpell);
            }
            catch (Exception e)
            {
                _logger.Error(null, e);
            }

            // Apply this buff's status effects (explicit SetStatusEffect + BuffType-derived CC state)
            // the same tick it activates, instead of waiting for the target's next UpdateBuffs.
            TargetUnit?.RecomputeBuffEffects();

            // After the buff is fully registered and active, let buffs already on this unit react
            // (Riot buff-script OnBuffAdded hook; spell-shields consume break-markers here — the
            // handler may synchronously DeactivateBuff this very buff, callers check Elapsed()).
            ApiEventManager.OnUnitBuffActivated.Publish(TargetUnit, this);

            // Riot Spellbook::OnBuffActivate (4.17 Spellbook.cpp:799): a buff carrying
            // SpellToggleSlot toggles the linked toggle-spell ON while it is active.
            ApplySpellToggle(true);
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
                using (ScriptContext.Enter(this)) BuffScript.OnDeactivate(TargetUnit, this, OriginSpell);
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

            // Riot Spellbook::OnBuffDeactivate (4.17 Spellbook.cpp:811): toggle the linked
            // toggle-spell back OFF when the buff ends.
            ApplySpellToggle(false);

            // Riot pet-duration buff (BuffManagerClient::GetPetDurationBuff, BuffManagerClient.cpp:654):
            // when the buff that governs a pet's lifetime ends, the pet dies.
            KillPetIfDurationBuff();
        }

        /// <summary>
        /// Toggles the toggle-spell linked to this buff via <see cref="BuffScriptMetaData.SpellToggleSlot"/>
        /// on or off, mirroring Riot Spellbook::OnBuffActivate/OnBuffDeactivate. The metadata value is the
        /// 1-based Lua SpellToggleSlot (Q=1, W=2, E=3, R=4); Riot's Lua::GetToggleSlotFromBuffScript
        /// subtracts 1 to reach the 0-based spellbook slot, and skips when the field is absent — the
        /// default 0 falls through to -1 here and is caught by the same guard.
        /// </summary>
        private void ApplySpellToggle(bool toggle)
        {
            int slot = (BuffScript?.BuffMetaData?.SpellToggleSlot ?? 0) - 1;
            // Slot range mirrors the decomp guard (0..62 = the 63 spellbook slots).
            if (slot < 0 || slot > (int)SpellSlotType.PassiveSpellSlot)
            {
                return;
            }

            if (TargetUnit is ObjAIBase owner
                && owner.Spells.TryGetValue((short)slot, out var spell)
                && spell != null)
            {
                spell.SetSpellToggle(toggle);
            }
        }

        /// <summary>
        /// Kills the pet whose lifetime this buff governs, when the buff carries
        /// <see cref="BuffScriptMetaData.IsPetDurationBuff"/>. Mirrors Riot's pet-duration model
        /// (Spell::Buff::BuffManagerClient::GetPetDurationBuff): the flagged buff is the pet's own
        /// CloneBuff, so the buff holder (<see cref="TargetUnit"/>) IS the pet to kill. Replaces the
        /// per-script manual <c>unit.Die(...)</c> that pet buffs used to run in their OnDeactivate.
        /// </summary>
        private void KillPetIfDurationBuff()
        {
            if (!(BuffScript?.BuffMetaData?.IsPetDurationBuff ?? false))
            {
                return;
            }

            if (TargetUnit is Pet pet && !pet.IsDead)
            {
                pet.Die(new DeathData
                {
                    BecomeZombie = false,
                    DieType = 0,
                    Unit = pet,
                    Killer = pet,
                    DamageType = DamageType.DAMAGE_TYPE_TRUE,
                    DamageSource = DamageSource.DAMAGE_SOURCE_INTERNALRAW,
                    DeathDuration = 0f
                });
            }
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

        public void SetToolTipVar<T>(int tipIndex, T value, bool hide = false) where T : struct
        {
            ToolTipData.Update(tipIndex, value, hide);
            // Any unit — Riot's bulk 0x7F carries buff tooltip vars for turrets/minions too
            // (replay: mid-game blocks with owner netids 0x400004xx, slot 1). The old
            // "is Champion" gate silently dropped those.
            _game.AddToolTipChange(ToolTipData);
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
                NotifyStackChange();
            }
            return result;
        }

        /// <summary>
        /// Routes a stack/count change to the correct wire packet. COUNTER-type buffs carry their value
        /// as an int32 via NPC_BuffUpdateNumCounter — BuffUpdateCount's Count is a byte (truncates &gt;255)
        /// and its ResizeClient path never sets the client mCounter, so a COUNTER buff sent via
        /// BuffUpdateCount would not update the displayed number. Everything else uses BuffUpdateCount with
        /// the REMAINING duration (the client computes mEndTime = now + duration, so full Duration would
        /// reset the timer ring — renewal paths reset TimeElapsed first, so remaining == full there).
        /// Mirrors <see cref="Refresh"/> / EditBuff / EditBuffCounter.
        /// </summary>
        private void NotifyStackChange()
        {
            if (BuffType == BuffType.COUNTER)
            {
                _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(this);
            }
            else
            {
                _game.PacketNotifier.NotifyNPC_BuffUpdateCount(this, Math.Max(0f, Duration - TimeElapsed), TimeElapsed);
            }
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
                    NotifyStackChange();
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
                NotifyStackChange();
            }
        }

        public void Refresh()
        {
            ResetTimeElapsed();
            // Renewal signal = NPC_BuffUpdateCount with runningTime=0 (after ResetTimeElapsed the
            // remaining time == full Duration and rt == 0), NOT NPC_BuffReplace.
            // DECOMP-verified (4.17 mac, BuffManagerClient.cpp): both PKT_NPC_BuffUpdateCount (:869)
            // and PKT_NPC_BuffReplace (:731) handlers converge on the SAME renewal path —
            // BuffInstance::ResizeClient(duration, count, rt) + BuffScriptInstance::Renew(caster).
            // ResizeClient (BuffInstance.cpp:579) sets mStartTime = now - rt, mEndTime = now + duration,
            // so rt=0 fully resets the timer ring exactly like Replace; UpdateCount additionally carries
            // the stack count (Replace keeps the existing count, forces rt=0). Replay-verified: 98% of
            // wire renewals are BuffUpdateCount(rt=0); Replace is reserved for permanent-aura/late-joiner
            // resyncs (project_buff_wire_semantics). Pulse auras (Taric RadianceAura ~1s, ShatterAura
            // ~0.5s) renew via BuffUpdateCount(rt=0). Path taken by every RENEW_EXISTING re-add +
            // FrozenMallet/Rylais slow refresh. Hidden buffs are replicated too (IsHidden=1).
            if (BuffType == BuffType.COUNTER)
            {
                // COUNTER carries its value as int32 via NumCounter (BuffUpdateCount's byte Count
                // would truncate); keep the timer-resetting Replace + a counter re-send.
                _game.PacketNotifier.NotifyNPC_BuffReplace(this);
                _game.PacketNotifier.NotifyNPC_BuffUpdateNumCounter(this);
            }
            else
            {
                _game.PacketNotifier.NotifyNPC_BuffUpdateCount(this, Duration, 0f);
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
                using (ScriptContext.Enter(this)) BuffScript.OnUpdate(this, diff);
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
