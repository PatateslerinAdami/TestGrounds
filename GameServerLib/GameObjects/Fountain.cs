using System;
using System.Numerics;
using GameServerCore.Enums;
using LeagueSandbox.GameServer.Content;

namespace LeagueSandbox.GameServer.GameObjects
{
    public class Fountain
    {
        private readonly Game _game;
        private float _healTickTimer;
        public Vector2 Position { get; set; }
        public TeamId Team { get; set; }

        public Fountain(Game game, TeamId team, Vector2 position)
        {
            _game = game;
            Position = position;
            _healTickTimer = 0;
            Team = team;
        }

        public void Update(float diff)
        {
            // All fountain regen parameters come from the map's Constants.var (sp_*), loaded into
            // GlobalData.SpawnPointVariables — the authoritative per-map source (previously these were
            // hardcoded literals that matched no Constants.var value). Faithful to obj_SpawnPoint::Update
            // (mac 4.17, BuildingSpawnPoint.cpp): the tick is gated by sp_RegenTickInterval, targets are
            // collected within sp_RegenRadius, and HP is restored by sp_HealthRegenPercent of max per tick.
            //
            // ARAM (Map12 reuses the Map1/tutorial map, hence Riot's split) selects sp_HealthRegenPercentARAM
            // instead, which is 0 — and the decomp's `if (rate <= 0) return` guard then disables fountain
            // regen ENTIRELY (no HP and no mana), matching ARAM having no fountain healing.
            //
            // Uncertain (documented in docs/CONSTANTS_VAR_AUDIT.md): mana uses sp_ManaRegenPercent per that
            // constant's Constants.var comment; the decomp's spawn-point Update did not reference it (its
            // exact application site was not recovered).
            var spawn = GlobalData.SpawnPointVariables;
            var healthRegenPercent = _game.Config.GameConfig.GameMode == "ARAM"
                ? spawn.HealthRegenPercentARAM
                : spawn.HealthRegenPercent;

            _healTickTimer += diff;
            if (_healTickTimer < spawn.RegenTickInterval * 1000f)
            {
                return;
            }

            _healTickTimer = 0;

            // A non-positive health-regen rate disables the fountain for this mode (obj_SpawnPoint::Update
            // returns before healing) — this is how ARAM turns fountain regen off.
            if (healthRegenPercent <= 0f)
            {
                return;
            }

            var champions = _game.ObjectManager.GetChampionsInRange(Position, spawn.RegenRadius, true);
            foreach (var champion in champions)
            {
                if (champion.Team != Team)
                {
                    continue;
                }

                champion.TakeHeal(champion, champion.Stats.HealthPoints.Total * healthRegenPercent,
                    HealType.HealthRegeneration);

                if ((byte)champion.Stats.ParType > 1)
                {
                    continue;
                }

                var mp = champion.Stats.CurrentMana;
                var maxMp = champion.Stats.ManaPoints.Total;
                champion.Stats.CurrentMana = Math.Min(mp + maxMp * spawn.ManaRegenPercent, maxMp);
                _game.ProtectionManager.HandleFountainProtection(champion);
            }
        }
    }
}
