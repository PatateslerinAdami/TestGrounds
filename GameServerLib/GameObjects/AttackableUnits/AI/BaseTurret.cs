using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Force.Crc32;
using GameServerCore.Domain;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    /// <summary>
    /// Base class for Turret GameObjects.
    /// In League, turrets are separated into visual and AI objects, so this GameObject represents the AI portion,
    /// while the visual object is handled automatically by clients via packets.
    /// </summary>
    public class BaseTurret : ObjAIBase
    {
        /// <summary>
        /// Current lane this turret belongs to.
        /// </summary>
        public Lane Lane { get; private set; }
        /// <summary>
        /// MapObject that this turret was created from.
        /// </summary>
        public MapObject ParentObject { get; private set; }
        /// <summary>
        /// Supposed to be the NetID for the visual counterpart of this turret. Used only for packets.
        /// </summary>
        public uint ParentNetId { get; private set; }
        /// <summary>
        /// Region assigned to this turret for vision and collision.
        /// </summary>
        public Region BubbleRegion { get; private set; }

        public override bool IsAffectedByFoW => false;

        // Cells in the navgrid this turret currently blocks. Tracked so we can release exactly the
        // cells we acquired when the turret dies or is removed (ref counted in the grid).
        private List<int> _blockedNavCells;

        public BaseTurret(
            Game game,
            string name,
            string model,
            Vector2 position,
            TeamId team = TeamId.TEAM_BLUE,
            uint netId = 0,
            Lane lane = Lane.LANE_Unknown,
            MapObject mapObject = default,
            int skinId = 0,
            Stats stats = null,
            string aiScript = ""
        ) : base(game, model, name, position: position, visionRadius: 800, skinId: skinId, netId: netId, team: team, stats: stats, aiScript: aiScript)
        {
            ParentNetId = Crc32Algorithm.Compute(Encoding.UTF8.GetBytes(name)) | 0xFF000000;
            Lane = lane;
            ParentObject = mapObject;
            SetTeam(team);
            Replication = new ReplicationAITurret(this);
        }



        /// <summary>
        /// Called when this unit dies.
        /// </summary>
        /// <param name="killer">Unit that killed this unit.</param>
        public override void Die(DeathData data)
        {
            UnbakeFootprint();

            var announce = new OnTurretDie
            {
                AssistCount = 0,
                GoldGiven = 0.0f,
                OtherNetID = data.Killer.NetId
            };
            _game.PacketNotifier.NotifyOnEvent(announce, this);

            base.Die(data);
        }

        public override void OnRemoved()
        {
            UnbakeFootprint();
            base.OnRemoved();
        }

        private void UnbakeFootprint()
        {
            if (_blockedNavCells != null)
            {
                _game.Map.NavigationGrid.RemoveDynamicBlocker(_blockedNavCells);
                _blockedNavCells = null;
            }
        }

        /// <summary>
        /// Function called when this GameObject has been added to ObjectManager.
        /// </summary>
        public override void OnAdded()
        {
            base.OnAdded();
            _game.ObjectManager.AddTurret(this);

            // Bake the turret footprint into the navgrid. Use the real mesh polygon from the
            // .sco.json (via ParentObject.Vertices2D) when available; fall back to a circle of
            // PathfindingRadius for turrets created without a MapObject (e.g. AzirTurret).
            if (ParentObject.Vertices2D != null && ParentObject.Vertices2D.Length >= 3)
            {
                _blockedNavCells = _game.Map.NavigationGrid.AddDynamicBlocker(ParentObject.Vertices2D);
            }
            else
            {
                _blockedNavCells = _game.Map.NavigationGrid.AddDynamicBlocker(Position, PathfindingRadius);
            }

            // TODO: Handle this via map script for LaneTurret and via CharScript for AzirTurret.
            BubbleRegion = new Region
            (
                _game, Team, Position,
                RegionType.Unknown2,
                collisionUnit: this,
                visionTarget: null,
                visionRadius: 800f,
                revealStealth: true,
                collisionRadius: PathfindingRadius,
                lifetime: 25000.0f
            );
        }

        public override void OnCollision(GameObject collider, bool isTerrain = false)
        {
            // TODO: Verify if we need this for things like SionR.
        }

        /// <summary>
        /// Overridden function unused by turrets.
        /// </summary>
        public override void RefreshWaypoints(float idealRange)
        {
        }

        /// <summary>
        /// Sets this turret's Lane to the specified Lane.
        /// Only sets if its current Lane is NONE.
        /// Used for ObjectManager.
        /// </summary>
        /// <param name="newId"></param>
        public void SetLaneID(Lane newId)
        {
            Lane = newId;
        }
    }
}
