using GameServerLib.GameObjects.AttackableUnits;
using LeaguePackets.Game.Events;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServerLib.Handlers
{
    internal static class ChampionDeathHandler
    {
        private const float EVIL_UNK_CONST_LEVEL_DIFF_BONUS_EXP_MULT = 0.1455f;
        private const float DEFAULT_MULTI_KILL_WINDOW_MS = 30_000.0f;

        sealed class MultiKillState
        {
            public int KillCount;
            public float LastKillTime;
        }

        static readonly Dictionary<uint, MultiKillState> MultiKillStates = new Dictionary<uint, MultiKillState>();

        static Game _game;

        internal static void Init(Game game)
        {
            _game = game;
        }

        internal static OnDeathAssist OnDeathAssistConstructor(DeathData deathData, AssistMarker marker)
        {
            return new OnDeathAssist()
            {
                AtTime = marker.StartTime,
                PhysicalDamage = marker.PhysicalDamage,
                MagicalDamage = marker.MagicalDamage,
                TrueDamage = marker.TrueDamage,
                OrginalGoldReward = deathData.GoldReward,
                KillerNetID = deathData.Killer.NetId,
                OtherNetID = deathData.Unit.NetId
            };
        }

        internal static void NotifyAssistEvent(Dictionary<ObjAIBase, AssistMarker> assists, DeathData data)
        {
            if (assists.Count == 0)
            {
                return;
            }

            float assistPercent = 1.0f / assists.Count;
            foreach (var champion in assists.Keys)
            {
                var onDeathAssist = OnDeathAssistConstructor(data, assists[champion]);
                onDeathAssist.PercentageOfAssist = assistPercent;
                _game.PacketNotifier.NotifyOnEvent(onDeathAssist, champion);
            }
        }

        internal static void NotifyDeathEvent(DeathData data, ObjAIBase[] assists = null)
        {
            assists ??= Array.Empty<ObjAIBase>();

            var championDie = new OnChampionDie()
            {
                OtherNetID = data.Killer.NetId,
                GoldGiven = data.GoldReward,
                AssistCount = assists.Length,
            };

            for (int i = 0; i < assists.Length && i < 12; i++)
            {
                championDie.Assists[i] = assists[i].NetId;
            }

            _game.PacketNotifier.NotifyOnEvent(championDie, data.Unit);
        }

        internal static void ProcessKill(DeathData data)
        {
            var map = _game.Map;
            var killed = data.Unit as Champion;
            var killer = data.Killer as Champion;
            if (killed == null || killer == null)
            {
                return;
            }

            int killedKillingSpree = Math.Max(killed.KillSpree, killed.ChampStats.CurrentKillingSpree);
            data.GoldReward = map.MapScript.MapScriptMetadata.ChampionBaseGoldValue;
            if (killed.ChampStats.CurrentKillingSpree > 1)
            {
                data.GoldReward = Math.Min(data.GoldReward * MathF.Pow(7f / 6f, killed.ChampStats.CurrentKillingSpree - 1), map.MapScript.MapScriptMetadata.ChampionMaxGoldValue);
            }
            else if (killed.ChampStats.CurrentKillingSpree == 0 && killed.DeathSpree >= 1)
            {
                data.GoldReward *= 11f / 12f;
                if (killed.DeathSpree > 1)
                {
                    data.GoldReward = Math.Max(data.GoldReward * MathF.Pow(0.8f, killed.DeathSpree / 2), map.MapScript.MapScriptMetadata.ChampionMinGoldValue);
                }
                killed.DeathSpree++;
            }

            if (!map.MapScript.HasFirstBloodHappened)
            {
                data.GoldReward += map.MapScript.MapScriptMetadata.FirstBloodExtraGold;
                map.MapScript.HasFirstBloodHappened = true;
            }

            var assists = new Dictionary<ObjAIBase, AssistMarker>();
            foreach (var assistMarker in killed.EnemyAssistMarkers)
            {
                if (!assists.ContainsKey(assistMarker.Source) && assistMarker.Source is Champion c)
                {
                    assists.Add(c, assistMarker);
                    RecursiveGetAlliedAssists(assists, c, data);
                }
            }

            assists.Remove(killer);
            assists = assists.OrderBy(x => x.Value.StartTime).ToDictionary(x => x.Key, x => x.Value);
            var assistObjArray = assists.Keys.ToArray();

            foreach (var assistant in assistObjArray)
            {
                if (assistant is Champion)
                {
                    ApiEventManager.OnAssist.Publish(assistant, data);
                }
            }

            NotifyAssistEvent(assists, data);
            NotifyDeathEvent(data, assistObjArray);
            NotifyChampionKillEvent(data);
            ProcessKillRewards(killed, killer, assistObjArray, data.GoldReward);
            UpdateKillerStats(killer);
            NotifyKillingSpreeEvents(killer, data);
            NotifyMultiKillEvents(killer, data);
            NotifyShutdownEvents(killer, killed, killedKillingSpree);

            killed.KillSpree = 0;
            killed.ChampStats.CurrentKillingSpree = 0;
            killed.DeathSpree++;
            MultiKillStates.Remove(killed.NetId);
        }

        internal static void NotifyChampionKillEvent(DeathData data)
        {
            _game.PacketNotifier.NotifyOnEvent(new OnChampionKill() { OtherNetID = data.Unit.NetId }, data.Killer);
        }

        internal static void UpdateKillerStats(Champion c)
        {
            c.GoldFromMinions = 0;
            c.ChampStats.Kills++;
            c.DeathSpree = 0;
            c.KillSpree++;
            c.ChampStats.CurrentKillingSpree = c.KillSpree;
            if (c.KillSpree > c.ChampStats.LargestKillingSpree)
            {
                c.ChampStats.LargestKillingSpree = c.KillSpree;
            }
        }

        static void NotifyKillingSpreeEvents(Champion killer, DeathData data)
        {
            var spree = killer.ChampStats.CurrentKillingSpree;
            if (spree < 3)
            {
                return;
            }

            _game.PacketNotifier.NotifyOnEvent(new OnKillingSpree()
            {
                OtherNetID = data.Unit.NetId,
                Ammount = spree
            }, killer);

            IEvent killingSpreeSetEvent = null;
            switch (spree)
            {
                case 3:
                    killingSpreeSetEvent = new OnKillingSpreeSet1();
                    break;
                case 4:
                    killingSpreeSetEvent = new OnKillingSpreeSet2();
                    break;
                case 5:
                    killingSpreeSetEvent = new OnKillingSpreeSet3();
                    break;
                case 6:
                    killingSpreeSetEvent = new OnKillingSpreeSet4();
                    break;
                case 7:
                    killingSpreeSetEvent = new OnKillingSpreeSet5();
                    break;
                default:
                    if (spree >= 8)
                    {
                        killingSpreeSetEvent = new OnKillingSpreeSet6();
                    }
                    break;
            }

            if (killingSpreeSetEvent == null)
            {
                return;
            }

            killingSpreeSetEvent.OtherNetID = data.Unit.NetId;
            _game.PacketNotifier.NotifyOnEvent(killingSpreeSetEvent, killer);
        }

        static void NotifyMultiKillEvents(Champion killer, DeathData data)
        {
            var now = _game.GameTime;
            var multiKillWindowMs = GlobalData.ChampionVariables.TimeForMultiKill > 0
                ? GlobalData.ChampionVariables.TimeForMultiKill * 1000.0f
                : DEFAULT_MULTI_KILL_WINDOW_MS;

            if (!MultiKillStates.TryGetValue(killer.NetId, out var multiKillState))
            {
                multiKillState = new MultiKillState()
                {
                    KillCount = 1,
                    LastKillTime = now
                };
                MultiKillStates[killer.NetId] = multiKillState;
            }
            else if (now - multiKillState.LastKillTime <= multiKillWindowMs)
            {
                multiKillState.KillCount++;
                multiKillState.LastKillTime = now;
            }
            else
            {
                multiKillState.KillCount = 1;
                multiKillState.LastKillTime = now;
            }

            if (multiKillState.KillCount > killer.ChampStats.LargestMultiKill)
            {
                killer.ChampStats.LargestMultiKill = multiKillState.KillCount;
            }

            IEvent multiKillEvent = null;
            switch (multiKillState.KillCount)
            {
                case 2:
                    multiKillEvent = new OnChampionDoubleKill();
                    killer.ChampStats.DoubleKills++;
                    break;
                case 3:
                    multiKillEvent = new OnChampionTripleKill();
                    killer.ChampStats.TripleKills++;
                    break;
                case 4:
                    multiKillEvent = new OnChampionQuadraKill();
                    killer.ChampStats.QuadraKills++;
                    break;
                case 5:
                    multiKillEvent = new OnChampionPentaKill();
                    killer.ChampStats.PentaKills++;
                    break;
                default:
                    if (multiKillState.KillCount >= 6)
                    {
                        multiKillEvent = new OnChampionUnrealKill();
                        killer.ChampStats.UnrealKills++;
                    }
                    break;
            }

            if (multiKillEvent == null)
            {
                return;
            }

            multiKillEvent.OtherNetID = data.Unit.NetId;
            _game.PacketNotifier.NotifyOnEvent(multiKillEvent, killer);
        }

        static void NotifyShutdownEvents(Champion killer, Champion killed, int killedKillingSpree)
        {
            if (killedKillingSpree < 3)
            {
                return;
            }

            _game.PacketNotifier.NotifyOnEvent(new OnKilledUnitOnKillingSpree() { OtherNetID = killed.NetId }, killer);

            IEvent shutdownSetEvent = null;
            switch (killedKillingSpree)
            {
                case 3:
                    shutdownSetEvent = new OnKilledUnitOnKillingSpreeSet1();
                    break;
                case 4:
                    shutdownSetEvent = new OnKilledUnitOnKillingSpreeSet2();
                    break;
                case 5:
                    shutdownSetEvent = new OnKilledUnitOnKillingSpreeSet3();
                    break;
                case 6:
                    shutdownSetEvent = new OnKilledUnitOnKillingSpreeSet4();
                    break;
                case 7:
                    shutdownSetEvent = new OnKilledUnitOnKillingSpreeSet5();
                    break;
                default:
                    if (killedKillingSpree >= 8)
                    {
                        shutdownSetEvent = new OnKilledUnitOnKillingSpreeSet6();
                    }
                    break;
            }

            if (shutdownSetEvent == null)
            {
                return;
            }

            shutdownSetEvent.OtherNetID = killed.NetId;
            _game.PacketNotifier.NotifyOnEvent(shutdownSetEvent, killer);
        }

        internal static void RecursiveGetAlliedAssists(Dictionary<ObjAIBase, AssistMarker> assistMarkers, Champion champ, DeathData deathData)
        {
            foreach (var assist in champ.AlliedAssistMarkers)
            {
                if (!(assist.Source is Champion c))
                {
                    continue;
                }

                if (!assistMarkers.ContainsKey(assist.Source))
                {
                    assistMarkers.Add(assist.Source, assist);
                    RecursiveGetAlliedAssists(assistMarkers, c, deathData);
                }
                else
                {
                    assistMarkers[c].StartTime = assistMarkers[c].StartTime < assist.StartTime ? assist.StartTime : assistMarkers[c].StartTime;
                }
            }
        }

        internal static void ProcessKillRewards(Champion killed, Champion killer, ObjAIBase[] assists, float gold)
        {
            float xpShareFactor = assists.Length + 1;

            killer.AddExperience(GetEXPGrantedFromChampion(killer, killed) / xpShareFactor);
            foreach (var obj in assists)
            {
                if (obj is Champion c)
                {
                    c.AddExperience(GetEXPGrantedFromChampion(c, killed) / xpShareFactor);
                }
            }

            killer.AddGold(killer, gold);
            foreach (var obj in assists)
            {
                if (!(obj is Champion c))
                {
                    continue;
                }

                float assistGold = gold / 2 * (1.0f / assists.Length);
                int killDiff = c.ChampStats.Assists - c.ChampStats.Kills;
                if (killDiff > 0)
                {
                    assistGold += 15 + 15 * MathF.Min(killDiff, 3);
                }
                assistGold = MathF.Min(gold, assistGold);

                c.AddGold(c, assistGold);
                c.ChampStats.Assists++;
            }
        }

        public static float GetEXPGrantedFromChampion(Champion killer, Champion killed)
        {
            int cLevel = killed.Stats.Level;
            float exp = (_game.Map.MapData.ExpCurve[cLevel] - _game.Map.MapData.ExpCurve[cLevel - 1]) * _game.Map.MapData.BaseExpMultiple;

            float levelDifference = cLevel - killer.Stats.Level;

            if (cLevel < 0)
            {
                exp -= exp * MathF.Min(_game.Map.MapData.LevelDifferenceExpMultiple * levelDifference, _game.Map.MapData.MinimumExpMultiple);
            }
            else if (cLevel > 0)
            {
                exp += exp * (levelDifference * EVIL_UNK_CONST_LEVEL_DIFF_BONUS_EXP_MULT);
            }

            return exp;
        }
    }
}
