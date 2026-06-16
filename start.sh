#!/bin/bash
# TestGrounds — Aatrox on Summoner's Rift (Map 1, CLASSIC) + auto-launch client
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

# Resolve .dotnet
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

CHAMPION="Soraka"
MAP_ID=1
MAP_NAME="Summoner's Rift"
CLIENT_DIR="/home/n7reny/PycharmProjects/dpsk/program 3/Client"
PROTON="$HOME/.steam/steam/steamapps/common/Proton 10.0/proton"
COMPATDATA="$HOME/.steam/steam/steamapps/compatdata/league420"
GAME_PORT=5119
BLOWFISH="17BLOhi6KZsTtldTsizvHg=="

echo "╔══════════════════════════════════════╗"
echo "║  TestGrounds — Solo Game             ║"
echo "╠══════════════════════════════════════╣"
printf "║  Champion: %-22s ║
" "$CHAMPION"
printf "║  Map:      %-22s ║
" "Map $MAP_ID ($MAP_NAME)"
echo "╚══════════════════════════════════════╝"
echo ""

# Kill any leftover server on the port
fuser -k "${GAME_PORT}/udp" 2>/dev/null || true
sleep 1

# ─── Build ───
echo "[1/4] Building TestGrounds..."
dotnet build --verbosity quiet
echo "  -> Build complete"

# ─── Write GameInfo.json ───
OUT_DIR="GameServerConsole/Settings"
mkdir -p "$OUT_DIR"

