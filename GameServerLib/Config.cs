using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using GameServerCore.Domain;
using GameServerCore.Enums;
using LeagueSandbox.GameServer;
using LeagueSandbox.GameServer.Content;
using LeagueSandbox.GameServer.Inventory;
using LeagueSandbox.GameServer.Logging;
using LeagueSandbox.GameServer.Networking;
using log4net;
using Newtonsoft.Json.Linq;

namespace LeagueSandbox.GameServer
{
    /// <summary>
    /// Class that contains basic game information which is used to decide how the game will function after starting, such as players, their spawns,
    /// the packages which control the functionality of their champions/abilities, and lastly whether basic game mechanics such as 
    /// cooldowns/mana costs/minion spawns should be enabled/disabled.
    /// </summary>
    public class Config
    {
        public List<PlayerConfig> Players { get; private set; }
        public GameConfig GameConfig { get; private set; }
        public ContentManager ContentManager { get; private set; }
        public FeatureFlags GameFeatures { get; private set; }
        public const string VERSION_STRING = "Version 4.20.0.315 [PUBLIC]";
        public static readonly Version VERSION = new Version(4, 20, 0, 315);

        public bool ChatCheatsEnabled { get; private set; }
        public string ContentPath { get; private set; }
        public bool IsDamageTextGlobal { get; private set; }
        public bool ProfilerEnabled { get; private set; }
        public bool TreatBaseSpellAsEmpty { get; private set; }

        public float ForcedStart { get; private set; }

        /// <summary>
        /// Optional coordinator-channel parameters parsed from the top-level
        /// <c>coordinatorChannel</c> JSON object. Null when the coordinator
        /// omitted the field (legacy / standalone launches) — the GameServer
        /// treats null as "no coordinator", same behaviour as pre-channel
        /// builds. See <c>Networking/Protobuf/gameserver_control.proto</c>
        /// for the wire protocol.
        /// </summary>
        public CoordinatorConfig CoordinatorChannel { get; private set; }

        private Config()
        {
        }

        public static Config LoadFromJson(Game game, string json)
        {
            var result = new Config();
            result.LoadConfig(game, json);
            return result;
        }

        public static Config LoadFromFile(Game game, string path)
        {
            var result = new Config();
            result.LoadConfig(game, File.ReadAllText(path));
            return result;
        }

