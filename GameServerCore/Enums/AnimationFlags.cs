using System;

namespace GameServerCore.Enums
{
    /// <summary>
    /// Flag byte for S2C_PlayAnimation (0xB0).
    /// Verified against the S4 mac decomp (patch 4.17):
    /// the client translates the wire byte in Riot::AnimationHelper::PlaybackFlagsFromPacketFlags
    /// (AnimationHelperClient.cpp:7) and only reads bits 0-4; bits 5-7 are never read.
    /// Replays confirm: bits 5-7 vary randomly across casts of the same animation
    /// (uninitialized Riot memory, same pattern as the NPC_Die bitfield),
    /// while bits 0-4 are deterministic per use-site.
    /// </summary>
    [Flags]
    public enum AnimationFlags : byte
    {
        None = 0,
        /// <summary>
        /// Locks the animation track until the clip finishes (client TransitionControllerNode::IsLocked).
        /// While locked, the client drops any further S2C_PlayAnimation packets
        /// (obj_AI_Base_PImpl_Int::OnNetworkPacket early-returns on IsAnimationLocked),
        /// suppresses automatic Run/Idle updates and survives StopAllAnimation(ignoreLock=false).
        /// Takes precedence over <see cref="OverrideIdle"/> (bit 1 is ignored when this is set).
        /// Riot examples: forced Idle1 plays, Attack1_Dash.
        /// </summary>
        Lock = 1 << 0,
        /// <summary>
        /// Marks the track idle-locked (client TransitionControllerNode::IsIdleLocked):
        /// the automatic Idle1 cycle will not replace this animation while it plays
        /// (obj_AI_Base::UpdateAnimationWalkRun skips UpdateIdleCycleAnimation).
        /// Run is NOT blocked. Ignored when <see cref="Lock"/> is set.
        /// Riot examples: Thresh Sickle_Null / Lantern_Null state overlays.
        /// </summary>
        OverrideIdle = 1 << 1,
        /// <summary>
        /// Skips blend-in/out: the animation snaps instantly instead of cross-fading
        /// (forces blendTime = 0 in Sequencer::ScheduleController*, node flag 0x10).
        /// Riot examples: Death1, Respawn, Jinx Rlauncher_To_Minigun stance swaps.
        /// </summary>
        NoBlend = 1 << 2,
        /// <summary>
        /// Holds the last frame when a non-looping clip reaches its end instead of finishing
        /// (AtomicClipController::Update returns playbackFlags &amp; 2 at clip end).
        /// Riot example: Destroyed_seq building animations stay in the destroyed pose.
        /// </summary>
        FreezeAtEnd = 1 << 3,
        /// <summary>
        /// Keeps a currently playing emote (dance/taunt/laugh) instead of clearing it
        /// (mapped to obj_AI_Base::kKeepAllEmote; without this bit the client calls ClearEmote).
        /// Riot example: Jinx weapon-swap animations (Rlauncher_To_Minigun | NoBlend).
        /// </summary>
        KeepEmote = 1 << 4,
        // Bits 5-7: not read by the 4.x client. Riot replays carry uninitialized
        // junk in these bits so do not byte-match them and do not send them.
        Junk5 = 1 << 5,
        Junk6 = 1 << 6,
        Junk7 = 1 << 7,
    }
}
