using System.Collections.Generic;
using LeaguePackets.Game.Common;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    /// <summary>
    /// End-of-game / scoreboard stat block for a champion, sent to its owner via S2C_HeroStats (0x46).
    ///
    /// The client deserializes this as a flat, sequential buffer (see AIHeroStatsNetwork.h
    /// StatsMemBuffer + AIHeroStats.cpp HeroStatsCollection::StatsDataFromNetwork): a leading
    /// int32 length followed by one value per stat, with NO offsets/padding. The stats are read
    /// in the order the client stores them in its std::map&lt;stat-name, ...&gt;, i.e. ASCII-sorted
    /// by stat name. The included set and per-stat types come from the patch data files
    /// DATA/Globals/HeroStatList.txt + CLASSIC_Stats.ini ([HeroStats] section).
    ///
    /// For patch 4.20 CLASSIC this is exactly the 76 stats emitted by <see cref="ToHeroStats"/>
    /// below (300 bytes; the leading int32 is written by S2C_HeroStats.WriteBody). Stats the
    /// server does not track are sent as zero. ID is declared LONGLONG in the .ini but is
    /// serialized as a 4-byte value on the wire (verified against replay 0x46 payloads), so it
    /// is emitted as an Int32 here.
    /// </summary>
    public class ChampionStats
    {
        public int Assists { get; set; }
        public int Kills { get; set; }
        public int DoubleKills { get; set; }
        public int UnrealKills { get; set; }
        public float GoldEarned { get; set; }
        public float GoldSpent { get; set; }
        public int CurrentKillingSpree { get; set; }
        public float LargestCriticalStrike { get; set; }
        public int LargestKillingSpree { get; set; }
        public int LargestMultiKill { get; set; }
        public float LongestTimeSpentLiving { get; set; }
        public float MagicDamageDealt { get; set; }
        public float MagicDamageDealtToChampions { get; set; }
        public float MagicDamageTaken { get; set; }
        public int MinionsKilled { get; set; }
        public int NeutralMinionsKilled { get; set; }
        public int NeutralMinionsKilledInEnemyJungle { get; set; }
        public int NeutralMinionsKilledInTeamJungle { get; set; }
        public int Deaths { get; set; }
        public int PentaKills { get; set; }
        public float PhysicalDamageDealt { get; set; }
        public float PhysicalDamageDealtToChampions { get; set; }
        public float PhysicalDamageTaken { get; set; }
        public int QuadraKills { get; set; }
        public int TeamId { get; set; }
        public float TotalDamageDealt { get; set; }
        public float TotalDamageDealtToChampions { get; set; }
        public float TotalDamageTaken { get; set; }
        public int TotalHeal { get; set; }
        public float TotalTimeCrowdControlDealt { get; set; }
        public float TotalTimeSpentDead { get; set; }
        public int TotalUnitsHealed { get; set; }
        public int TripleKills { get; set; }
        public float TrueDamageDealt { get; set; }
        public float TrueDamageDealtToChampions { get; set; }
        public float TrueDamageTaken { get; set; }
        public int TurretsKilled { get; set; }
        public int BarracksKilled { get; set; }
        public int WardsKilled { get; set; }
        public int WardsPlaced { get; set; }

        /// <summary>
        /// Builds the ordered list of stat values for S2C_HeroStats. The order and types match the
        /// client's expectation for the 4.20 CLASSIC stat definition (ASCII-sorted by stat name).
        /// Feed the result to S2C_HeroStats.WriteData.
        /// </summary>
        public List<HeroStat> ToHeroStats()
        {
            return new List<HeroStat>
            {
                new HeroStatInt32 { Value = Assists },                            // ASSISTS
                new HeroStatInt32 { Value = BarracksKilled },                     // BARRACKS_KILLED
                new HeroStatInt32 { Value = Kills },                              // CHAMPIONS_KILLED
                new HeroStatInt32 { Value = 0 },                                  // CONSUMABLES_PURCHASED
                new HeroStatInt32 { Value = DoubleKills },                        // DOUBLE_KILLS
                new HeroStatFloat { Value = 0 },                                  // EXP
                new HeroStatInt32 { Value = 0 },                                  // FRIENDLY_DAMPEN_LOST
                new HeroStatInt32 { Value = 0 },                                  // FRIENDLY_HQ_LOST
                new HeroStatInt32 { Value = 0 },                                  // FRIENDLY_TURRET_LOST
                new HeroStatFloat { Value = GoldEarned },                         // GOLD_EARNED
                new HeroStatFloat { Value = GoldSpent },                          // GOLD_SPENT
                new HeroStatInt32 { Value = 0 },                                  // HQ_KILLED
                new HeroStatInt32 { Value = 0 },                                  // ID (LONGLONG in .ini, 4 bytes on wire)
                new HeroStatInt32 { Value = 0 },                                  // ITEM0
                new HeroStatInt32 { Value = 0 },                                  // ITEM1
                new HeroStatInt32 { Value = 0 },                                  // ITEM2
                new HeroStatInt32 { Value = 0 },                                  // ITEM3
                new HeroStatInt32 { Value = 0 },                                  // ITEM4
                new HeroStatInt32 { Value = 0 },                                  // ITEM5
                new HeroStatInt32 { Value = 0 },                                  // ITEM6
                new HeroStatInt32 { Value = 0 },                                  // ITEMS_PURCHASED
                new HeroStatInt32 { Value = CurrentKillingSpree },                // KILLING_SPREES
                new HeroStatFloat { Value = LargestCriticalStrike },             // LARGEST_CRITICAL_STRIKE
                new HeroStatInt32 { Value = LargestKillingSpree },               // LARGEST_KILLING_SPREE
                new HeroStatInt32 { Value = LargestMultiKill },                  // LARGEST_MULTI_KILL
                new HeroStatInt32 { Value = 0 },                                  // LEVEL
                new HeroStatFloat { Value = LongestTimeSpentLiving },            // LONGEST_TIME_SPENT_LIVING
                new HeroStatFloat { Value = MagicDamageDealt },                  // MAGIC_DAMAGE_DEALT_PLAYER
                new HeroStatFloat { Value = MagicDamageDealtToChampions },       // MAGIC_DAMAGE_DEALT_TO_CHAMPIONS
                new HeroStatFloat { Value = MagicDamageTaken },                  // MAGIC_DAMAGE_TAKEN
                new HeroStatInt32 { Value = MinionsKilled },                     // MINIONS_KILLED
                new HeroStatString { Value = "" },                                // NAME
                new HeroStatInt32 { Value = NeutralMinionsKilled },              // NEUTRAL_MINIONS_KILLED
                new HeroStatInt32 { Value = NeutralMinionsKilledInEnemyJungle }, // NEUTRAL_MINIONS_KILLED_ENEMY_JUNGLE
                new HeroStatInt32 { Value = NeutralMinionsKilledInTeamJungle },  // NEUTRAL_MINIONS_KILLED_YOUR_JUNGLE
                new HeroStatInt32 { Value = 0 },                                  // NEVER_ENTERED_GAME
                new HeroStatInt32 { Value = Deaths },                            // NUM_DEATHS
                new HeroStatInt32 { Value = PentaKills },                        // PENTA_KILLS
                new HeroStatFloat { Value = PhysicalDamageDealt },              // PHYSICAL_DAMAGE_DEALT_PLAYER
                new HeroStatFloat { Value = PhysicalDamageDealtToChampions },   // PHYSICAL_DAMAGE_DEALT_TO_CHAMPIONS
                new HeroStatFloat { Value = PhysicalDamageTaken },             // PHYSICAL_DAMAGE_TAKEN
                new HeroStatInt32 { Value = 0 },                                  // PING
                new HeroStatInt32 { Value = QuadraKills },                       // QUADRA_KILLS
                new HeroStatInt32 { Value = 0 },                                  // SIGHT_WARDS_BOUGHT_IN_GAME
                new HeroStatInt32 { Value = 0 },                                  // SKIN
                new HeroStatInt32 { Value = 0 },                                  // SPELL1_CAST
                new HeroStatInt32 { Value = 0 },                                  // SPELL2_CAST
                new HeroStatInt32 { Value = 0 },                                  // SPELL3_CAST
                new HeroStatInt32 { Value = 0 },                                  // SPELL4_CAST
                new HeroStatInt32 { Value = 0 },                                  // SUMMON_SPELL1_CAST
                new HeroStatInt32 { Value = 0 },                                  // SUMMON_SPELL2_CAST
                new HeroStatInt32 { Value = 0 },                                  // SUPER_MONSTER_KILLED
                new HeroStatInt32 { Value = TeamId },                            // TEAM
                new HeroStatInt32 { Value = 0 },                                  // TEAMMATE_NEVER_ENTERED_GAME
                new HeroStatFloat { Value = 0 },                                  // TIME_OF_FROM_LAST_DISCONNECT
                new HeroStatFloat { Value = 0 },                                  // TIME_PLAYED
                new HeroStatFloat { Value = 0 },                                  // TIME_SPENT_DISCONNECTED
                new HeroStatFloat { Value = TotalDamageDealt },                 // TOTAL_DAMAGE_DEALT
                new HeroStatFloat { Value = TotalDamageDealtToChampions },      // TOTAL_DAMAGE_DEALT_TO_CHAMPIONS
                new HeroStatFloat { Value = TotalDamageTaken },                // TOTAL_DAMAGE_TAKEN
                new HeroStatInt32 { Value = TotalHeal },                         // TOTAL_HEAL
                new HeroStatFloat { Value = TotalTimeCrowdControlDealt },       // TOTAL_TIME_CROWD_CONTROL_DEALT
                new HeroStatFloat { Value = TotalTimeSpentDead },              // TOTAL_TIME_SPENT_DEAD
                new HeroStatInt32 { Value = TotalUnitsHealed },                 // TOTAL_UNITS_HEALED
                new HeroStatInt32 { Value = TripleKills },                      // TRIPLE_KILLS
                new HeroStatFloat { Value = TrueDamageDealt },                 // TRUE_DAMAGE_DEALT_PLAYER
                new HeroStatFloat { Value = TrueDamageDealtToChampions },      // TRUE_DAMAGE_DEALT_TO_CHAMPIONS
                new HeroStatFloat { Value = TrueDamageTaken },                // TRUE_DAMAGE_TAKEN
                new HeroStatInt32 { Value = TurretsKilled },                    // TURRETS_KILLED
                new HeroStatInt32 { Value = UnrealKills },                      // UNREAL_KILLS
                new HeroStatInt32 { Value = 0 },                                  // VISION_WARDS_BOUGHT_IN_GAME
                new HeroStatInt32 { Value = WardsKilled },                      // WARD_KILLED
                new HeroStatInt32 { Value = WardsPlaced },                      // WARD_PLACED
                new HeroStatInt32 { Value = 0 },                                  // WAS_AFK
                new HeroStatInt32 { Value = 0 },                                  // WAS_AFK_AFTER_FAILED_SURRENDER
                new HeroStatString { Value = "" }                                 // WIN
            };
        }
    }
}