cat > "$OUT_DIR/GameInfo.json" << EOF
{
    "players": [
        // Human player — BLUE team, slot 5
        {
            "playerId": 1,
            "blowfishKey": "$BLOWFISH",
            "rank": "CHALLENGER",
            "name": "TestPlayer",
            "champion": "$CHAMPION",
            "team": "BLUE",
            "skin": 0,
            "summoner1": "SummonerFlash",
            "summoner2": "SummonerHeal",
            "ribbon": 2,
            "icon": 0,
            "runes": {
                "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245,
                "6": 5245, "7": 5245, "8": 5245, "9": 5245,
                "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317,
                "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289,
                "28": 5335, "29": 5335, "30": 5335
            },
            "talents": {}
        },
        // BLUE bots (4)
        {
            "playerId": -1, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotBlue1",
            "champion": "Ezreal", "team": "BLUE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        },
        {
            "playerId": -2, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotBlue2",
            "champion": "Ezreal", "team": "BLUE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        },
        {
            "playerId": -3, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotBlue3",
            "champion": "Ezreal", "team": "BLUE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        },
        {
            "playerId": -4, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotBlue4",
            "champion": "Ezreal", "team": "BLUE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        },
        // PURPLE bots (5)
        {
            "playerId": -5, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotPurple1",
            "champion": "Ezreal", "team": "PURPLE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        },
        {
            "playerId": -6, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotPurple2",
            "champion": "Ezreal", "team": "PURPLE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        },
        {
            "playerId": -7, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotPurple3",
            "champion": "Ezreal", "team": "PURPLE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        },
        {
            "playerId": -8, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotPurple4",
            "champion": "Ezreal", "team": "PURPLE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        },
        {
            "playerId": -9, "blowfishKey": "$BLOWFISH", "rank": "DIAMOND", "name": "BotPurple5",
            "champion": "Ezreal", "team": "PURPLE", "skin": 0,
            "summoner1": "SummonerHeal", "summoner2": "SummonerFlash",
            "ribbon": 2, "icon": 0, "aiScript": "EzrealBot",
            "runes": { "1": 5245, "2": 5245, "3": 5245, "4": 5245, "5": 5245, "6": 5245, "7": 5245, "8": 5245, "9": 5245, "10": 5317, "11": 5317, "12": 5317, "13": 5317, "14": 5317, "15": 5317, "16": 5317, "17": 5317, "18": 5317, "19": 5289, "20": 5289, "21": 5289, "22": 5289, "23": 5289, "24": 5289, "25": 5289, "26": 5289, "27": 5289, "28": 5335, "29": 5335, "30": 5335 },
            "talents": { "4111": 1, "4112": 3, "4114": 1, "4122": 3, "4124": 1, "4132": 1, "4134": 3, "4142": 3, "4151": 1, "4152": 3, "4162": 1, "4211": 2, "4213": 2, "4221": 1, "4222": 3, "4232": 1 }
        }
    ],
    "game": {
        "map": $MAP_ID,
        "gameMode": "CLASSIC",
        "dataPackage": "LeagueSandbox-Scripts"
    },
    "gameInfo": {
        "MANACOSTS_ENABLED": true,
        "COOLDOWNS_ENABLED": true,
        "CHEATS_ENABLED": true,
        "MINION_SPAWNS_ENABLED": true,
        "DEATH_TIMER_ENABLED": true,
        "CONTENT_PATH": "../../../../../Content",
        "IS_DAMAGE_TEXT_GLOBAL": false,
        "PROFILER_ENABLED": false,
        "BASESPELL_EMPTY": true
    },
    "forcedStart": 45
}
EOF
echo "  -> GameInfo.json written"

# Strip JSON comments (game Config parser doesn't support them)
python3 -c "
import re, json
with open('$OUT_DIR/GameInfo.json') as f:
    raw = f.read()
stripped = re.sub(r'//.*$', '', raw, flags=re.MULTILINE)
with open('$OUT_DIR/GameInfo.json', 'w') as f:
    f.write(stripped)
" 2>/dev/null || true

# ─── Write GameServerSettings.json (no internal autostart — we handle it in this script) ───
cat > "$OUT_DIR/GameServerSettings.json" << 'EOF'
{
    "autoStartClient": false,
    "clientLocation": "",
    "clientLaunchScriptPath": "",
    "winePrefix": ""
}
EOF

# ─── Start GameServer in background ───
CONFIG_FILE="$SCRIPT_DIR/GameServerConsole/Settings/GameInfo.json"
echo "[2/4] Starting GameServer on port ${GAME_PORT}..."
echo "  -> Config: $CONFIG_FILE"
dotnet run --project GameServerConsole -- --port "$GAME_PORT" --config "$CONFIG_FILE" > /tmp/tg-gameserver.log 2>&1 &
GAME_PID=$!
echo "  -> GameServer PID: $GAME_PID"

# Wait for server to be ready
echo "  -> Waiting for server to bind..."
for i in $(seq 1 30); do
    if ss -uln | grep -q ":${GAME_PORT} "; then
        echo "  -> Server bound to port $GAME_PORT (after ${i}s)"
        break
    fi
    if ! kill -0 $GAME_PID 2>/dev/null; then
        echo "  -> ERROR: GameServer died during startup!"
        tail -20 /tmp/tg-gameserver.log
        exit 1
    fi
    sleep 1
done

# ─── Launch Client via Proton ───
echo "[3/4] Launching League client via Proton..."
if [ -f "$CLIENT_DIR/League of Legends.exe" ]; then
    export STEAM_COMPAT_DATA_PATH="$COMPATDATA"
    export STEAM_COMPAT_CLIENT_INSTALL_PATH="$CLIENT_DIR"
    export PROTON_NO_ESYNC=1
    export PROTON_NO_FSYNC=1

    cd "$CLIENT_DIR"
    "$PROTON" run "./League of Legends.exe" "8394" "LoLLauncher.exe" "" "127.0.0.1 ${GAME_PORT} ${BLOWFISH} 1" > /tmp/tg-client.log 2>&1 &
    CLIENT_PID=$!
    cd "$SCRIPT_DIR"
    echo "  -> Client launching (PID: $CLIENT_PID)"
else
    echo "  -> ⚠️  Client not found at $CLIENT_DIR/League of Legends.exe"
    CLIENT_PID=""
fi

echo ""
echo "[4/4] Ready!"
echo ""
echo "  Champion: $CHAMPION"
echo "  Game:     localhost:${GAME_PORT}"
echo "  Logs:     tail -f /tmp/tg-gameserver.log"
echo ""
echo "  Press Ctrl+C to stop everything"
echo ""

trap "
    echo '';
    echo 'Shutting down...';
    kill $GAME_PID 2>/dev/null;
    [ -n "$CLIENT_PID" ] && kill $CLIENT_PID 2>/dev/null;
    fuser -k ${GAME_PORT}/tcp 2>/dev/null;
    exit 0
" SIGINT SIGTERM

wait
