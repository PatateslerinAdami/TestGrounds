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

        // Non-capability flags: plain set/clear bitfield (imperative SetStatus). Immovable stays here too —
        // it is default-OFF (enable polarity, opposite to CanX) and has no ref-counted callers.
        private StatusFlags _nonCapabilityBase;

        // Buff-derived layer: OR of every active buff's contribution, rebuilt each RecomputeBuffEffects
        // (a full recompute over the live BuffList — functionally a ref-count for buff sources).
        private StatusFlags _buffEnable;
        private StatusFlags _buffDisable;

        private const StatusFlags CapabilityMask =
            StatusFlags.CanMove | StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMoveEver;

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

            StatusFlags otherBits = status & ~CapabilityMask;
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
    }
}
