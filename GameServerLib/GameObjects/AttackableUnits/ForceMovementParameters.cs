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


        /// <summary>
        /// Duration of the movement in seconds. If set and PathSpeedOverride is 0, speed will be calculated automatically.
        /// </summary>
        public float Duration { get; set; }
        /// <summary>
        /// Target position for the movement.
        /// </summary>
        public Vector2 TargetPosition { get; set; }
        /// <summary>
        /// Type of force movement.
        /// </summary>
        public ForceMovementType MovementType { get; set; } = ForceMovementType.FURTHEST_WITHIN_RANGE;
        /// <summary>
        /// How the movement affects current orders.
        /// </summary>
        public ForceMovementOrdersType MovementOrdersType { get; set; } = ForceMovementOrdersType.POSTPONE_CURRENT_ORDER;
        /// <summary>
        /// How the movement affects facing direction.
        /// </summary>
        public ForceMovementOrdersFacing MovementOrdersFacing { get; set; } = ForceMovementOrdersFacing.FACE_MOVEMENT_DIRECTION;

        /// <summary>
        /// Animation to play during the movement.
        /// </summary>
        public string Animation { get; set; } = "";
        /// <summary>
        /// If true, overrides the "RUN" animation state. If false, uses PlayAnimation/StopAnimation.
        /// </summary>
        public bool OverrideRunAnimation { get; set; } = false;
        /// <summary>
        /// Flags to pass to PlayAnimation.
        /// </summary>
        public AnimationFlags AnimationFlags { get; set; } = 0;
        /// <summary>
        /// Flags to pass to StopAnimation.
        /// </summary>
        public StopAnimationFlags StopAnimationFlags { get; set; } = StopAnimationFlags.IgnoreLock;
        public bool DoStopAnimation { get; set; } = false;
        /// <summary>
        /// Time scale for the animation.
        /// </summary>
        public float AnimationTimeScale { get; set; } = 1.0f;
        /// <summary>
        /// Start time for the animation.
        /// </summary>
        public float AnimationStartTime { get; set; } = 0.0f;
        /// <summary>
        /// Speed scale for the animation.
        /// </summary>
        public float AnimationSpeedScale { get; set; } = 0.0f;

        /// <summary>
        /// Whether to ignore terrain collision.
        /// </summary>
        public bool IgnoreTerrain { get; set; } = false;
        /// <summary>
        /// Whether this movement makes the unit unstoppable (immune to enemy forced movements and CC).
        /// </summary>
        public bool IsUnstoppable { get; set; } = false;

        public SpellDataFlags SpellDataFlags { get; set; } = 0;
    }
}
namespace GameServerCore.Enums
{
    [Flags]
    public enum StopAnimationFlags
    {
        None = 0,
        Fade = 1,
        IgnoreLock = 2,
        StopAll = 4
    }
}
