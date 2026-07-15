using GameServerCore.Enums;
using System;
using System.Numerics;

namespace LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI
{
    /// <summary>
    /// A master-attached, master-following object (Riot: <c>FollowerObject : obj_AI_Base</c>,
    /// <c>Game/LoL/AI/Object/Follower/AIFollower.cpp</c>). Decomp-verified behaviour:
    ///   * <c>Create(internalName, charName, skinID, master)</c> attaches to a master obj_AI_Base and
    ///     sets <c>mIsInvulnerable=1</c>; <c>SetMasterObject</c> sets <c>TeamID = master.Team</c>.
    ///   * <c>Update</c>: <c>Position = master.Position</c> every tick; no master ⇒ returns false
    ///     (deactivate/remove).
    ///   * <c>IsValidSpellTarget</c> ⇒ false + invulnerable ⇒ never a valid attack/spell target.
    ///   * <c>IsVisible</c> ⇔ master visible (the CLIENT hides the follower with its master).
    /// Spawned client-side by the standalone <c>S2C_CreateFollowerObject</c> (0xF1, header
    /// SenderNetID = master); reparented/detached by <c>S2C_ReattachFollowerObject</c> (0xF2,
    /// NewOwnerId=0 ⇒ detach ⇒ client deactivates it). Replay-verified 4.20 user: <c>SyndraOrbs</c>
    /// (Syndra's orbiting orbs; every Reattach in the replays used NewOwnerId=0 = release).
    /// </summary>
    public class FollowerObject : ObjAIBase
    {
        /// <summary>The obj_AI_Base this follower is attached to and follows. Null once detached.</summary>
        public ObjAIBase Master { get; private set; }

        /// <summary>NetNodeID sent in S2C_CreateFollowerObject (0x40 in every replay).</summary>
        public byte NetNodeID { get; }

        /// <summary>The follower's internal object name (S2C_CreateFollowerObject.InternalName).</summary>
        public string InternalName { get; }

        /// <summary>
        /// When true, the follower is periodically turned to a new facing (a steady deterministic
        /// orbit spin — see Update). Set false if a script drives the follower's facing itself.
        /// </summary>
        public bool AutoRotate { get; set; } = true;

        // Orbit rotation cadence. LerpTime 1.75s is replay-verified (every S2C_FaceDirection for
        // SyndraOrbs used LerpTime=1.75). The exact per-update direction is Syndra-kit-specific
        // (roughly random in the replays), so we drive a steady deterministic turn instead (no RNG —
        // server determinism). Interval == LerpTime so each turn blends straight into the next.
        private const float FaceIntervalMs = 1750f;
        private const float FaceStepDeg = 45f;
        private const float FaceLerpSeconds = 1.75f;
        private float _faceTimer;
        private float _faceAngleDeg;

        // 4.20 REPLAY-VERIFIED (differs from the 4.17 decomp, which made the follower a purely
        // client-side always-attached object): in 4.20 the follower is a normal vision-gated unit —
        // it enters/leaves visibility with its master (OnEnter/LeaveVisibilityClient 0xBA/0x51 seen per
        // follower in the replays). So we do NOT override IsAffectedByFoW — inherit AttackableUnit's
        // fog behaviour (true). Since the follower sits at the master's exact position, our vision
        // system naturally makes it visible exactly when the master's position is, and the standard
        // OnEnterVision/OnLeaveVision machinery sends the 0xBA/0x51 toggles.

        public FollowerObject(Game game, string internalName, string characterName, int skinId,
            ObjAIBase master, byte netNodeId = 0x40, uint netId = 0)
            : base(game, model: characterName, name: internalName, skinId: skinId, netId: netId,
                   team: master.Team, enableScripts: false)
        {
            InternalName = internalName;
            NetNodeID = netNodeId;
            // Decomp Create() sets mIsInvulnerable=1: a follower can never take damage (belt-and-braces
            // with IsTargetableByUnit=false, so even an AoE that doesn't filter targetability can't hurt it).
            SetStatus(StatusFlags.Invulnerable, true);
            SetMaster(master);
        }

        // Decomp: IsValidSpellTarget => false. A follower is never a valid attack/spell target
        // (this is how Riot's "invulnerable" follower is realised — it can't be selected or hit).
        public override bool IsTargetableByUnit(AttackableUnit acquirer) => false;