        private void LoadConfig(Game game, string json)
        {
            var data = JObject.Parse(json);

            var gameInfo = data.SelectToken("gameInfo");

            // Defensive null checks
            bool ReadBool(string key, bool defaultValue)
            {
                var token = gameInfo?.SelectToken(key);
                return token != null && token.Type != JTokenType.Null
                    ? (bool)token
                    : defaultValue;
            }
            string ReadString(string key, string defaultValue)
            {
                var token = gameInfo?.SelectToken(key);
                return token != null && token.Type != JTokenType.Null
                    ? (string)token
                    : defaultValue;
            }

            SetGameFeatures(FeatureFlags.EnableCooldowns,   ReadBool("COOLDOWNS_ENABLED",     false));
            SetGameFeatures(FeatureFlags.EnableManaCosts,   ReadBool("MANACOSTS_ENABLED",     false));
            SetGameFeatures(FeatureFlags.EnableLaneMinions, ReadBool("MINION_SPAWNS_ENABLED", false));
            SetGameFeatures(FeatureFlags.EnableDeathTimer,  ReadBool("DEATH_TIMER_ENABLED",   true));
            SetGameFeatures(FeatureFlags.EnableEmpoweredSumsForTesting, ReadBool("EMPOWERED_SUMS_ENABLED", false));
            SetGameFeatures(FeatureFlags.EnableTournamentMode, ReadBool("TOURNAMENT_MODE_ENABLED", false));

            // Read if chat commands are enabled
            ChatCheatsEnabled = ReadBool("CHEATS_ENABLED", true);

            // Read where the content is
            ContentPath = ReadString("CONTENT_PATH", null);

            // Evaluate if content path is correct, if not try to path traversal to find it
            if (string.IsNullOrEmpty(ContentPath) || !Directory.Exists(ContentPath))
            {
                ContentPath = GetContentPath();
            }

            // Read global damage text setting
            IsDamageTextGlobal = ReadBool("IS_DAMAGE_TEXT_GLOBAL", false);

            // CPU profiler: off by default and off when the key is missing.
            ProfilerEnabled = ReadBool("PROFILER_ENABLED", false);

            // When true, the BaseSpell placeholder script (used to fill unused
            // rune/extra/respawn slots on every ObjAIBase) is treated as empty,
            // so its no-op OnUpdate isn't called per tick on every slot.
            // Defaults to true: BaseSpell has no overrides anyway, so skipping
            // it is a free perf win and a major trace declutter.
            TreatBaseSpellAsEmpty = ReadBool("BASESPELL_EMPTY", true);

            // Read the game configuration
            var gameToken = data.SelectToken("game");
            GameConfig = new GameConfig(gameToken);

            Players = new List<PlayerConfig>();

            // Read the player configuration
            var playerConfigurations = data.SelectToken("players");
            foreach (var player in playerConfigurations)
            {
                var playerConfig = new PlayerConfig(player);
                Players.Add(playerConfig);
            }

            ForcedStart = (float)(data.SelectToken("forcedStart") ?? 0) * 1000;

            // ── Coordinator channel ────────────────────────────────────
            // The coordinator may add unknown sibling keys here in future
            // versions (auth tokens, heartbeat intervals, etc); per the
            // forward-compat contract we silently ignore them and pull only
            // the three fields we know about. Newtonsoft.Json's default
            // behaviour is permissive on unknown JSON keys, so no extra
            // configuration is needed.
            var coordToken = data.SelectToken("coordinatorChannel");
            if (coordToken != null && coordToken.Type == JTokenType.Object)
            {
                var host = (string)coordToken.SelectToken("host");
                var port = (int?)coordToken.SelectToken("port") ?? 0;
                var matchId = (int?)coordToken.SelectToken("matchId") ?? 0;
                if (!string.IsNullOrWhiteSpace(host) && port > 0)
                {
                    CoordinatorChannel = new CoordinatorConfig(host, port, matchId);
                }
            }
        }

        public void LoadContent(Game game)
        {
            // Load data package
            ContentManager = ContentManager.LoadDataPackage(game, GameConfig.DataPackage, ContentPath);
            TalentContentCollection.Init(ContentManager);
            foreach (var player in Players)
            {
                player.LoadTalentsAndRunes();
            }
        }

        private string GetContentPath()
        {
            string result = null;

            var executionDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var path = new DirectoryInfo(executionDirectory ?? Directory.GetCurrentDirectory());

            while (result == null)
            {
                if (path == null)
                {
                    break;
                }

                var directory = path.GetDirectories().Where(c => c.Name.Equals("Content")).ToArray();

                if (directory.Length == 1)
                {
                    result = directory[0].FullName;
                }
                else
                {
                    path = path.Parent;
                }
            }

            return result;
        }

        public void SetGameFeatures(FeatureFlags flag, bool enabled)
        {
            // Toggle the flag on.
            if (enabled)
            {
                GameFeatures |= flag;
            }
            // Toggle off.
            else
            {
                GameFeatures &= ~flag;
            }
        }

