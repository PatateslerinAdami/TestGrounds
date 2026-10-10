using LeagueSandbox.GameServer.GameObjects.StatsNS;
using GameServerCore.Enums;
using System.Numerics;
using System.Collections.Generic;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class Minion : ObjAIBase
    {
        private readonly Dictionary<int, (AttackableUnit Target, float Speed)> _clientAttackLoops = new Dictionary<int, (AttackableUnit, float)>();

        public bool UsesClientAttackLoop { get; set; }

        public bool ShouldNotifyClientAttack(int userId, AttackableUnit target, bool isCrit)
        {
            if (!UsesClientAttackLoop || !IsMelee)
            {
                return true;
            }

            float speed = Stats.AttackSpeedMultiplier.Total;
            bool repeating = _clientAttackLoops.TryGetValue(userId, out var loop)
                && loop.Target == target
                && loop.Speed == speed;
            _clientAttackLoops[userId] = (target, speed);
            return !repeating || isCrit;
        }

        public void ResetClientAttackLoop(int userId = -1)
        {
            if (userId < 0)
            {
                _clientAttackLoops?.Clear();
            }
            else
            {
                _clientAttackLoops?.Remove(userId);
            }
        }

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

        /// <summary>
        /// Optional native spawn visibility size, separate from the unit's sight radius.
        /// </summary>
        public float? VisibilitySize { get; set; }

        //TODO: Implement these variables
        public int DamageBonus { get; protected set; }
        public int HealthBonus { get; protected set; }
        public int InitialLevel { get; protected set; }
        public bool DisableFoW { get; set; } = false;

        public override bool IsAffectedByFoW => !DisableFoW;

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
            bool enableScripts = true,
            Vector3 direction = default
        ) : base(game, model, name, 40, position, 1100, skinId, netId, team, stats, AIScript, enableScripts)
        {
            if (direction != default)
            {
                Direction = direction;
            }

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
        public override bool SetWaypoints(List<Vector2> newWaypoints, bool isForced = false)
        {
            bool wasMoving = !IsPathEnded();
            bool success = base.SetWaypoints(newWaypoints, isForced);

            if (success && newWaypoints.Count > 0 && newWaypoints[newWaypoints.Count - 1] != Position)
            {
                ResetClientAttackLoop();
            }

            if (success && !IsAttacking && (!UsesClientAttackLoop || !wasMoving))
            {
                _game.PacketNotifier.NotifyNPC_InstantStop_Attack(this, false, false, false, true, true, 0);
            }

            return success;
        }




    }
}