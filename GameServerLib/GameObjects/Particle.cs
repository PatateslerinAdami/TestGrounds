using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects
{
    /// <summary>
    /// Class used for all in-game visual effects meant to be explicitly networked by the server (never spawned client-side).
    /// </summary>
    public class Particle : GameObject
    {
        private float _currentTime;

        private static string NormalizeParticleName(string particleName)
        {
            if (string.IsNullOrEmpty(particleName))
            {
                return string.Empty;
            }

            return particleName.Contains(".troy") ? particleName : $"{particleName}.troy";
        }

        /// <summary>
        /// Creator of this particle.
        /// </summary>
        public GameObject Caster { get; }

        /// <summary>
        /// Primary bind target.
        /// </summary>
        public GameObject BindObject { get; }

        /// <summary>
        /// Client-sided, internal name of the particle used in networking, usually always ends in .troy.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Client-sided particle name used when displayed to enemies.
        /// </summary>
        public string NameForEnemies { get; }

        /// <summary>
        /// Secondary bind target. Null when not attached to anything.
        /// </summary>
        public GameObject TargetObject { get; }

        /// <summary>
        /// Position this object is spawned at.
        /// </summary>
        public Vector2 StartPosition { get; private set; }

        /// <summary>
        /// Position this object is aimed at and/or moving towards.
        /// </summary>
        public Vector2 EndPosition { get; private set; }

        /// <summary>
        /// Client-sided, internal name of the bone that this particle should be attached to on the owner, for networking.
        /// </summary>
        public string BoneName { get; }

        /// <summary>
        /// Client-sided, internal name of the bone that this particle should be attached to on the target, for networking.
        /// </summary>
        public string TargetBoneName { get; }

        /// <summary>
        /// Scale of the particle used in networking.
        /// </summary>
        public float Scale { get; }

        /// <summary>
        /// Total game-time that this particle should exist for.
        /// </summary>
        public float Lifetime { get; }

        /// <summary>
        /// The only team that should be able to see this particle.
        /// </summary>
        public TeamId SpecificTeam { get; }

        /// <summary>
        /// The only unit that should be able to see this particle.
        /// Only effective if this is a player controlled unit.
        /// </summary>
        public GameObject SpecificUnit { get; }

        /// <summary>
        /// Whether or not the particle should be titled along the ground towards its end position.
        /// </summary>
        public bool FollowsGroundTilt { get; }

        /// <summary>
        /// Flags which determine how the particle behaves. Values unknown.
        /// </summary>
        public FXFlags Flags { get; }

        public override bool IsAffectedByFoW => true;
        public override bool SpawnShouldBeHidden => true;
        public bool isInfinite = false;
        public bool IgnoreCasterVisibility { get; }
        public float OverrideTargetHeight { get; }

        public Particle(Game game, GameObject caster, GameObject bindObj, GameObject target, string particleName,
            float scale = 1.0f, string boneName = "", string targetBoneName = "", uint netId = 0,
            Vector3 direction = new Vector3(), bool followGroundTilt = false, float lifetime = 0,
            TeamId teamOnly = TeamId.TEAM_ALL, GameObject unitOnly = null,
            FXFlags flags = FXFlags.GivenDirection, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null)
            : base(game, target.Position, 0, 0, 0, netId, teamOnly)
        {
            Caster = caster;
            BindObject = bindObj;
            TargetObject = target;
            StartPosition = TargetObject.Position;
            BoneName = boneName;
            TargetBoneName = targetBoneName;
            Scale = scale;
            Direction = direction;
            Lifetime = lifetime;
            SpecificTeam = teamOnly;
            SpecificUnit = unitOnly;
            FollowsGroundTilt = followGroundTilt;
            Flags = flags;
            IgnoreCasterVisibility = ignoreCasterVisibility;
            OverrideTargetHeight = overrideTargetHeight;

            if (bindObj != null)
            {
                Team = bindObj.Team;
            }
            else if (caster != null)
            {
                Team = caster.Team;
            }

            Name = NormalizeParticleName(particleName);
            NameForEnemies = string.IsNullOrEmpty(enemyParticle) ? Name : NormalizeParticleName(enemyParticle);

            _game.ObjectManager.AddObject(this);
        }

        public Particle(Game game, GameObject caster, GameObject bindObj, Vector2 targetPos, string particleName,
            float scale = 1.0f, string boneName = "", string targetBoneName = "", uint netId = 0,
            Vector3 direction = new Vector3(), bool followGroundTilt = false, float lifetime = 0,
            TeamId teamOnly = TeamId.TEAM_ALL, GameObject unitOnly = null,
            FXFlags flags = FXFlags.GivenDirection, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null)
            : base(game, targetPos, 0, 0, 0, netId, teamOnly)
        {
            Caster = caster;

            BindObject = bindObj;
            if (BindObject != null)
            {
                Position = BindObject.Position;
            }

            TargetObject = null;
            StartPosition = targetPos;
            BoneName = boneName;
            TargetBoneName = targetBoneName;
            Scale = scale;
            Direction = direction;
            Lifetime = lifetime;
            SpecificTeam = teamOnly;
            SpecificUnit = unitOnly;
            FollowsGroundTilt = followGroundTilt;
            Flags = flags;
            IgnoreCasterVisibility = ignoreCasterVisibility;
            OverrideTargetHeight = overrideTargetHeight;

            if (bindObj != null)
            {
                Team = bindObj.Team;
            }
            else if (caster != null)
            {
                Team = caster.Team;
            }

            Name = NormalizeParticleName(particleName);
            NameForEnemies = string.IsNullOrEmpty(enemyParticle) ? Name : NormalizeParticleName(enemyParticle);

            _game.ObjectManager.AddObject(this);
        }

        public Particle(Game game, GameObject caster, Vector2 startPos, Vector2 endPos, string particleName,
            float scale = 1.0f, string boneName = "", string targetBoneName = "", uint netId = 0,
            Vector3 direction = new Vector3(), bool followGroundTilt = false, float lifetime = 0,
            TeamId teamOnly = TeamId.TEAM_ALL, GameObject unitOnly = null,
            FXFlags flags = FXFlags.GivenDirection, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null)
            : base(game, startPos, 0, 0, 0, netId, teamOnly)
        {
            Caster = caster;

            BindObject = null;
            TargetObject = null;
            StartPosition = startPos;
            EndPosition = endPos;
            BoneName = boneName;
            TargetBoneName = targetBoneName;
            Scale = scale;
            Direction = direction;
            Lifetime = lifetime;
            SpecificTeam = teamOnly;
            SpecificUnit = unitOnly;
            FollowsGroundTilt = followGroundTilt;
            Flags = flags;
            IgnoreCasterVisibility = ignoreCasterVisibility;
            OverrideTargetHeight = overrideTargetHeight;

            if (caster != null)
            {
                Team = caster.Team;
            }

            Name = NormalizeParticleName(particleName);
            NameForEnemies = string.IsNullOrEmpty(enemyParticle) ? Name : NormalizeParticleName(enemyParticle);

            _game.ObjectManager.AddObject(this);
        }

        public string GetEffectNameForTeam(TeamId team)
        {
            return Team == TeamId.TEAM_ALL || team == Team ? Name : NameForEnemies;
        }

        /// <summary>
        /// Returns whether this particle is allowed to be seen by recipients on the given team,
        /// ignoring FoW/line-of-sight checks.
        /// </summary>
        public bool IsAudienceVisibleToTeam(TeamId team)
        {
            if (SpecificTeam != TeamId.TEAM_ALL && team != SpecificTeam)
            {
                return false;
            }

            // Team-level checks cannot satisfy unit-only particles.
            return SpecificUnit == null;
        }

        /// <summary>
        /// Returns whether this particle is allowed to be seen by a specific player recipient,
        /// ignoring FoW/line-of-sight checks.
        /// </summary>
        public bool IsAudienceVisibleToRecipient(TeamId team, int userId)
        {
            if (SpecificTeam != TeamId.TEAM_ALL && team != SpecificTeam)
            {
                return false;
            }

            if (SpecificUnit is Champion champion)
            {
                return champion.ClientId == userId;
            }

            return SpecificUnit == null;
        }

        /// <summary>
        /// Returns whether this particle should be considered immediately visible for an observer team
        /// before distance/line-of-sight checks.
        /// </summary>
        public bool ShouldAutoRevealForObserverTeam(TeamId observerTeam)
        {
            if (!IsAudienceVisibleToTeam(observerTeam))
            {
                return false;
            }

            return SpecificTeam == TeamId.TEAM_ALL
                && (Team == TeamId.TEAM_ALL || observerTeam == Team);
        }

        public override void Update(float diff)
        {
            _currentTime += diff / 1000.0f;
            if (_currentTime >= Lifetime && Lifetime >= 0 && !isInfinite)
            {
                SetToRemove();
            }
        }

        public float GetTimeAlive()
        {
            return _currentTime;
        }

        public override void SetToRemove()
        {
            if (!IsToRemove())
            {
                base.SetToRemove();
                _game.PacketNotifier.NotifyFXKill(this);
            }
        }
    }
}
