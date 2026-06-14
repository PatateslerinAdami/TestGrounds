using GameServerCore.Enums;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits
{
    public class ForceMovementParameters
    {
        /// <summary>
        /// Name of the movement, useful for identifying specific dashes in events.
        /// </summary>
        public string MovementName { get; set; } = "";
        /// <summary>
        /// The unit that caused this forced movement.
        /// </summary>
        public AttackableUnit Caster { get; set; }
        /// <summary>
        /// Status flags which are disabled while dashing.
        /// </summary>
        public StatusFlags SetStatus { get; set; } = StatusFlags.CanAttack | StatusFlags.CanCast | StatusFlags.CanMove;
        /// <summary>
        /// Amount of time passed since the unit started dashing.
        /// </summary>
        public float ElapsedTime { get; set; }
        /// <summary>
        /// The distance traveled from the beginning of the dash.
        /// </summary>
        public float PassedDistance { get; set; }
        /// <summary>
        /// Speed to use for the movement.
        /// </summary>
        public float PathSpeedOverride { get; set; }
        /// <summary>
        /// Maximum vertical height.
        /// NOTES: Internally follows the path of a parabola, stretched in the x and y axis by the distance to the destination and stretched in the z axis to the maximum height (this stretching of the z axis scales the vertical speed).
        /// </summary>
        public float ParabolicGravity { get; set; }
        /// <summary>
        /// End position of the movement.
        /// </summary>
        public Vector2 ParabolicStartPoint { get; set; }
        /// <summary>
        /// Whether or not the unit performing the movement should face the direction it had before starting the movement.
        /// </summary>
        public bool KeepFacingDirection { get; set; }
        /// <summary>
        /// What happens to the unit's current order when this forced movement ends (Riot
        /// ForceMovementOrdersType). POSTPONE_CURRENT_ORDER (default) = leave the order intact so the AI
        /// brain / player resumes it after the dash (the legacy behavior). CANCEL_ORDER = the dash
        /// replaced the order, so the unit goes idle (Stop) when the forced movement ends. Previously
        /// accepted at the API boundary but silently dropped — see docs/FORCED_MOVEMENT_REWRITE_PLAN.md P1b.
        /// </summary>
        public ForceMovementOrdersType MovementOrdersType { get; set; } = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER;
        /// <summary>
        /// For POSTPONE_CURRENT_ORDER: the destination of a plain MoveTo order that was active when the
        /// forced movement started, snapshotted at dash-begin because it lives in Waypoints (which the
        /// dash clears). Re-issued when the forced movement ends so the unit resumes walking to it —
        /// mirroring Riot's ORDER_STATUS_POSTPONED re-execute (IssueOrders, the order keeps its position).
        /// Vector2.Zero = none (no MoveTo to resume; AttackTo resumes naturally via the surviving TargetUnit).
        /// </summary>
        public Vector2 PostponedMoveDestination { get; set; } = Vector2.Zero;
        /// <summary>
        /// NetID of the unit to move towards.
        /// </summary>
        public uint FollowNetID { get; set; }
        /// <summary>
        /// Maximum distance to follow the FollowNetID.
        /// </summary>
        public float FollowDistance { get; set; }
        /// <summary>
        /// Distance ahead of the FollowNetID to travel after reaching it.
        /// </summary>
        public float FollowBackDistance { get; set; }
        /// <summary>
        /// Maximum amount of time to follow the FollowNetID.
        /// </summary>
        public float FollowTravelTime { get; set; }
    }
}
