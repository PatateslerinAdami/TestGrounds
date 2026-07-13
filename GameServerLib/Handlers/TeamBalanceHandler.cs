using GameServerCore.Domain;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.Logging;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LeagueSandbox.GameServer.Handlers
{
    // Team-balance vote. Structurally a mirror of SurrenderHandler (manual, player-initiated vote
    // as chosen for this server), but scoped to the voting team and — on pass — GRANTS catch-up
    // compensation (gold/exp) instead of ending the game. The client reuses the surrender vote HUD
    // (HudVote.mIsBalanceVote) and the same reason codes (SurrenderReason), so we reuse that enum.
    //
    // DATA GAP (flagged, not silently invented): the real 4.20 server auto-OFFERED this vote when a
    // team was short a human player and the gold/exp/tower grant amounts are NOT recoverable from the
    // (client-only) mac decomp. So: trigger is manual (user-chosen), and the grant amounts are owned
    // by the map script via AddTeamBalance(...) — tune there, they are placeholders, not 4.20-exact.
    // TowersGranted is forwarded to the client for the HUD breakdown only; applying server-side tower
    // compensation is undefined in the decomp and intentionally NOT done here.
    public class TeamBalanceHandler : IUpdate
    {
        private Dictionary<Champion, bool> _votes = new Dictionary<Champion, bool>();
        private Game _game;
        private static ILog _logger = LoggerProvider.GetLogger();

        public float BalanceMinimumTime { get; set; }
        public float BalanceRestTime { get; set; }
        public float BalanceLength { get; set; }
        public float LastBalanceTime { get; set; }
        public bool IsBalanceActive { get; set; }
        public TeamId Team { get; set; }

        public float GoldGranted { get; set; }
        public int ExperienceGranted { get; set; }
        public int TowersGranted { get; set; }

        // All three timing params are in SECONDS (minTime/restTime/length) — mirrors SurrenderHandler.
        // Converted to ms internally at the GameTime comparisons below (GameTime is ms).
        public TeamBalanceHandler(Game g, TeamId team, float minTime, float restTime, float length,
            float goldGranted, int experienceGranted, int towersGranted)
        {
            _game = g;
            Team = team;
            BalanceMinimumTime = minTime;
            BalanceRestTime = restTime;
            BalanceLength = length;
            GoldGranted = goldGranted;
            ExperienceGranted = experienceGranted;
            TowersGranted = towersGranted;
        }

        public Tuple<int, int> GetVoteCounts()
        {
            int yes = _votes.Count(kv => kv.Value == true);
            int no = _votes.Count - yes;
            return new Tuple<int, int>(yes, no);
        }

        public void HandleTeamBalanceVote(int userId, Champion who, bool vote)
        {
            if (_game.GameTime < BalanceMinimumTime * 1000.0f)
            {
                _game.PacketNotifier.NotifyTeamBalanceStatus(userId, who.Team, SurrenderReason.NotAllowedYet, 0, 0, GoldGranted, ExperienceGranted, TowersGranted);
                return;
            }

            bool open = !IsBalanceActive;
            if (!IsBalanceActive && _game.GameTime < LastBalanceTime + BalanceRestTime * 1000.0f)
            {
                _game.PacketNotifier.NotifyTeamBalanceStatus(userId, who.Team, SurrenderReason.DontSpamSurrender, 0, 0, GoldGranted, ExperienceGranted, TowersGranted);
                return;
            }

            if (open)
            {
                IsBalanceActive = true;
                LastBalanceTime = _game.GameTime;
                _votes.Clear();
            }

            if (_votes.ContainsKey(who))
            {
                _game.PacketNotifier.NotifyTeamBalanceStatus(userId, who.Team, SurrenderReason.AlreadyVoted, 0, 0, GoldGranted, ExperienceGranted, TowersGranted);
                return;
            }
            _votes[who] = vote;
            Tuple<int, int> voteCounts = GetVoteCounts();
            // Per-team vote: only this team's players are eligible (the disadvantaged team accepts balance).
            var teamPlayers = _game.PlayerManager.GetPlayers(false).Where(p => p.Team == Team).ToList();
            int total = teamPlayers.Count;

            _logger.Info($"Champion {who.Model} voted {vote} for team balance. Currently {voteCounts.Item1} yes, {voteCounts.Item2} no, of {total} team players");

            _game.PacketNotifier.NotifyTeamBalanceVote(who, open, vote, (byte)voteCounts.Item1, (byte)voteCounts.Item2, (byte)total, BalanceLength, GoldGranted, ExperienceGranted, TowersGranted);

            if (voteCounts.Item1 >= total - 1)
            {
                IsBalanceActive = false;
                foreach (var p in teamPlayers)
                {
                    _game.PacketNotifier.NotifyTeamBalanceStatus(p.ClientId, Team, SurrenderReason.SurrenderAgreed, (byte)voteCounts.Item1, (byte)voteCounts.Item2, GoldGranted, ExperienceGranted, TowersGranted);
                }
                GrantBalance(teamPlayers.Select(p => p.Champion));
            }
        }

        // Apply the catch-up compensation to every champion on the team. Gold + experience use the
        // standard Champion grant API (same path as turret/minion gold). Tower compensation is sent to
        // the client for display only (see DATA GAP note above) — not applied server-side.
        private void GrantBalance(IEnumerable<Champion> teamChampions)
        {
            foreach (var champion in teamChampions)
            {
                if (champion == null)
                {
                    continue;
                }
                if (GoldGranted > 0)
                {
                    champion.AddGold(null, GoldGranted, false);
                }
                if (ExperienceGranted > 0)
                {
                    champion.AddExperience(ExperienceGranted, false);
                }
            }
        }

        public void Update(float diff)
        {
            if (IsBalanceActive && _game.GameTime >= LastBalanceTime + (BalanceLength * 1000.0f))
            {
                IsBalanceActive = false;
                Tuple<int, int> count = GetVoteCounts();
                foreach (var p in _game.PlayerManager.GetPlayers(false))
                {
                    if (p.Team == Team)
                    {
                        _game.PacketNotifier.NotifyTeamBalanceStatus(p.ClientId, Team, SurrenderReason.VoteWasNoSurrender, (byte)count.Item1, (byte)count.Item2, GoldGranted, ExperienceGranted, TowersGranted);
                    }
                }
            }
        }
    }
}