        /// <summary>
        /// Attach to a new master (Riot: <c>SetMasterObject</c>) — inherits the master's team. A null
        /// master detaches the follower (which then removes itself on the next Update, matching the
        /// decomp's "no master ⇒ Update returns false").
        /// </summary>
        public void SetMaster(ObjAIBase master)
        {
            Master = master;
            if (master != null)
            {
                SetTeam(master.Team);
            }
        }

        // Follower spawn is the standalone S2C_CreateFollowerObject (0xF1) with SenderNetID = master,
        // NOT the standard unit spawn packet — so override the per-player spawn hook. IsAffectedByFoW
        // is false, so this fires once per player (≈ the replay's broadcast).
        protected override void OnSpawn(int userId, TeamId team, bool doVision)
        {
            _game.PacketNotifier.NotifyS2C_CreateFollowerObject(this, userId);
        }

        public override void Update(float diff)
        {
            // Decomp FollowerObject::Update: no (live) master ⇒ deactivate; otherwise snap to the
            // master's position. Deliberately does NOT call base ObjAIBase.Update — a follower runs no
            // AI/pathing (it just tracks the master), matching the decomp's full override.
            if (Master == null || Master.IsToRemove() || Master.IsDead)
            {
                SetToRemove();
                return;
            }

            SetPosition(Master.Position);

            // Orbit rotation (4.20 replay: a stream of S2C_FaceDirection with LerpTime=1.75s, sent
            // even while the follower isn't visible). Advance a steady facing so the follower visibly
            // turns; a script can drive the facing itself by clearing AutoRotate.
            if (AutoRotate)
            {
                _faceTimer += diff;
                if (_faceTimer >= FaceIntervalMs)
                {
                    _faceTimer -= FaceIntervalMs;
                    _faceAngleDeg = (_faceAngleDeg + FaceStepDeg) % 360f;
                    float rad = _faceAngleDeg * (MathF.PI / 180f);
                    FaceDirection(new Vector3(MathF.Cos(rad), 0f, MathF.Sin(rad)));
                }
            }
        }

        /// <summary>
        /// Turns the follower to face a world-space direction, blending over <paramref name="lerpTime"/>
        /// seconds (Riot <c>FollowerObject::DoFaceDirection</c>; S2C_FaceDirection 0x50). Broadcast, so
        /// it's tracked even while the follower isn't visible — matching the replays.
        /// </summary>
        public void FaceDirection(Vector3 direction, float lerpTime = FaceLerpSeconds)
        {
            _game.PacketNotifier.NotifyFaceDirection(this, direction, isInstant: false, turnTime: lerpTime);
        }

        /// <summary>
        /// Plays an animation on the follower (S2C_PlayAnimation 0xB0). Replay: SyndraOrbs plays
        /// "Orbs" + "Backup_Idle" at creation. Vision-scoped broadcast.
        /// </summary>
        public void PlayAnimation(string animation)
        {
            _game.PacketNotifier.NotifyS2C_PlayAnimation(this, animation);
        }

        // A follower is a cosmetic object pinned to its master's position: it must NOT register as a
        // physics collider (it would block units at the master's feet) nor as its own vision provider
        // (the master already provides vision there). So skip GameObject.OnAdded's collision + vision
        // registration entirely.
        public override void OnAdded()
        {
        }

        // A follower does NONE of the standard AttackableUnit net-replication (Riot's FollowerObject
        // fully overrides net-visibility and doesn't replicate like a normal unit — its client state is
        // driven purely by CreateFollowerObject + tracking the master). Crucially, base ObjAIBase never
        // allocates a `Replication` for us — only concrete units (Champion/Minion/...) do — so the base
        // OnSync/OnAfterSync would NullReference on `Replication`. No-op them.
        protected override void OnSync(int userId, TeamId team)
        {
        }

        public override void OnAfterSync()
        {
        }

        public override void OnRemoved()
        {
            // Tell clients to detach this follower (reattach to owner 0 ⇒ the client's FollowerObject
            // loses its master and deactivates). This is the follower's removal packet (decomp).
            _game.PacketNotifier.NotifyS2C_ReattachFollowerObject(this, 0);
            base.OnRemoved();
        }
    }
}