        public Dictionary<TeamId, Dictionary<int, Dictionary<int, Vector2>>> GetMapSpawns()
        {
            Dictionary<TeamId, Dictionary<int, Dictionary<int, Vector2>>> toReturn = new Dictionary<TeamId, Dictionary<int, Dictionary<int, Vector2>>>();
            foreach (var rawInfo in ContentManager.GetMapSpawns(GameConfig.Map))
            {
                var team = TeamId.TEAM_BLUE;
                if (rawInfo.Key.ToLower().Equals("purple"))
                {
                    team = TeamId.TEAM_PURPLE;
                }

                for (int i = 0; i < rawInfo.Value.Count; i++)
                {
                    for (int j = 0; j < rawInfo.Value[i].Count(); j++)
                    {
                        if (toReturn.ContainsKey(team))
                        {
                            if (toReturn[team].ContainsKey(i + 1))
                            {
                                toReturn[team][i + 1].Add(j + 1, new Vector2((int)((JArray)rawInfo.Value[i][j])[0], (int)((JArray)rawInfo.Value[i][j])[1]));
                            }
                            else
                            {
                                toReturn[team].Add(rawInfo.Value[i].Count(), new Dictionary<int, Vector2>{
                                    { j + 1, new Vector2((int)((JArray)rawInfo.Value[i][j])[0], (int)((JArray)rawInfo.Value[i][j])[1]) } });
                            }
                        }
                        else
                        {
                            toReturn.Add(team, new Dictionary<int, Dictionary<int, Vector2>> { { rawInfo.Value[i].Count(), new Dictionary<int, Vector2> {
                                { j + 1, new Vector2((int)((JArray)rawInfo.Value[i][j])[0], (int)((JArray)rawInfo.Value[i][j])[1]) } } } });
                        }
                    }
                }
            }
            return toReturn;
        }
    }
}

public class MapData
{
    public int Id { get; private set; }
    public Dictionary<string, float> MapConstants { get; private set; }
    /// <summary>
    /// Collection of MapObjects present within a map's room file, with the key being the name present in the room file. Refer to <see cref="MapObject"/>.
    /// </summary>
    public Dictionary<string, MapObject> MapObjects { get; private set; }
    /// <summary>
    /// Collection of MapObjects which represent lane minion spawn positions.
    /// Not present within the room file, therefor it is split into its own collection.
    /// </summary>
    public Dictionary<string, MapObject> SpawnBarracks { get; private set; }
    /// <summary>
    /// Experience required to level, ordered from 2 and up.
    /// </summary>
    public List<float> ExpCurve { get; private set; }
    public float BaseExpMultiple { get; set; }
    public float LevelDifferenceExpMultiple { get; set; }
    public float MinimumExpMultiple { get; set; }
    /// <summary>
    /// Amount of time death should last depending on level (TimeDeadPerLevel, 1-based:
    /// index 0 = Level01). Query through <see cref="GetDeathTime"/>.
    /// </summary>
    public List<float> DeathTimes { get; private set; }
    /// <summary>
    /// Late-game death-timer scaling (DeathTimeScaling section of the map's DeathTimes file):
    /// after StartTime seconds of game time, the base death time scales by PercentIncrease per
    /// full IncrementTime elapsed, capped at PercentCap (a multiplier cap, e.g. 1.5 = ×1.5).
    /// Map1: 1500/60, Map11: 2100/30, Map12: 1440/60 ×1.25 (client inibins, patch 4.20).
    /// </summary>
    public int DeathTimeScalingStartTime { get; set; }
    public int DeathTimeScalingIncrementTime { get; set; }
    public float DeathTimeScalingPercentIncrease { get; set; }
    public float DeathTimeScalingPercentCap { get; set; }
    /// <summary>
    /// Per-level stat growth factor for this map (LEVELS/MapX/StatsProgression.inibin →
    /// "PerLevelStatsFactor"). This is Riot's CharacterRecord::mPerLevelStatsFactor (mac 4.17
    /// CharacterData.cpp:1735), a per-match static shared by ALL units (champions and monsters),
    /// NOT jungle-monster-specific. Index i is the factor for level i+1 (Level1..Level18); consumed
    /// as a per-level-up increment in Stats.GetLevelUpStatValue. All shipped
    /// maps carry the standard curve (0.65 + 0.035*level), so this is data-driven parity rather than
    /// a behaviour change for stock content.
    /// </summary>
    public List<float> StatsProgression { get; private set; }

