using System.Collections.Generic;
using GameServerCore.Enums;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits
{
    /// <summary>
    /// Internal source of truth for a unit's action-state, mirroring Riot's <c>CharacterState</c> (decomp
    /// AI/Character/CharacterState.h). Backed by ref-counted capability disable-holds + a plain-bit layer +
    /// the buff-derived layer, replacing the old flat <see cref="StatusFlags"/> bitmask.
    ///
    /// M2 rebuild — Phase 1 is pure encapsulation: <see cref="Status"/> is computed IDENTICALLY to the
    /// previous inline AttackableUnit logic, so behaviour and every reader are unchanged. Later phases route
    /// stun/root/silence/disarm through the ref-counted capabilities (Riot has no Stunned/Rooted state) and
    /// migrate consumers. See docs/M2_CHARACTERSTATE_REBUILD_PLAN.md.
    /// </summary>
    internal sealed class CharacterState
    {
        // Default-ON capability bits are ref-counted DISABLE-holds (Riot CharacterState::RefCountedState):
        // a capability is enabled iff its hold counter is 0, so overlapping disablers compose correctly —
        // one source releasing its hold must not re-enable the capability while another still holds it
        // disabled (the Xerath-Q-lockout overlap class of bug).
        private int _disableCanMove;
        private int _disableCanAttack;
        private int _disableCanCast;
        private int _disableCanMoveEver;

        // Ref-counted ENABLE-holds (Riot CharacterState::RefCountedState, ENABLE polarity — opposite of the
        // default-ON capability disable-holds above): default-OFF, the bit is set iff its hold counter > 0,
        // so overlapping enablers compose (two buffs enable it; one expiring must NOT clear it while the
        // other still holds it). Riot's RefCountedStates struct (decomp CharacterState.h) ref-counts exactly
        // {CanAttack, CanCast, CanMove, Immovable, Suppressed, Sleep, NearSight, DodgePiercing}: the caps are
        // the disable-holds above; these five are the enable-polarity states. (Riot's
        // kNonRefCountedCharacterStates feature would collapse each to set-1/0; we don't model it and always
        // ref-count, consistent with the capability holds above.)
        private static readonly StatusFlags[] EnableHoldFlags =
        {
            StatusFlags.DodgePiercing, StatusFlags.Suppressed, StatusFlags.Sleep,
            StatusFlags.NearSighted, StatusFlags.Immovable
        };
        private readonly Dictionary<StatusFlags, int> _enableHolds = new Dictionary<StatusFlags, int>();

        // Non-capability flags: plain set/clear bitfield (imperative SetStatus) — everything not in the
        // capability or enable-hold masks.
        private StatusFlags _nonCapabilityBase;

        // Buff-derived layer: OR of every active buff's contribution, rebuilt each RecomputeBuffEffects
        // (a full recompute over the live BuffList — functionally a ref-count for buff sources).
        private StatusFlags _buffEnable;
        private StatusFlags _buffDisable;

        private const StatusFlags CapabilityMask =
            StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMoveEver;

        // Ref-counted enable-hold flags (routed to their hold counters instead of the plain bitfield).
        private const StatusFlags RefCountedEnableMask =
            StatusFlags.DodgePiercing | StatusFlags.Suppressed | StatusFlags.Sleep
            | StatusFlags.NearSighted | StatusFlags.Immovable;

        /// <summary>The effective action-state bitmask (capabilities + plain bits + buff layer).</summary>
        public StatusFlags Status { get; private set; }

        /// <summary>
        /// Imperative set/clear of one or more flags. Capability bits are ref-counted disable-holds; all
        /// other bits are plain set/clear. <c>Set(StatusFlags.None, true)</c> is a pure recompute trigger.
        /// </summary>
        public void Set(StatusFlags status, bool enabled)
        {
            if ((status & StatusFlags.CanMove) != 0) _disableCanMove = RefHold(_disableCanMove, enabled);
            if ((status & StatusFlags.CanAttack) != 0) _disableCanAttack = RefHold(_disableCanAttack, enabled);
            if ((status & StatusFlags.CanCast) != 0) _disableCanCast = RefHold(_disableCanCast, enabled);
            if ((status & StatusFlags.CanMoveEver) != 0) _disableCanMoveEver = RefHold(_disableCanMoveEver, enabled);

            // Enable-polarity ref-counted holds: enabled=true adds a hold, false releases one.
            StatusFlags enableBits = status & RefCountedEnableMask;
            if (enableBits != 0)
            {
                foreach (var flag in EnableHoldFlags)
                {
                    if ((enableBits & flag) == 0) continue;
                    _enableHolds.TryGetValue(flag, out int c);
                    _enableHolds[flag] = RefEnableHold(c, enabled);
                }
            }

            StatusFlags otherBits = status & ~CapabilityMask & ~RefCountedEnableMask;
            if (otherBits != 0)
            {
                if (enabled)
                {
                    _nonCapabilityBase |= otherBits;
                }
                else
                {
                    _nonCapabilityBase &= ~otherBits;
                }
            }

            Recompute();
        }

        /// <summary>
        /// Replace the buff-derived enable/disable layer (called from AttackableUnit.RecomputeBuffEffects
        /// after re-aggregating the live BuffList).
        /// </summary>
        public void SetBuffEffects(StatusFlags enable, StatusFlags disable)
        {
            _buffEnable = enable;
            _buffDisable = disable;
            Recompute();
        }

        private void Recompute()
        {
            // Base layer: non-capability bits as-is, plus each default-ON capability iff it has no active
            // disable-hold. Then the buff layer (enable overrides disable, already resolved by the caller).
            StatusFlags b = _nonCapabilityBase;
            if (_disableCanMove == 0) b |= StatusFlags.CanMove;
            if (_disableCanAttack == 0) b |= StatusFlags.CanAttack;
            if (_disableCanCast == 0) b |= StatusFlags.CanCast;
            if (_disableCanMoveEver == 0) b |= StatusFlags.CanMoveEver;
            // Enable-holds: default-OFF, set iff a hold is active (buff layer below can still add its own).
            foreach (var flag in EnableHoldFlags)
            {
                if (_enableHolds.TryGetValue(flag, out int c) && c > 0) b |= flag;
            }
            Status = (b & ~_buffDisable) | _buffEnable;
        }

        // enable=true releases one hold (clamped at 0 — LOAD-BEARING: capabilities are enabled before any
        // disable exists, so an over-release must stay at 0 and leave the capability enabled). enable=false
        // adds a hold. The capability is enabled iff its counter is 0.
        private static int RefHold(int count, bool enable)
        {
            if (enable)
            {
                return count > 0 ? count - 1 : 0;
            }
            return count + 1;
        }

        // ENABLE-polarity ref-count (mirror of RefHold, opposite direction): enable=true adds a hold,
        // enable=false releases one (clamped at 0 so an over-release stays OFF). The state is ON iff the
        // counter is > 0. Matches Riot's SetDodgePiercing ref-count (count += newState ? 1 : -1).
        private static int RefEnableHold(int count, bool enable)
        {
            if (enable)
            {
                return count + 1;
            }
            return count > 0 ? count - 1 : 0;
        }
    }
}
