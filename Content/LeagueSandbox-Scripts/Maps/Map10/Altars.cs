using System;
using System.Collections.Generic;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Handlers;
using static LeagueSandbox.GameServer.API.ApiMapFunctionManager;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace MapScripts.Map10
{
    /// <summary>
    /// Twisted Treeline Shadow Altars (West / East). Spawns the two altar units (TT_Buffplat_L/R) at
    /// their wire-exact 4.20 positions and registers each as an engine <see cref="CapturePoint"/> (the
    /// altar's PrimaryAbilityResource is the replicated capture meter). The MasterBuff tier grants
    /// (P3) hook into the OnCaptured/OnUnlocked events. Values are wire-derived from real TT replays —
    /// see project_tt_altars_420.
    /// </summary>
    public static class Altars
    {
        // Wire-derived 4.20 TT capture values (rates PAR/s, times ms).
        private const float GOAL = 60000.0f;          // BaseMP of TT_Buffplat = BLUE's full-capture meter
        private const float NEUTRAL_VALUE = 40000.0f; // resting PAR (neutral colour); PURPLE full = 20000 (mirror)
        private const float FILL_RATE = 2400.0f;       // ~600 PAR per 0.25s replication tick while channeling
        private const float DECAY_RATE = 214.0f;       // meter bleed during the 90s lock (unlocks at ~40000)
        private const float LOCK_DURATION = 90000.0f;  // "locked for 90 seconds" (tooltip + wire)
        private const float UNLOCK_TIME = 180000.0f;   // altars unlock at 3:00 (announcement at 2:30)
        private const float CAPTURE_RADIUS = 300.0f; // confirmed in-game
        // On capture the owner gains a real vision bubble at the altar (Riot AddRegion: GrantVision +
        // RevealStealth, radius 300 = capture radius) — that's how an enemy contesting your altar is
        // seen with TRUE vision (clears fog), not just a fog-silhouette reveal. Lifetime 25000 is the
        // engine "permanent" sentinel (same value turrets use, and the wire TimeToLive in the replay);
        // it persists while the team owns the altar and is explicitly removed when the enemy recaptures.
        private const float OWNER_VISION_LIFETIME = 25000.0f;

        // The owner's active vision bubble per altar, so it can be removed on ownership change.
        private static readonly Dictionary<CapturePoint, Region> _ownerVisionBubble = new Dictionary<CapturePoint, Region>();

        // Source key for the altar's lock animation-state override (so it can be cleared on unlock).
        private static readonly object AnimSource = new object();

        // The chains particle currently shown over a locked altar (TT_Lock_<team>_<side>), so it can be
        // removed when the altar unlocks / changes owner.
        private static readonly Dictionary<CapturePoint, Particle> _chainsParticle = new Dictionary<CapturePoint, Particle>();

        // The persistent owned-state glow (TT_altar_owned_<team>) while a team controls the altar — it
        // stays through the post-capture unlock (the altar keeps giving its buff until the enemy
        // recaptures), and is only swapped/removed when ownership changes.
        private static readonly Dictionary<CapturePoint, Particle> _ownedGlowParticle = new Dictionary<CapturePoint, Particle>();

        // Per-channeling-champion capture beam + sound, so they can be stopped when the champion stops
        // channeling or the capture completes (keyed by champion NetID; a champion channels one altar at a time).
        private static readonly Dictionary<uint, (Particle beam, Particle sound)> _channelFX = new Dictionary<uint, (Particle, Particle)>();

        // West = TT_Buffplat_L (5328.4, 6757.1), East = TT_Buffplat_R (10071.5, 6761.9). Y handled by navgrid.
        private static readonly Vector2 WestPosition = new Vector2(5328.4f, 6757.1f);
        private static readonly Vector2 EastPosition = new Vector2(10071.5f, 6761.9f);

        // Wire-exact spawn facing (S2C_FaceDirection 0x50, constant across all replays). The altars carry
        // design elements (fence/gradient) that only line up at this orientation. West and East are
        // Z-mirrored. LerpTime 0.0833 = the FaceDirection default turnTime.
        private static readonly Vector3 WestDirection = new Vector3(-339.6f, -12.0f, -505.0f);
        private static readonly Vector3 EastDirection = new Vector3(-339.6f, -12.0f, 505.0f);

        // Wire-exact orientation for the chain FX (TT_Lock/LockComplete/Unlock carry flags 0x30 =
        // UpdateOrientation|SimulateWhileOffScreen + this vector, so the chain ends seat into the altar
        // socket instead of floating). Constant per altar across all replays. The radial FX (owned glow,
        // ghost) send no orientation (flags 0x20).
        private static readonly Vector3 WestChainOrientation = new Vector3(-0.62f, 0.0f, -0.79f);
        private static readonly Vector3 EastChainOrientation = new Vector3(-0.83f, 0.0f, -0.56f);

        public static CapturePoint West { get; private set; }
        public static CapturePoint East { get; private set; }

        // The 4.20 tier buffs (real Riot names → correct client icon/tooltip + TT_masterbuff troy).
        private static readonly string[] AllTierBuffs =
        {
            "TreelineMasterBuffT0", "TreelineMasterBuffT1Left", "TreelineMasterBuffT1Right", "TreelineMasterBuffT2"
        };

        public static void Initialize()
        {
            West = SpawnAltar("TT_Buffplat_L", WestPosition, WestDirection);
            East = SpawnAltar("TT_Buffplat_R", EastPosition, EastDirection);

            // On capture the owning team flips, so both teams' altar counts can change — recompute both.
            West.OnCaptured += team => OnAltarCaptured(West, team);
            East.OnCaptured += team => OnAltarCaptured(East, team);
            West.OnUnlocked += () => OnAltarUnlocked(West);
            East.OnUnlocked += () => OnAltarUnlocked(East);
            West.OnChampionBeginCapture += champ => OnCaptureChannelBegin(West, champ);
            East.OnChampionBeginCapture += champ => OnCaptureChannelBegin(East, champ);
            West.OnChampionEndCapture += EndCaptureChannel;
            East.OnChampionEndCapture += EndCaptureChannel;

            // Everyone starts at T0 (baseline mana regen) with both altars neutral.
            RefreshTeamTiers();

            // Both altars start LOCKED & neutral: neutral lock chains + the "initial" minimap icon.
            SetAltarStateVisual(West, locked: true, team: TeamId.TEAM_NEUTRAL);
            SetAltarStateVisual(East, locked: true, team: TeamId.TEAM_NEUTRAL);
        }

        // West = TT_Buffplat_L = side "L"; East = TT_Buffplat_R = side "R".
        private static string SideOf(CapturePoint altar) => altar == West ? "L" : "R";

        // Particle/icon team token: TT_Lock_<Neutral|Blue|Purple>_<side>.
        private static string TeamToken(TeamId team) =>
            team == TeamId.TEAM_BLUE ? "Blue" : team == TeamId.TEAM_PURPLE ? "Purple" : "Neutral";

        // Persistent owned-state glow over a controlled altar (lower-case in the 4.20 set):
        // TT_altar_owned_<blue|purple>.
        private static string OwnedGlowName(TeamId team) =>
            team == TeamId.TEAM_BLUE ? "TT_altar_owned_blue" : "TT_altar_owned_purple";

        // The ghost that rises out of the altar when a team captures it (one-shot at the lock):
        // TT_Ghost<Left|Right><Blue|Purple> (West = Left, East = Right).
        private static string GhostName(CapturePoint altar, TeamId team) =>
            $"TT_Ghost{(altar == West ? "Left" : "Right")}{(team == TeamId.TEAM_BLUE ? "Blue" : "Purple")}";

        // Altar = TT_Buffplat side word for the audio FX (TT_Audio-Altar_<West|East>_Unlocked).
        private static string SideWord(CapturePoint altar) => altar == West ? "West" : "East";

        // Side word for the channel sound (TT_capture_sound_<left|right>).
        private static string SideLower(CapturePoint altar) => altar == West ? "left" : "right";

        /// <summary>
        /// The altar's voice line for a capture: the spirit's dedicated line if the capturer has one
        /// (TT_VO_&lt;side&gt;_&lt;champ&gt;), the generic Shadow Isles line for a Shadow Isles champion without one
        /// (TT_VO_&lt;side&gt;_ShadowIsles), otherwise a random generic line (TT_VO_&lt;side&gt;_Generic1/2). West = "L"
        /// (the Lady), East = "R" (the Lord).
        /// </summary>
        private static string VoiceLineName(CapturePoint altar, Champion capturer)
        {
            string side = SideOf(altar);
            string model = capturer?.Model ?? "";
            if (VoiceLineChampions.Contains(model))
            {
                return $"TT_VO_{side}_{model}";
            }
            if (ShadowIslesChampions.Contains(model))
            {
                return $"TT_VO_{side}_ShadowIsles";
            }
            return $"TT_VO_{side}_Generic{_voiceRng.Next(1, 3)}";
        }

        /// <summary>
        /// A non-owner champion stepped onto a capturable altar: play the capture beam (champion → altar)
        /// and the PAR-driven channel sound (anchored at the altar, aimed at the champion). Wire: caster =
        /// altar, KeywordNetID = 0 for both; beam binds to the champion, sound binds to the altar.
        /// </summary>
        private static void OnCaptureChannelBegin(CapturePoint altar, Champion capturer)
        {
            EndCaptureChannel(capturer); // clear any stale channel FX for this champion first
            string teamWord = capturer.Team == TeamId.TEAM_BLUE ? "blue" : "purple";
            var beam = AddParticleTarget(altar.Altar, capturer, $"TT_capturebeam_{teamWord}", altar.Altar,
                lifetime: CHANNEL_FX_LIFETIME, affectedByFoW: false);
            var sound = AddParticleTarget(altar.Altar, altar.Altar, $"TT_capture_sound_{SideLower(altar)}", capturer,
                lifetime: CHANNEL_FX_LIFETIME, flags: FXFlags.PARDriven | FXFlags.SimulateWhileOffScreen,
                affectedByFoW: false);
            _channelFX[capturer.NetId] = (beam, sound);
        }

        // Stops a champion's capture beam + channel sound when they stop channeling (left range) or the
        // capture completed (the point locks) — otherwise the PAR-driven sound keeps playing after capture.
        private static void EndCaptureChannel(Champion capturer)
        {
            if (_channelFX.TryGetValue(capturer.NetId, out var fx))
            {
                if (fx.beam != null) RemoveParticle(fx.beam);
                if (fx.sound != null) RemoveParticle(fx.sound);
                _channelFX.Remove(capturer.NetId);
            }
        }

        // Persistent vs one-shot altar FX lifetimes. 25000 is the engine "permanent" sentinel.
        private const float PERSISTENT_FX_LIFETIME = 25000.0f;
        private const float ONESHOT_FX_LIFETIME = 3000.0f;
        // The capture beam + channel sound run for roughly the 9s capture (the sound is PAR-driven, so the
        // client tracks the meter); fires once per step-on.
        private const float CHANNEL_FX_LIFETIME = 10000.0f;

        // Champions with a dedicated altar voice line (TT_VO_<side>_<champ>); everyone else gets a random
        // generic line. Case-insensitive against the champion model name.
        private static readonly HashSet<string> VoiceLineChampions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Elise", "Evelynn", "Hecarim", "Karthus", "Mordekaiser", "Thresh", "Yorick"
        };
        // Shadow Isles champions WITHOUT a dedicated line above — they get the generic Shadow Isles line
        // (TT_VO_<side>_ShadowIsles) instead of a plain generic. The 7 above (all Shadow Isles too) override
        // it with their own line. As of patch 4.20 the only such champion is Maokai; edit if more apply.
        private static readonly HashSet<string> ShadowIslesChampions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Maokai"
        };
        private static readonly Random _voiceRng = new Random();

        /// <summary>
        /// Spawns an altar FX exactly as Riot does on the wire: unbound (BindNetID=0) but associated with the
        /// altar (TargetNetID=altar), placed at the altar's <b>ground position</b> — NOT bound to the unit's
        /// model pivot (binding offsets the effect off the altar). Always visible (the altar ignores FoW).
        /// </summary>
        // Wire-exact FX_Create_Group PackageHash for ALL altar particles (constant across both altars, all
        // replays). Our builder would otherwise derive it from the caster's object hash, which is wrong and
        // (for the embedded lock sound in TT_Lock) prevents the sound bank from resolving → no lock sound.
        private const uint ALTAR_FX_PACKAGE_HASH = 0x4d48843f;

        // Our Map10 navgrid samples ~-37 at the altar tiles (the raised dais top), but Riot anchors every
        // altar FX at the ground below (wire Y = -128.8). The FX height = max(navgrid, particleHeight) +
        // OverrideTargetHeight, so this offset lowers them from the dais to that ground (-37 + -91.7 ≈ -128.8).
        private const float ALTAR_FX_GROUND_OFFSET = -91.7f;

        // Wire: every altar FX has CasterNetID=altar but KeywordNetID=0 (NOT the caster's id, which our
        // packet builder would otherwise derive) — so override the keyword net id to 0. SendUnbatched so
        // each altar FX is its own FX_Create_Group packet (Riot's pattern); the client plays only one
        // embedded sound per packet, so bundling the lock sound with the spirit VO drops one of them.
        private static Particle AltarFX(CapturePoint altar, string name, float lifetime) =>
            SpellEffectCreate(name, caster: altar.Altar, targetObject: altar.Altar, lifetime: lifetime,
                overrideTargetHeight: ALTAR_FX_GROUND_OFFSET, fowTeam: TeamId.TEAM_NEUTRAL,
                packageHashOverride: ALTAR_FX_PACKAGE_HASH, sendUnbatched: true);

        private static Vector3 ChainOrientation(CapturePoint altar) =>
            altar == West ? WestChainOrientation : EastChainOrientation;

        /// <summary>
        /// Like <see cref="AltarFX"/> but oriented: the chain effects (lock / lock-complete / unlock) carry
        /// the altar's chain orientation vector + the UpdateOrientation flag (wire flags 0x30) so the chains
        /// seat into the altar socket. Radial effects (owned glow, ghost) use the plain unoriented variant.
        /// </summary>
        private static Particle AltarChainFX(CapturePoint altar, string name, float lifetime) =>
            // orientTowards auto-sets UpdateOrientation; SimulateWhileOffScreen is the SpellEffectCreate
            // baseline — together the wire-exact flags 0x30, no manual flag OR needed.
            SpellEffectCreate(name, caster: altar.Altar, targetObject: altar.Altar, lifetime: lifetime,
                orientTowards: ChainOrientation(altar), overrideTargetHeight: ALTAR_FX_GROUND_OFFSET,
                fowTeam: TeamId.TEAM_NEUTRAL,
                packageHashOverride: ALTAR_FX_PACKAGE_HASH, sendUnbatched: true);

        /// <summary>
        /// Sets an altar's lock visuals: the chains particle over a locked altar (TT_Lock_&lt;team&gt;_&lt;side&gt;,
        /// removed when unlocked) and the minimap icon (AltarInitial pre-unlock, AltarLocked when a team
        /// holds it, AltarUnlocked when capturable). Particle names from the 4.20 particle set.
        /// </summary>
        private static void SetAltarStateVisual(CapturePoint altar, bool locked, TeamId team)
        {
            if (_chainsParticle.TryGetValue(altar, out var old) && old != null)
            {
                RemoveParticle(old);
                _chainsParticle.Remove(altar);
            }

            if (locked)
            {
                _chainsParticle[altar] = AltarChainFX(altar, $"TT_Lock_{TeamToken(team)}_{SideOf(altar)}", PERSISTENT_FX_LIFETIME);
                altar.Altar.IconInfo.ChangeIcon(team == TeamId.TEAM_NEUTRAL ? "AltarInitial" : "AltarLocked");
            }
            else
            {
                altar.Altar.IconInfo.ChangeIcon("AltarUnlocked");
            }
        }

        /// <summary>
        /// Shows the persistent owned-state glow (TT_altar_owned_&lt;team&gt;). It appears once a captured
        /// altar's lock expires (it is capturable again but still owned/buffing) and lives until the next
        /// capture locks it again.
        /// </summary>
        private static void SetOwnedGlow(CapturePoint altar, TeamId team)
        {
            ClearOwnedGlow(altar);
            _ownedGlowParticle[altar] = AltarFX(altar, OwnedGlowName(team), PERSISTENT_FX_LIFETIME);
        }

        private static void ClearOwnedGlow(CapturePoint altar)
        {
            if (_ownedGlowParticle.TryGetValue(altar, out var old) && old != null)
            {
                RemoveParticle(old);
                _ownedGlowParticle.Remove(altar);
            }
        }

        private static void OnAltarCaptured(CapturePoint altar, TeamId team)
        {
            // Ownership flipped to `team`: remove the previous owner's vision bubble on this altar, then
            // grant the new owner true vision of the altar area (GrantVision + RevealStealth) — Riot's
            // per-capture AddRegion (replay: removed once, on the next ownership change). This is how an
            // enemy contesting your altar is seen with real vision (clears fog), not a fog silhouette.
            if (_ownerVisionBubble.TryGetValue(altar, out var oldBubble) && oldBubble != null)
            {
                oldBubble.SetToRemove();
            }
            _ownerVisionBubble[altar] = AddPosPerceptionBubble(
                altar.Position, CAPTURE_RADIUS, OWNER_VISION_LIFETIME, team, revealStealthed: true);

            // The altar becomes the capturing team's unit (wire: S2C_UnitChangeTeam 0xD7 on capture). This
            // is what colours the minimap icon (and the owned visuals) per team, and it PERSISTS through the
            // post-lock unlock until the enemy recaptures — which is why an unlocked-but-owned altar still
            // shows the owner's colour (and keeps giving its buff).
            altar.Altar.SetTeam(team);

            // The previous owner's glow ends now that the altar locks again; the new owner's glow only
            // returns once this lock expires (OnAltarUnlocked).
            ClearOwnedGlow(altar);

            // Capturing locks the altar (wire one-shots at the lock): the team's ghost rises
            // (TT_Ghost<side><team>), the chains wrap on (TT_LockComplete_<team>_<side>), and the altar's
            // spirit speaks its capture voice line (champion-specific or random generic).
            AltarFX(altar, GhostName(altar, team), ONESHOT_FX_LIFETIME);
            AltarChainFX(altar, $"TT_LockComplete_{TeamToken(team)}_{SideOf(altar)}", ONESHOT_FX_LIFETIME);
            AltarFX(altar, VoiceLineName(altar, altar.LastCapturer), ONESHOT_FX_LIFETIME);
            // Note: the lock sound (event Play_sfx_TT_Alter_alterlocked) is embedded in the TT_Lock visual
            // troy (sent below as the persistent chains). Our client does not play sound emitters embedded
            // in visual troys (only standalone audio troys like TT_Audio-Altar play), so the lock sound is a
            // known gap — there is no separate lock-audio packet on the wire to send (replay-verified).

            // Locked look: override the idle with the looping locked animation (wire: SetAnimStates
            // {"IDLE1" -> "LOCKLOOP1"} on capture) + the team's persistent lock chains + AltarLocked icon.
            OverrideAnimation(altar.Altar, "LOCKLOOP1", "IDLE1", AnimSource);
            SetAltarStateVisual(altar, locked: true, team: team);

            RefreshTeamTiers();
        }

        private static void OnAltarUnlocked(CapturePoint altar)
        {
            // Unlock: clear the lock override (wire: empty SetAnimStates), play the unlock sequence, and
            // drop the chains + switch the minimap icon to unlocked. The owner's team stays (the altar is
            // still owned and buffing until the enemy recaptures), so the unlocked icon keeps the colour.
            RemoveOverrideAnimations(altar.Altar, AnimSource);
            PlayAnimation(altar.Altar, "UnlockSequence");
            SetAltarStateVisual(altar, locked: false, team: TeamId.TEAM_NEUTRAL);

            // Unlock one-shots (wire): the team/neutral unlock burst + the per-altar unlocked audio.
            AltarChainFX(altar, $"TT_Unlock_{TeamToken(altar.OwnerTeam)}_{SideOf(altar)}", ONESHOT_FX_LIFETIME);
            AltarFX(altar, $"TT_Audio-Altar_{SideWord(altar)}_Unlocked", ONESHOT_FX_LIFETIME);

            // Capturable again: a still-owned altar now shows its persistent owned glow (TT_altar_owned_*).
            // The initial neutral unlock at UNLOCK_TIME has no owner, so nothing.
            if (altar.OwnerTeam != TeamId.TEAM_NEUTRAL)
            {
                SetOwnedGlow(altar, altar.OwnerTeam);
            }
        }

        /// <summary>Recomputes each team's MasterBuff tier from how many altars it currently owns.</summary>
        private static void RefreshTeamTiers()
        {
            ApplyTierToTeam(TeamId.TEAM_BLUE);
            ApplyTierToTeam(TeamId.TEAM_PURPLE);
        }

        private static void ApplyTierToTeam(TeamId team)
        {
            bool ownsWest = West.OwnerTeam == team;
            bool ownsEast = East.OwnerTeam == team;
            int count = (ownsWest ? 1 : 0) + (ownsEast ? 1 : 0);

            string tierBuff;
            if (count >= 2)
            {
                tierBuff = "TreelineMasterBuffT2";
            }
            else if (count == 1)
            {
                // Left/Right is cosmetic (icon) — effect is identical; pick by which altar is held.
                tierBuff = ownsWest ? "TreelineMasterBuffT1Left" : "TreelineMasterBuffT1Right";
            }
            else
            {
                tierBuff = "TreelineMasterBuffT0";
            }

            foreach (var champion in GetAllPlayersFromTeam(team))
            {
                foreach (var b in AllTierBuffs)
                {
                    if (b != tierBuff)
                    {
                        RemoveBuff(champion, b);
                    }
                }
                if (!HasBuff(champion, tierBuff))
                {
                    // Source = the champion itself so the buff carries a valid source unit (its name shows
                    // on the buff) instead of a null source.
                    AddBuff(tierBuff, 25000.0f, 1, null, champion, champion, infiniteduration: true);
                }
            }
        }

        private static CapturePoint SpawnAltar(string model, Vector2 position, Vector3 direction)
        {
            // Static neutral unit: no collision, not attackable (NoAutoAttack/NoHealthBar in its data),
            // no AI so it never moves. Its mana (max = BaseMP 60000) is the capture meter.
            var altar = CreateMinion(model, model, position, team: TeamId.TEAM_NEUTRAL,
                ignoreCollision: true, isTargetable: false);
            altar.Stats.CurrentMana = NEUTRAL_VALUE; // tug-of-war meter rests at neutral (NOT 0 = a team colour)
            // Altars are objectives: always visible to both teams like a structure (its data gives no
            // vision — PerceptionBubbleRadius 0 — so without this it only renders when a champion stands
            // on it and reads "unloaded" from outside fog).
            altar.AlwaysVisible = true;
            // Capture-progress circle: a PAR-driven flex particle (wire attach type 3) attached after
            // spawn; it fills from the altar's mana meter. (TT uses NO HandleCapturePointUpdate — that's
            // the Dominion-only 0xD3 path; see project_tt_altars_420.)
            altar.CaptureCircleFlexAttachType = 3;

            // Altars start LOCKED until they unlock at UNLOCK_TIME (180s): loop the locked animation from
            // spawn (wire: SetAnimStates {IDLE1 -> LOCKLOOP1} at t=0). The CapturePoint fires OnUnlocked
            // at the unlock time → OnAltarUnlocked clears this and plays the unlock sequence. (Stored on
            // the unit and re-sent per-recipient on spawn via the AlwaysVisible hook, so it isn't lost to
            // the pre-spawn-broadcast timing.)
            OverrideAnimation(altar, "LOCKLOOP1", "IDLE1", AnimSource);

            // Set the altar's heading (like Riot's AIMinion::Create facing = FacingPos − Pos). The unit
            // never moves and SpawnMinionS2C carries no facing field, so the engine re-sends this as a
            // S2C_FaceDirection right after the spawn, per recipient (see NotifyEnterTeamVision's
            // AlwaysVisible branch) — matching Riot, no post-spawn timer. The broadcast this call emits
            // now (before any client has the unit) is harmlessly dropped; its job here is to store Direction.
            altar.FaceDirection(direction, isInstant: true);

            return AddCapturePoint(altar, GOAL, NEUTRAL_VALUE, FILL_RATE, DECAY_RATE, LOCK_DURATION, CAPTURE_RADIUS, UNLOCK_TIME);
        }
    }
}