    /// <summary>
    /// Item ids that cannot be purchased in this map's shop (Items.inibin "UnpurchasableItemList").
    /// Faithful to the client: ItemArray::CreateItemInstnace sets mbIsPurchasable=false for any id in
    /// this set, and ItemShopFoundry rejects the buy (mac 4.17 decomp). This is the ALWAYS-active base
    /// list; per-mode "UnpurchasableItemList_&lt;mutator&gt;" categories live in <see cref="ModeUnpurchasableItems"/>.
    /// </summary>
    public HashSet<int> UnpurchasableItems { get; private set; }

    /// <summary>
    /// Per-mutator unpurchasable-item categories from Items.inibin ("UnpurchasableItemList_ASCENSION",
    /// "_ARAM", ...), keyed by mutator name. The client only applies these when the matching mutator is
    /// in <c>GameStartData::GetMutators()</c>. We have no mutator system yet, so these are parsed and
    /// stored but NOT gated on — wire them in once mutators/game-modes exist (e.g. Map8 = ODIN).
    /// </summary>
    public Dictionary<string, HashSet<int>> ModeUnpurchasableItems { get; private set; }

    /// <summary>
    /// Item ids in this map's "ItemInclusionList". NOTE: this is a shop-DISPLAY (mInStore) gate that the
    /// client only honors when it_UseExplicitItemInclusion (a CVar, off by default) or tutorial mode is
    /// set — it is NOT a purchase gate in normal play (mac 4.17 decomp, ItemArray.cpp:341/426). Stored for
    /// completeness; do not reject buys based on it.
    /// </summary>
    public HashSet<int> ItemInclusionList { get; private set; }

    public MapData(int mapId)
    {
        Id = mapId;
        MapConstants = new Dictionary<string, float>();
        MapObjects = new Dictionary<string, MapObject>();
        SpawnBarracks = new Dictionary<string, MapObject>();
        ExpCurve = new List<float>();
        DeathTimes = new List<float>();
        StatsProgression = new List<float>();
        UnpurchasableItems = new HashSet<int>();
        ModeUnpurchasableItems = new Dictionary<string, HashSet<int>>();
        ItemInclusionList = new HashSet<int>();
    }

