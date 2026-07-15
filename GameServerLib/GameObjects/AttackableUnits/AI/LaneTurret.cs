using System.Numerics;
using GameServerCore.Domain;
using GameServerCore.Enums;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    public class LaneTurret : BaseTurret
    {
        public TurretType Type { get; }

        public LaneTurret(
            Game game,
            string name,
            string model,
            Vector2 position,
            TeamId team = TeamId.TEAM_BLUE,
            TurretType type = TurretType.OUTER_TURRET,
            uint netId = 0,
            Lane lane = Lane.LANE_Unknown,
            MapObject mapObject = default,
            Stats stats = null,
            string aiScript = ""
        ) : base(game, name, model, position, team, netId, lane, mapObject, stats: stats, aiScript: aiScript)
        {
            SetStatus(StatusFlags.CanMove, false);
            SetStatus(StatusFlags.CanMoveEver, false);
            Type = type;

            if (type == TurretType.FOUNTAIN_TURRET)
            {
                SetIsTargetableToTeam(TeamId.TEAM_BLUE, false);
                SetIsTargetableToTeam(TeamId.TEAM_PURPLE, false);
            }
        }

        // Riot-verified answer to the old "map-script events or leave here?"
        // Riot's Map1 LevelScript.lua HandleDestroyedObject (lua-unluac-420) contains ZERO reward
        // logic for structure deaths — it only drives the state machine (invulnerability chain,
        // barracks disable, ceremony). The gold/exp values come from chardata
        // (Local/GlobalGoldGivenOnDeath, exactly what this method reads) and their distribution is
        // server-engine-side, matching this placement.
        public override void Die(DeathData data)
        {
            float localGold = CharData.LocalGoldGivenOnDeath;
            float globalGold = CharData.GlobalGoldGivenOnDeath;
            float globalEXP = CharData.GlobalExpGivenOnDeath;

            // Wire-verified turret gold (replay UnitAddGold forensics, e.g. outer turret
            // Local=150/Global=100, inner Local=100/Global=125 — exactly the chardata values):
            // - LOCAL gold is a PROXIMITY share: LocalGoldGivenOnDeath / nearbyEnemyCount, sent
            //   with SourceNetID = the TURRET. 
            //   ( for 4.20: assist markers only drive the OnTurretDie announce; the wire shares are
            //   clean divisions of the local value.)
            // - GLOBAL gold goes to every enemy player with SourceNetID = 0 (null source).
            // The divisor counts ENEMY champions only (the old code divided by BOTH teams' nearby
            // champions, silently losing gold whenever the turret's own team stood in range).
            var championsInRange = _game.ObjectManager.GetChampionsInRange(Position, Stats.Range.Total * 1.5f, true);
            var enemiesInRange = championsInRange.FindAll(c => c.Team != Team);

            if (localGold > 0 && enemiesInRange.Count > 0)
            {
                float goldShare = localGold / enemiesInRange.Count;
                foreach (var champion in enemiesInRange)
                {
                    champion.AddGold(this, goldShare);
                }
            }

            foreach (var player in _game.PlayerManager.GetPlayers(true))
            {
                var champion = player.Champion;
                if (player.Team != Team)
                {
                    champion.AddGold(null, globalGold);
                    champion.AddExperience(globalEXP);
                }
            }

            base.Die(data);
        }

        public override void AutoAttackHit(AttackableUnit target, HitResult? wireHitResult = null)
        {
            if (Type == TurretType.FOUNTAIN_TURRET)
            {
                target.TakeDamage(this, 1000, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_ATTACK, false);
            }
            else
            {
                base.AutoAttackHit(target, wireHitResult);
            }
        }
    }
}
