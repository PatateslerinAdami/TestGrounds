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
        /// <summary>
        /// When set (non-null), overrides the wire's <c>KeywordNetID</c> field at
        /// packet-build time. Default behavior writes <c>KeywordNetID = Caster.NetId</c>
        /// when <see cref="Caster"/> is set. Some replay-verified FX (Vel'Koz R's
        /// <c>beam_end</c>) write KeywordNetID = 0 even with a caster present.
        /// Use <c>0</c> to force the wire field to 0 explicitly.
        /// </summary>
        public uint? KeywordNetIDOverride { get; set; }

        /// <summary>
        /// Overrides the FX_Create_Group PackageHash (default = caster's object hash). Some effects resolve
        /// their embedded sound bank by the particle's own package hash, not the caster's — e.g. the TT altar
        /// chains (TT_Lock) whose lock sound only plays with the wire-exact package hash.
        /// </summary>
        public uint? PackageHashOverride { get; set; }

        /// <summary>
        /// When true this particle is sent as its OWN FX_Create_Group packet instead of being bundled with
        /// other same-tick particles. The client only plays one embedded sound per FX_Create_Group packet, so
        /// any sound-carrying effect (e.g. the TT altar TT_Lock lock sound, the spirit VO) must not share a
        /// packet with another sound effect — Riot sends each as its own packet.
        /// </summary>
        public bool SendUnbatched { get; set; }

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
        /// A unit that must NOT see this particle (Riot's BBSpellEffectCreate SpecificUnitToExclude).
        /// Inverse of <see cref="SpecificUnit"/>: every other valid recipient still sees it, subject
        /// to normal fog-of-war. Server-side audience filter only — not a wire field.
        /// </summary>
        public GameObject SpecificUnitExclude { get; }

        /// <summary>
        /// Riot's BBSpellEffectCreate FOWTeam value (which team's fog-of-war gates this FX;
        /// TEAM_NEUTRAL = not gated). We currently act on it only as the on/off
        /// <see cref="IsAffectedByFoW"/> toggle (gated == FOWTeam != TEAM_NEUTRAL); the raw team is
        /// captured here for fidelity / future per-team gating. Defaults to TEAM_NEUTRAL.
        /// </summary>
        public TeamId FOWTeam { get; set; } = TeamId.TEAM_NEUTRAL;

        /// <summary>
        /// Whether or not the particle should be titled along the ground towards its end position.
        /// </summary>
        public bool FollowsGroundTilt { get; }

        /// <summary>
        /// Flags which determine how the particle behaves. Values unknown.
        /// </summary>
        public FXFlags Flags { get; }

        // Riot's BBSpellEffectCreate FOWTeam: a real team => the FX is fog-of-war gated (only
        // shown to recipients with vision of it); TEAM_NEUTRAL => not gated (always shown).
        // We model that as this toggle. Defaults true (every particle was FoW-gated before).
        private readonly bool _affectedByFoW;
        public override bool IsAffectedByFoW => _affectedByFoW;
        public override bool SpawnShouldBeHidden => true;
        // Riot's BBSpellEffectCreate SendIfOnScreenOrDiscard: when true the FX is a one-shot —
        // sent only to recipients who can see it AT CREATION; no later fog-of-war re-entry
        // resend (if you weren't looking, you missed it). False (default) = persistent: the
        // engine resends the create whenever the particle re-enters a team's vision.
        public bool SendIfOnScreenOrDiscard { get; }
        public bool isInfinite = false;
        public bool IgnoreCasterVisibility { get; }
        public float OverrideTargetHeight { get; }

        public Particle(Game game, GameObject caster, GameObject bindObj, GameObject target, string particleName,
            float scale = 1.0f, string boneName = "", string targetBoneName = "", uint netId = 0,
            Vector3 direction = new Vector3(), bool followGroundTilt = false, float lifetime = 0,
            TeamId teamOnly = TeamId.TEAM_ALL, GameObject unitOnly = null,
            FXFlags flags = FXFlags.UpdateOrientation, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null,
            uint? keywordNetIDOverride = null, float fowVisionRadius = 0f, bool affectedByFoW = true,
            bool sendIfOnScreenOrDiscard = false, uint? packageHashOverride = null, bool sendUnbatched = false,
            GameObject specificUnitExclude = null, float startFromTime = 0f)
            : base(game, target.Position, 0, 0, 0, netId, teamOnly)
        {
            // Riot SpellEffectCreateRecord.m_StartFromTime: spawn the particle pre-aged by this many
            // seconds. _currentTime feeds FXCreateData.TimeSpent (PacketNotifier via GetTimeAlive()),
            // which the client applies as UpdateFastForward. Must be set before AddObject — the
            // FX_Create_Group packet is built synchronously there.
            _currentTime = startFromTime;
            PackageHashOverride = packageHashOverride;
            SendUnbatched = sendUnbatched;
            _affectedByFoW = affectedByFoW;
            SendIfOnScreenOrDiscard = sendIfOnScreenOrDiscard;
            VisionRadius = fowVisionRadius;
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
            SpecificUnitExclude = specificUnitExclude;
            FollowsGroundTilt = followGroundTilt;
            Flags = flags;
            IgnoreCasterVisibility = ignoreCasterVisibility;
            OverrideTargetHeight = overrideTargetHeight;

            // Must be set before AddObject — the FX_Create_Group packet is built synchronously
            // there (see the targetPos ctor for the full explanation).
            KeywordNetIDOverride = keywordNetIDOverride;

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
            FXFlags flags = FXFlags.UpdateOrientation, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null,
            uint? keywordNetIDOverride = null, float fowVisionRadius = 0f, bool affectedByFoW = true,
            bool sendIfOnScreenOrDiscard = false, uint? packageHashOverride = null, bool sendUnbatched = false,
            GameObject specificUnitExclude = null, float startFromTime = 0f)
            : base(game, targetPos, 0, 0, 0, netId, teamOnly)
        {
            // Riot SpellEffectCreateRecord.m_StartFromTime: spawn the particle pre-aged by this many
            // seconds. _currentTime feeds FXCreateData.TimeSpent (PacketNotifier via GetTimeAlive()),
            // which the client applies as UpdateFastForward. Must be set before AddObject — the
            // FX_Create_Group packet is built synchronously there.
            _currentTime = startFromTime;
            PackageHashOverride = packageHashOverride;
            SendUnbatched = sendUnbatched;
            _affectedByFoW = affectedByFoW;
            SendIfOnScreenOrDiscard = sendIfOnScreenOrDiscard;
            VisionRadius = fowVisionRadius;
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
            SpecificUnitExclude = specificUnitExclude;
            FollowsGroundTilt = followGroundTilt;
            Flags = flags;
            IgnoreCasterVisibility = ignoreCasterVisibility;
            OverrideTargetHeight = overrideTargetHeight;

            // KeywordNetIDOverride MUST be set before AddObject below — the FX_Create_Group
            // packet is built synchronously inside AddObject (via Sync → ConstructSpawnPacket)
            // and cached into the per-recipient batch. Setting via property after
            // construction has no effect on the cached packet.
            KeywordNetIDOverride = keywordNetIDOverride;

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
            FXFlags flags = FXFlags.UpdateOrientation, bool ignoreCasterVisibility = false,
            float overrideTargetHeight = 0f, string enemyParticle = null,
            uint? keywordNetIDOverride = null, float fowVisionRadius = 0f, bool affectedByFoW = true,
            bool sendIfOnScreenOrDiscard = false, uint? packageHashOverride = null, bool sendUnbatched = false,
            GameObject specificUnitExclude = null, float startFromTime = 0f)
            : base(game, startPos, 0, 0, 0, netId, teamOnly)
        {
            // Riot SpellEffectCreateRecord.m_StartFromTime: spawn the particle pre-aged by this many
            // seconds. _currentTime feeds FXCreateData.TimeSpent (PacketNotifier via GetTimeAlive()),
            // which the client applies as UpdateFastForward. Must be set before AddObject — the
            // FX_Create_Group packet is built synchronously there.
            _currentTime = startFromTime;
            PackageHashOverride = packageHashOverride;
            SendUnbatched = sendUnbatched;
            _affectedByFoW = affectedByFoW;
            SendIfOnScreenOrDiscard = sendIfOnScreenOrDiscard;
            VisionRadius = fowVisionRadius;
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
            SpecificUnitExclude = specificUnitExclude;
            FollowsGroundTilt = followGroundTilt;
            Flags = flags;
            IgnoreCasterVisibility = ignoreCasterVisibility;
            OverrideTargetHeight = overrideTargetHeight;

            // Must be set before AddObject — the FX_Create_Group packet is built synchronously
            // there (see the targetPos ctor for the full explanation).
            KeywordNetIDOverride = keywordNetIDOverride;

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

            // SpecificUnitToExclude: this recipient is barred even if everything else would allow it.
            if (SpecificUnitExclude is Champion excluded && excluded.ClientId == userId)
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

            return SpecificTeam == TeamId.TEAM_ALL;
        }

        public override void Update(float diff)
        {
            // A bound particle's server-side Position is only set once at spawn, so its fog-of-war
            // visibility (TeamHasVisionOn uses Position) is evaluated at the cast location forever.
            // For an FX attached to a roaming unit that means the FX can wrongly leave/enter a team's
            // vision based on the stale spawn point rather than the unit it's attached to. Keep Position
            // on the bind target so FoW tracks the unit. Vision-only — the client attaches via BindNetID,
            // and Position is a plain field (no movement replication), so this changes nothing visual.
            if (BindObject != null && !BindObject.IsToRemove())
            {
                Position = BindObject.Position;
            }

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
