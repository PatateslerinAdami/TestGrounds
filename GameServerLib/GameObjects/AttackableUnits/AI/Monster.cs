using System.Numerics;
using GameServerCore.Enums;
using GameServerLib.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.StatsNS;

namespace LeagueSandbox.GameServer.GameObjects
{
    public class Monster : Minion
    {
        public MonsterCamp Camp { get; private set; }
        public string SpawnAnimation { get; private set; }
        /// <summary>
        /// The camp look-at WORLD POINT (Lua CampFacePoints). Transmitted verbatim as
        /// S2C_CreateNeutral.FaceDirectionPosition; the spawn heading (Direction) is
        /// normalize(FacePoint − spawn). Riot AIMinion::Create uses the same facing = FacingPos − Pos.
        /// </summary>
        public Vector3 FacePoint { get; private set; }

        public Monster(
            Game game,
            string name,
            string model,
            Vector2 position,
            Vector3 faceDirection,
            MonsterCamp monsterCamp,
            TeamId team = TeamId.TEAM_NEUTRAL,
            uint netId = 0,
            string spawnAnimation = "",
            bool isTargetable = true,
            bool ignoresCollision = false,
            Stats stats = null,
            string aiScript = "",
            int damageBonus = 0,
            int healthBonus = 0,
            int initialLevel = 1
        ) : base
            (
                game, null, position, model, name,
                netId, team, 0, ignoresCollision, isTargetable,
                null, stats, aiScript, damageBonus, healthBonus, initialLevel
            )
        {
            Camp = monsterCamp;
            Team = team;
            SpawnAnimation = spawnAnimation;
            // faceDirection is the camp look-at WORLD POINT. Store it for the spawn packet and face the
            // heading toward it (normalize(point − spawn)).
            FacePoint = faceDirection;
            var heading = Vector2.Normalize(new Vector2(faceDirection.X, faceDirection.Z) - position);
            if (!float.IsNaN(heading.X))
            {
                FaceDirection(new Vector3(heading.X, 0f, heading.Y));
            }
            IsTargetable = isTargetable;
            IgnoresCollision = ignoresCollision;
        }

        public void UpdateInitialLevel(int level)
        {
            InitialLevel = level;
        }
    }
}

