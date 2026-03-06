using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerCore.Enums;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class Minion : ObjAIBase
    {
        /// <summary>
        /// Unit which spawned this minion.
        /// </summary>
        public ObjAIBase Owner { get; }
        /// <summary>
        /// Whether or not this minion should ignore collisions.
        /// </summary>
        public bool IgnoresCollision { get; protected set; }
        /// <summary>
        /// Whether or not this minion is considered a ward.
        /// </summary>
        public bool IsWard { get; protected set; }
        /// <summary>
        /// Whether or not this minion is a LaneMinion.
        /// </summary>
        public bool IsLaneMinion { get; protected set; }
        /// <summary>
        /// Whether or not this minion is targetable at all.
        /// </summary>
        public bool IsTargetable { get; protected set; }
        /// <summary>
        /// Only unit which is allowed to see this minion.
        /// </summary>
        public ObjAIBase VisibilityOwner { get; }

        //TODO: Implement these variables
        public int DamageBonus { get; protected set; }
        public int HealthBonus { get; protected set; }
        public int InitialLevel { get; protected set; }

        public Minion(
            Game game,
            ObjAIBase owner,
            Vector2 position,
            string model,
            string name,
            uint netId = 0,
            TeamId team = TeamId.TEAM_NEUTRAL,
            int skinId = 0,
            bool ignoreCollision = false,
            bool targetable = true,
            bool isWard = false,
            ObjAIBase visibilityOwner = null,
            Stats stats = null,
            string AIScript = "",
            int damageBonus = 0,
            int healthBonus = 0,
            int initialLevel = 1,
            bool enableScripts = true
        ) : base(game, model, name, 40, position, 1100, skinId, netId, team, stats, AIScript, enableScripts)
        {
            Owner = owner;

            IsLaneMinion = false;
            IsWard = isWard;
            IgnoresCollision = ignoreCollision;
            if (IgnoresCollision)
            {
                SetStatus(StatusFlags.Ghosted, true);
            }

            IsTargetable = targetable;
            if (!IsTargetable)
            {
                SetStatus(StatusFlags.Targetable, false);
            }

            VisibilityOwner = visibilityOwner;
            DamageBonus = damageBonus;
            HealthBonus = healthBonus;
            InitialLevel = initialLevel;
            MoveOrder = OrderType.Stop;

            Replication = new ReplicationMinion(this);
        }

        public override void OnCollision(GameObject collider, bool isTerrain = false)
        {
            base.OnCollision(collider, isTerrain);
            if (isTerrain) return;

            if (IsDead || MovementParameters != null || Status.HasFlag(StatusFlags.Ghosted)) return;

            if (collider is AttackableUnit otherUnit)
            {
                if (otherUnit.MovementParameters != null || otherUnit.Status.HasFlag(StatusFlags.Ghosted)) return;

                if (IsAttacking || GetCastSpell() != null || ChannelSpell != null || MoveOrder == OrderType.Hold || MoveOrder == OrderType.Stop)
                {
                    return;
                }

                bool otherIsHuman = otherUnit is Champion && (otherUnit as ObjAIBase)?.IsBot == false;

                bool otherIsBusyAI = false;
                if (otherUnit is ObjAIBase otherAI)
                {
                    otherIsBusyAI = otherAI.IsAttacking || otherAI.GetCastSpell() != null || otherAI.ChannelSpell != null || otherAI.MoveOrder == OrderType.Hold || otherAI.MoveOrder == OrderType.Stop;
                }

                if (collider.Position != Position)
                {
                    Vector2 toCollider = collider.Position - Position;
                    float distSq = toCollider.LengthSquared();
                    float combinedRadius = CollisionRadius + otherUnit.CollisionRadius;

                    if (distSq < (combinedRadius * combinedRadius) && distSq > 0.0001f)
                    {
                        float distance = (float)Math.Sqrt(distSq);
                        float overlap = combinedRadius - distance;

                        Vector2 pushDirection = -(toCollider / distance);

                        Vector2 myDir = Waypoints.Count > CurrentWaypointKey ? (Waypoints[CurrentWaypointKey] - Position) : Vector2.Zero;
                        if (myDir != Vector2.Zero) myDir = Vector2.Normalize(myDir); 

                        Vector2 otherDir = otherUnit.Waypoints.Count > otherUnit.CurrentWaypointKey ? (otherUnit.Waypoints[otherUnit.CurrentWaypointKey] - otherUnit.Position) : Vector2.Zero;
                        if (otherDir != Vector2.Zero) otherDir = Vector2.Normalize(otherDir);

                        bool headingSameWay = Vector2.Dot(myDir, otherDir) > 0.7f;

                        if (headingSameWay && !otherIsBusyAI && !otherIsHuman)
                        {
                            Vector2 tangentRight = new Vector2(-myDir.Y, myDir.X);
                            Vector2 tangentLeft = new Vector2(myDir.Y, -myDir.X);

                            pushDirection = Vector2.Dot(pushDirection, tangentRight) > 0 ? tangentRight : tangentLeft;
                            overlap *= 0.2f;
                        }
                        else
                        {
                            overlap *= (otherIsBusyAI || otherIsHuman) ? 1.0f : 0.5f;
                        }
                        overlap = Math.Min(overlap, 15.0f);

                        Vector2 newPos = Position + (pushDirection * overlap);

                        if (_game.Map.PathingHandler.IsWalkable(newPos, PathfindingRadius))
                        {
                            SetPosition(newPos, false);
                        }
                    }
                }
            }
        }
    }
}