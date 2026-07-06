using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;

namespace LeagueSandbox.GameServer.Handlers
{
    /// <summary>
    /// Engine-side capture-point state machine, mirroring Riot's <c>CapturePoint::CapturePointState</c>
    /// (Twisted Treeline altars / Dominion control points). The capture meter is stored as the parent
    /// unit's PrimaryAbilityResource (mana) — exactly as Riot does (<c>AlwaysUpdatePAR=1</c>,
    /// <c>BaseMP</c> = goal) — so the 0..Goal value replicates to clients for free as the on-altar
    /// capture circle. See project_tt_altars_420 for the wire-derived values.
    ///
    /// Behaviour (empirically decoded from 4.20 TT replays, OnReplication 0xC4 PAR meter):
    ///  - Capturable only after <see cref="UnlockTime"/> (altars start inactive; Map10 unlock = 180s).
    ///  - A single non-owner team standing in <see cref="CaptureRadius"/> fills the meter toward
    ///    <see cref="Goal"/> at <see cref="FillRate"/> PAR/s. Contested (both teams present) pauses.
    ///  - At <see cref="Goal"/>: ownership flips to the capturing team (<see cref="OnCaptured"/>), the
    ///    point locks for <see cref="LockDuration"/>, during which the meter decays at
    ///    <see cref="DecayRate"/> PAR/s. On unlock (<see cref="OnUnlocked"/>) the meter holds at its
    ///    decayed value (~Goal − LockDuration*DecayRate), so re-captures start partway up.
    /// </summary>
    public class CapturePoint
    {
        private readonly Game _game;

        public AttackableUnit Altar { get; }
        public Vector2 Position => Altar.Position;

        // Wire-derived config (4.20 TT). All rates are PAR/second; times are milliseconds.
        // The capture meter is a TUG-OF-WAR around NeutralValue: BLUE pushes UP to Goal, PURPLE pushes
        // DOWN to the mirrored PurpleGoal. The PAR rests at NeutralValue (neutral colour), so the altar
        // does NOT start at 0 (that reads as one team's colour). Wire: neutral ~40000, blue full 60000,
        // purple full ~20000 (symmetric ±20000), decay back toward neutral during the lock.
        public float Goal { get; }            // BLUE's full-capture value (the PAR max, e.g. 60000)
        public float NeutralValue { get; }    // resting/neutral PAR (e.g. 40000)
        public float PurpleGoal => 2.0f * NeutralValue - Goal; // mirrored full-capture for PURPLE (e.g. 20000)
        public float FillRate { get; }
        public float DecayRate { get; }
        public float LockDuration { get; }
        public float CaptureRadius { get; }
        public float UnlockTime { get; }

        public TeamId OwnerTeam { get; private set; } = TeamId.TEAM_NEUTRAL;
        public bool IsLocked { get; private set; }
        public bool IsActive => _game.GameTime >= UnlockTime;
        private float _lockTimer;
        private bool _initialUnlockFired;

        // Non-owner champions currently channeling the capture, so a fresh step-on fires the capture
        // beam + sound once and leaving / completing fires the matching stop. The champion that pushed
        // the meter to its goal.
        private readonly Dictionary<uint, Champion> _activeCapturers = new Dictionary<uint, Champion>();
        public Champion LastCapturer { get; private set; }

        /// <summary>Fired the instant the meter reaches <see cref="Goal"/>; argument is the capturing team.</summary>
        public event Action<TeamId> OnCaptured;
        /// <summary>
        /// Fired when a non-owner champion newly steps onto the (capturable) altar and begins a capture
        /// channel — once per step-on, used to play the capture beam + channel sound.
        /// </summary>
        public event Action<Champion> OnChampionBeginCapture;
        /// <summary>
        /// Fired when a channeling champion stops (leaves range, or the point locks on capture) — used to
        /// stop the capture beam + channel sound so it does not keep playing after the capture finishes.
        /// </summary>
        public event Action<Champion> OnChampionEndCapture;
        /// <summary>
        /// Fired when the point becomes capturable: once at <see cref="UnlockTime"/> (the initial unlock —
        /// altars start locked) and again each time a post-capture lock expires.
        /// </summary>
        public event Action OnUnlocked;

        public CapturePoint(Game game, AttackableUnit altar, float goal, float neutralValue, float fillRate,
            float decayRate, float lockDuration, float captureRadius, float unlockTime)
        {
            _game = game;
            Altar = altar;
            Goal = goal;
            NeutralValue = neutralValue;
            FillRate = fillRate;
            DecayRate = decayRate;
            LockDuration = lockDuration;
            CaptureRadius = captureRadius;
            UnlockTime = unlockTime;
        }