    /// <summary>
    /// Populates the per-map shop item lists from a converted Items.inibin (Items.json) ContentFile.
    /// Mirrors ItemArray::CreateItemInstnace (mac 4.17): the base "UnpurchasableItemList" is always
    /// active (=> not buyable); "UnpurchasableItemList_&lt;mutator&gt;" categories are stored per mode but
    /// only take effect once a mutator system applies them; "ItemInclusionList" is a display gate the
    /// client honors only under it_UseExplicitItemInclusion/tutorial, so it is stored but never gated.
    /// </summary>
    public void LoadItemLists(ContentFile itemsFile)
    {
        const string unpurchasablePrefix = "UnpurchasableItemList_";
        foreach (var section in itemsFile.Values.Keys)
        {
            if (section.Equals("UnpurchasableItemList", StringComparison.OrdinalIgnoreCase))
            {
                AddItemIds(itemsFile.Values[section], UnpurchasableItems);
            }
            else if (section.StartsWith(unpurchasablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var mode = section.Substring(unpurchasablePrefix.Length);
                var set = new HashSet<int>();
                AddItemIds(itemsFile.Values[section], set);
                ModeUnpurchasableItems[mode] = set;
            }
            else if (section.Equals("ItemInclusionList", StringComparison.OrdinalIgnoreCase))
            {
                AddItemIds(itemsFile.Values[section], ItemInclusionList);
            }
        }
    }

    /// <summary>
    /// Parses "Item0", "Item1", ... string entries into <paramref name="target"/> as ints, skipping
    /// non-numeric and 0 ids (0 is the client's end-of-list terminator in ReadCFG_I).
    /// </summary>
    private static void AddItemIds(Dictionary<string, string> entries, HashSet<int> target)
    {
        foreach (var value in entries.Values)
        {
            if (int.TryParse(value, out var id) && id != 0)
            {
                target.Add(id);
            }
        }
    }

    /// <summary>
    /// Base death time in seconds for a champion of the given level at the given game time.
    /// Faithful port of AIHeroDeathTimer::Manager::DeathTimes::GetDeathTime (4.17 mac decomp):
    /// TimeDeadPerLevel is 1-based (Level01 = level 1), the scaling increment count is floored,
    /// and PercentCap caps the multiplier itself. Replay-verified against 4.20.0.319 Map11
    /// (rlp c3a95050: 95/104 hero deaths fit exactly incl. the ×1.5 cap at 75s; the misses are
    /// level-tracking gaps of the analysis, all offset by exactly one level).
    /// </summary>
    public float GetDeathTime(int level, float gameTimeSeconds)
    {
        if (DeathTimes.Count == 0)
        {
            return 0.0f;
        }

        float deathTime = DeathTimes[Math.Clamp(level - 1, 0, DeathTimes.Count - 1)];
        if (DeathTimeScalingStartTime > 0 && DeathTimeScalingIncrementTime > 0
            && gameTimeSeconds > DeathTimeScalingStartTime)
        {
            int increments = (int)((gameTimeSeconds - DeathTimeScalingStartTime) / DeathTimeScalingIncrementTime);
            deathTime *= Math.Min(1.0f + DeathTimeScalingPercentIncrease * increments, DeathTimeScalingPercentCap);
        }
        return deathTime;
    }
}

public class GameConfig
{
    public int Map => (int)_gameData.SelectToken("map");
    public string GameMode => _gameData.SelectToken("gameMode").ToString().ToUpper().Replace(" ", string.Empty);
    public string DataPackage => (string)_gameData.SelectToken("dataPackage");

    private JToken _gameData;

    public GameConfig(JToken gameData)
    {
        _gameData = gameData;
    }
}

public class PlayerConfig
{
    // 16 zero bytes — a valid Blowfish key for slots that never handshake (bots).
    private const string BOT_BLOWFISH_KEY = "AAAAAAAAAAAAAAAAAAAAAA==";

    public long PlayerID { get; private set; }
    public string Rank { get; private set; }
    public string Name { get; private set; }
    public string Champion { get; private set; }
    public TeamId Team { get; private set; }
    public short Skin { get; private set; }
    public string Summoner1 { get; private set; }
    public string Summoner2 { get; private set; }
    /// <summary>
    /// Bot difficulty for bot slots (0 = Beginner, 1 = Intermediate; wire:
    /// S2C_CreateHero.SkillLevel). Riot's Beginner co-op replay carries 0 everywhere.
    /// </summary>
    public byte BotDifficulty { get; private set; }
    /// <summary>
    /// Summoner level shown on the loading screen (wire: PlayerLoadInfo.SummonorLevel).
    /// Replay: humans carry their real level (30 in all captures), bots always 1.
    /// </summary>
    public ushort SummonerLevel { get; private set; }
    /// <summary>
    /// Honor crest shown on this player's loading-screen card to ALLIES (wire: PlayerLoadInfo.
    /// AllyBadgeID; the client maps the id via its 4-slot badge atlas,
    /// PlayerConnectionInfoBase::IndexFromBadgeID). Platform data at Riot — config-supplied here.
    /// </summary>
    public short Ribbon { get; private set; }
    /// <summary>
    /// Honor crest shown to ENEMIES (wire: PlayerLoadInfo.EnemyBadgeID — the "Honorable
    /// Opponent" side of the honor split). 0 = none.
    /// </summary>
    public short EnemyRibbon { get; private set; }
    public int Icon { get; private set; }
    public string BlowfishKey { get; private set; }
    public RuneCollection Runes { get; private set; }
    public TalentInventory Talents { get; private set; }
    public string AIScript { get; private set; }
    /// <summary>
    /// Config-declared bot: the server spawns this champion with the bot AI at game start and
    /// never waits for a client on the slot. Riot's wire model for bot slots (4.20 co-op replay
    /// a5347e9d, SynchVersion PlayerLiteInfo): ID=-1, isBot bit set, botName/botSkinName =
    /// champion model, summoner level 1, no elo, icon -1; bots never appear in the
    /// loading-screen roster/rename/reskin packets (only the humans do).
    /// </summary>
    public bool IsBot { get; private set; }

