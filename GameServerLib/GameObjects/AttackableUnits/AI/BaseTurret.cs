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
        // Turrets don't auto-provide vision; like Riot, a turret's sight is an explicit
        // perception-bubble Region (added in the turret char script). Avoids double-providing
        // and keeps the radius/flags in one place. NOTE: until a turret char script adds that
        // Region, the turret grants no team vision.
        public override bool AutoProvidesVision => false;

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

            // Replay-verified (4.20, 16 OnTurretDie events): assists = champions with an active
            // assist marker on the turret, the killer is never included, GoldGiven is always 0.
            var assists = GetEnemyChampionAssists(data.Killer);
            var announce = new OnTurretDie
            {
                AssistCount = assists.Count,
                GoldGiven = 0.0f,
                OtherNetID = data.Killer.NetId
            };
            for (int i = 0; i < assists.Count && i < announce.Assists.Length; i++)
            {
                announce.Assists[i] = assists[i].NetId;
            }
            _game.PacketNotifier.NotifyOnEvent(announce, this);

            base.Die(data);
        }

        /// <summary>
        /// Riot's obj_AI_Turret::Create adjustNaviMesh parameter: map-placed turrets bake their
        /// footprint into the nav grid (AITurret.cpp:309 `if (mAdjustNaviMesh)`), script-spawned
        /// turrets (Azir) don't.
        /// </summary>
        protected virtual bool AdjustsNavMesh => true;

        public override void OnRemoved()
        {
            // Riot freezes the turret nav bake on death: obj_AI_Turret::RemoveFromNavGrid gates
            // its flag-clear on !IsDead() (AITurret.cpp:629-637), so a destroyed turret's rubble
            // keeps blocking pathing permanently. The CLIENT runs the same code in its own
            // movement sim — unbaking server-side would let server paths cut through rubble the
            // client can't follow (desync/snap at dead towers). Non-death removals still unbake.
            if (!IsDead)
            {
                UnbakeFootprint();
            }
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
            
            if (AdjustsNavMesh)
            {
                // Pathing half of Riot's turret bake: obj_AI_Turret::OnCreate writes
                // NOT_PASSABLE|SEE_THROUGH over a 140u disc (AITurret.cpp:310
                // SetFlagInRadius(pos, 140, 0x46)) — 140 is the Riot literal, deliberately larger
                // than the ~88u pathfinding radius because the A* is cell-based with no per-unit
                // radius corridor: the bake is pre-inflated by expected unit clearance, exactly
                // like the inhib/nexus footprints. Baking only PathfindingRadius let unit bodies
                // clip the turret base.
                _blockedNavCells = _game.Map.NavigationGrid.AddDynamicBlocker(Position, 140f);

                // Vision half of the same bake: mark the turret's own emplacement structure
                // SEE_THROUGH so it never occludes line of sight (the real turret sees over its
                // own base). Fixes the nexus-turret-only vision flicker — its baked base blob sits
                // ~110u out, past the LoS-ray start offset, and was grazing the turret↔enemy ray.
                _game.Map.NavigationGrid.MarkSeeThroughInRadius(Position, 140f);
            }

            // Perception bubble (vision 800 + true sight) moved to the turret CHAR SCRIPTS
            // (Content/Characters/Turrets/TurretCharScripts.cs) — Riot's model: the S1/4.20 turret
            // char scripts create it (BubbleSize = 800 in CharOnActivate), not the engine. An
            // eventual AzirTurret implementation gets its bubble the same way via its own script.
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
