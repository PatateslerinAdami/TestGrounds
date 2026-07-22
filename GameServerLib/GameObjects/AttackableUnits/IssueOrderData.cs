using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.SpellNS;

namespace GameServerLib.GameObjects.AttackableUnits
{
    /// <summary>
    /// Payload for the vetoable <c>OnHandleOnIssueOrder</c> event. Mirrors Riot's buff-script hook
    /// <c>BuffScriptInstance::HandleOnIssueOrder(obj_AI_Base*, orders_e, const r3dPoint3D&,
    /// AttackableUnit*, const Spell::SpellCastInfo*)</c> (BuffScript.h:65) as a single data object; the
    /// event is a ConditionDispatcher, so a listener returning false rejects the order — matching the
    /// hook's bool return.
    /// </summary>
    public class IssueOrderData
    {
        /// <summary>The order being issued (Riot <c>orders_e</c>).</summary>
        public OrderType Order { get; set; }

        /// <summary>
        /// Target point of the order (Riot <c>r3dPoint3D</c>). Best-effort at the publish site: the
        /// target unit's position for a unit-targeted order, otherwise the unit's move destination.
        /// </summary>
        public Vector2 TargetPosition { get; set; }

        /// <summary>
        /// Target unit of the order, or null for a positional / no-target order (Riot <c>AttackableUnit*</c>).
        /// </summary>
        public AttackableUnit TargetUnit { get; set; }

        /// <summary>
        /// Cast info when the order is a spell cast (Riot <c>Spell::SpellCastInfo*</c>), otherwise null.
        /// Our per-cast info type is <see cref="CastInfo"/> (the pending <c>SpellToCast.CastInfo</c>).
        /// </summary>
        public CastInfo CastInfo { get; set; }
    }
}