        private float Meter
        {
            get => Altar.Stats.CurrentMana;
            set => Altar.Stats.CurrentMana = value;
        }

        public void Update(float diff)
        {
            if (Altar == null || Altar.IsDead)
            {
                return;
            }

            // Before UnlockTime the altar sits in its initial locked state (visual set at spawn).
            if (!IsActive)
            {
                return;
            }

            // Initial unlock at UnlockTime: the "Altars have unlocked" moment (locked -> capturable).
            if (!_initialUnlockFired)
            {
                _initialUnlockFired = true;
                OnUnlocked?.Invoke();
            }

            float perTick = diff / 1000.0f;

            // Which teams have a champion in range (for the capture meter). The owner's altar vision —
            // so they see enemies contesting it — is a GrantVision perception bubble granted on capture
            // (see Altars.OnCaptured), NOT a per-champion reveal, matching Riot's AddRegion at the altar.
            bool blue = false, purple = false;
            Champion blueChamp = null, purpleChamp = null;
            var stillChanneling = new HashSet<uint>();
            foreach (var champ in ApiFunctionManager.EnumerateChampionsInRange(Position, CaptureRadius, true))
            {
                if (champ.Team == TeamId.TEAM_BLUE) { blue = true; blueChamp ??= champ; }
                else if (champ.Team == TeamId.TEAM_PURPLE) { purple = true; purpleChamp ??= champ; }

                // Capture-channel begin: a non-owner champion on a capturable altar — fire once per step-on.
                if (!IsLocked && champ.Team != OwnerTeam && champ.Team != TeamId.TEAM_NEUTRAL)
                {
                    stillChanneling.Add(champ.NetId);
                    if (!_activeCapturers.ContainsKey(champ.NetId))
                    {
                        _activeCapturers[champ.NetId] = champ;
                        OnChampionBeginCapture?.Invoke(champ);
                    }
                }
            }

            // Channel end: any champion that was channeling but no longer is (left range, or the point just
            // locked → IsLocked gates them all out) — fire the stop so the beam + sound don't linger.
            foreach (var netId in _activeCapturers.Keys.ToList())
            {
                if (!stillChanneling.Contains(netId))
                {
                    var champ = _activeCapturers[netId];
                    _activeCapturers.Remove(netId);
                    OnChampionEndCapture?.Invoke(champ);
                }
            }

            if (IsLocked)
            {
                // Locked: the meter bleeds back toward neutral; the point stays uncapturable for the
                // full LockDuration regardless of the meter value.
                Meter = DecayToward(Meter, NeutralValue, DecayRate * perTick);
                _lockTimer -= diff;
                if (_lockTimer <= 0.0f)
                {
                    IsLocked = false;
                    OnUnlocked?.Invoke();
                }
                return;
            }

            // A team can only capture a point it does NOT already own.
            bool blueCaptures = blue && OwnerTeam != TeamId.TEAM_BLUE;
            bool purpleCaptures = purple && OwnerTeam != TeamId.TEAM_PURPLE;

            if (blueCaptures && purpleCaptures)
            {
                // Contested: both teams pull → no net progress (meter holds).
                return;
            }

            if (blueCaptures)
            {
                // BLUE drags the meter UP toward Goal.
                float meter = Meter + FillRate * perTick;
                if (meter >= Goal)
                {
                    Meter = Goal;
                    LastCapturer = blueChamp;
                    Capture(TeamId.TEAM_BLUE);
                }
                else
                {
                    Meter = meter;
                }
            }
            else if (purpleCaptures)
            {
                // PURPLE drags the meter DOWN toward PurpleGoal.
                float meter = Meter - FillRate * perTick;
                if (meter <= PurpleGoal)
                {
                    Meter = PurpleGoal;
                    LastCapturer = purpleChamp;
                    Capture(TeamId.TEAM_PURPLE);
                }
                else
                {
                    Meter = meter;
                }
            }
            else
            {
                // Uncontested: an abandoned partial capture drifts back toward neutral.
                Meter = DecayToward(Meter, NeutralValue, DecayRate * perTick);
            }
        }

        private void Capture(TeamId team)
        {
            OwnerTeam = team;
            IsLocked = true;
            _lockTimer = LockDuration;
            OnCaptured?.Invoke(team);
        }

        private static float DecayToward(float value, float target, float step)
        {
            if (value > target) return Math.Max(target, value - step);
            if (value < target) return Math.Min(target, value + step);
            return target;
        }
    }
}