    private JToken _playerData;

    private static ILog _logger = LoggerProvider.GetLogger();
    public PlayerConfig(JToken playerData)
    {
        _playerData = playerData;
        IsBot = ((bool?)playerData.SelectToken("isBot")) ?? false;
        Champion = (string)playerData.SelectToken("champion");

        // A bot entry only requires "isBot", "champion" and "team" — everything else falls back
        // to the replay-observed bot values. Riot bots carry PlayerID -1 on the wire; a config
        // playerId on a bot entry is deliberately ignored (the handshake must never match it).
        PlayerID = IsBot ? -1 : (long)playerData.SelectToken("playerId");
        Rank = (string)playerData.SelectToken("rank") ?? "";
        Name = (string)playerData.SelectToken("name") ?? (IsBot ? Champion : "");

        Team = TeamId.TEAM_PURPLE;
        var teamStr = (string)playerData.SelectToken("team") ?? "";
        if (teamStr.ToLower().Equals("blue"))
        {
            Team = TeamId.TEAM_BLUE;
        }

        Skin = (short?)playerData.SelectToken("skin") ?? 0;
        // All bots in the 4.20 co-op replay share the same summoner pair on the wire:
        // SummonerBoost (hash 105717908) + SummonerSmite (106858133).
        Summoner1 = (string)playerData.SelectToken("summoner1") ?? (IsBot ? "SummonerBoost" : "SummonerFlash");
        Summoner2 = (string)playerData.SelectToken("summoner2") ?? (IsBot ? "SummonerSmite" : "SummonerHeal");
        Ribbon = (short?)playerData.SelectToken("ribbon") ?? 0;
        EnemyRibbon = (short?)playerData.SelectToken("enemyRibbon") ?? 0;
        SummonerLevel = (ushort?)playerData.SelectToken("summonerLevel") ?? (ushort)(IsBot ? 1 : 30);
        BotDifficulty = (byte?)playerData.SelectToken("botDifficulty") ?? 0;
        Icon = (int?)playerData.SelectToken("icon") ?? (IsBot ? -1 : 0);
        BlowfishKey = (string)playerData.SelectToken("blowfishKey") ?? (IsBot ? BOT_BLOWFISH_KEY : null);
        AIScript = (string)playerData.SelectToken("aiScript") ?? "";
    }

    public void LoadTalentsAndRunes()
    {
        Runes = new RuneCollection();
        var runes = _playerData.SelectToken("runes");
        if (runes != null)
        {
            foreach (JProperty runeCategory in runes)
            {
                Runes.Add(Convert.ToInt32(runeCategory.Name), Convert.ToInt32(runeCategory.Value));
            }
        }
        else if (!IsBot)
        {
            _logger.Warn($"No runes found for player {PlayerID}!");
        }

        Talents = new TalentInventory();
        var talents = _playerData.SelectToken("talents");
        if (talents != null)
        {
            foreach (JProperty talent in talents)
            {
                byte level = 1;
                try
                {
                    level = talent.Value.Value<byte>();
                }
                catch
                {
                    _logger.Warn($"Invalid Talent Rank for Talent {talent.Name}! Please use ranks between 1 and {byte.MaxValue}! Defaulting to Rank 1...");
                }
                Talents.Add(talent.Name, level);
            }
        }
    }
}
