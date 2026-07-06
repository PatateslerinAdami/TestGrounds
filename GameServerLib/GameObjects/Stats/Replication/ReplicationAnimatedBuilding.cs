using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.Buildings.AnimatedBuildings;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    public class ReplicationAnimatedBuilding : Replication
    {
        public ReplicationAnimatedBuilding(ObjAnimatedBuilding owner) : base(owner)
        {

        }
        public override void Update()
        {
            UpdateFloat(Stats.CurrentHealth, ReplicationBucket.Local1, 0); //mHP
            UpdateBool(Stats.IsInvulnerable, ReplicationBucket.Local1, 1); //IsInvulnerable
            UpdateBool(Stats.IsTargetable, ReplicationBucket.Global, 0); //mIsTargetable
            UpdateUint((uint)Stats.IsTargetableToTeam, ReplicationBucket.Global, 1); //mIsTargetableToTeamFlags
        }
    }
}
