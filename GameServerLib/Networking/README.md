# GameServer Coordinator Channel

A small, generic, language-agnostic control channel between this GameServer
and **whatever match-coordinator system spawned it**. The wire protocol
lives in [`Protobuf/gameserver_control.proto`](Protobuf/gameserver_control.proto)
and is the entire contract — the GameServer doesn't care what's on the
other end, so any coordinator (lobby system, dedicated matchmaker, embedded
all-in-one launcher, scripted CI harness, …) that speaks this proto is
compatible.

If you're replacing an existing match-coordinator with a new one, copy the
`.proto` file out of this directory into your project, run `protoc` for
your language, and implement the protocol below. The GameServer side does
not need to change.

## Spawn-time contract

The coordinator launches the GameServer with the same two CLI args it
always has — port + config — and nothing else. The coordinator endpoint
is now embedded in the GameInfo JSON the coordinator writes anyway:

```
GameServerConsole \
    --port 45123 \
    --config /tmp/lol-match-42.json
```

The coordinator-control endpoint is described by a top-level
`coordinatorChannel` object inside GameInfo.json. Three fields, all
required when the object is present:

```json
{
  "players":  [ ... ],
  "game":     { ... },
  "gameInfo": { ... },
  "forcedStart": 60,

  "coordinatorChannel": {
    "host":    "127.0.0.1",
    "port":    55566,
    "matchId": 42
  }
}
```

| JSON key   | Type    | Notes                                                                      |
|------------|---------|----------------------------------------------------------------------------|
| `host`     | string  | TCP host to dial (typically `127.0.0.1` for in-process).                   |
| `port`     | int     | TCP port the coordinator is listening on (1–65535). Often OS-assigned, so re-read it from JSON every launch. |
| `matchId`  | int     | Coordinator-supplied match identifier, echoed in every control-channel message. |

If `coordinatorChannel` is absent (or has an unparseable / out-of-range
field), **the GameServer runs in legacy/standalone mode** and never opens
the channel. Existing tooling and direct/manual launches continue to work
without modification.

> **Forward-compat:** the coordinator may add additional optional keys
> inside `coordinatorChannel` in future versions (heartbeat interval,
> auth token, etc.). The GameServer's JSON parser silently ignores
> unknown keys (Newtonsoft.Json default), so new fields don't break old
> GameServers and vice versa.

> **Legacy `--coord-host` / `--coord-port` / `--match-id` CLI flags**
> have been removed. The CLI parser is configured with
> `IgnoreUnknownArguments = true` so older callers that still pass them
> won't error — the args are silently discarded.

## Wire framing

Every message on the TCP connection is framed as:

```
[4 bytes: big-endian uint32 body length] [body bytes…]
```

Body bytes are a serialized `GameServerToCoordinator` (game → coordinator)
or `CoordinatorToGameServer` (the other way). Maximum body length is 1 MiB;
anything larger is treated as wire corruption.

## Connection lifecycle

```
GameServer                             Coordinator
─────────                              ───────────
 spawn (with coordinatorChannel
        embedded in GameInfo.json)
   │
   ├── parse JSON, bind UDP port,
   │   read coordinatorChannel.{host,port,matchId}
   │
   ├── TCP connect ─────────────────►  accept (per-match)
   │
   ├── send Ready{} ────────────────►  ★ NOW route clients to GS port
   │                                     (token registration / proxy
   │                                      activation / etc.)
   │
   ┊                                  ┊
   │  (match plays out;              │
   │   socket stays open)            │
   ┊                                  ┊
   │
   ├── send MatchEnded{} ───────────►  cleanup, decrement active match,
   │                                   notify lobby browser, archive stats
   │
   └── close TCP, exit
```

The coordinator **MUST** wait for `Ready{}` before allowing any LoL client
to reach the GameServer's UDP port. If the coordinator routes clients
before Ready, packets sent during the GameServer's startup window may be
silently dropped (the OS has nothing listening yet) and the LoL client
could show a "couldn't connect, check firewall" popup.

If the GameServer process dies without sending `MatchEnded{}` (crash,
`SIGKILL`, etc), the TCP connection drops without a clean close. The
coordinator should treat the unsolicited drop as an abnormal match end
and clean up tokens / decrement counters accordingly.

## Reference messages

(Full schema with comments in the `.proto` file — this is a quick summary.)

### GameServer → Coordinator

| Message       | When                                                                | Notes                                                                                  |
|---------------|---------------------------------------------------------------------|----------------------------------------------------------------------------------------|
| `Ready`       | Once, immediately after the UDP port is bound                       | Required gate. Echoes match_id + actual port + version string.                         |
| `MatchEnded`  | Once, when the match concludes (any reason)                         | Includes Reason enum, duration in seconds, winning team (1=blue, 2=purple, 0=none).    |
| `Heartbeat`   | Optional, every ~10s                                                | connected_players + elapsed_seconds for coordinator-side telemetry. Coordinator may ignore. |

### Coordinator → GameServer

| Message    | When                                                                  | GameServer response                                                       |
|------------|-----------------------------------------------------------------------|--------------------------------------------------------------------------|
| `Shutdown` | Optional, e.g. admin kick / lobby cancel / server rotation             | Sends `MatchEnded{Reason=SHUTDOWN_REQUESTED}` and exits cleanly.          |

## Forward-compatibility notes

- The `oneof payload` on both message types allows future fields to be
  added without breaking older endpoints. Implementations should always
  handle the `PAYLOAD_NOT_SET` / unknown-case explicitly and skip rather
  than reject.
- The `MatchEnded.Reason` enum is similarly forward-compatible — older
  coordinators that receive a newer reason should treat it as
  `REASON_UNSPECIFIED`.
- `Heartbeat` was not present in the very first version of the channel.
  Coordinators implementing only `Ready` + `MatchEnded` are still
  protocol-compliant; the GameServer never *requires* heartbeats and
  doesn't fault a coordinator that ignores them.

## Operational guidance

- **Concurrency**: each match is its own TCP connection. A coordinator
  hosting multiple concurrent matches will have one accept per spawn;
  runs them on separate per-connection threads or async tasks. The
  GameServer's send path is internally serialized by a mutex and is
  safe to call from any thread.
- **Retries / reconnect**: the GameServer makes ONE connect attempt at
  startup. If it fails, the GameServer logs and continues in standalone
  mode — the match still plays out, but no further coordinator messages
  will be exchanged. Add a coordinator-side timeout if you want to abort
  the match on no-Ready.
- **Localhost-only by design**: the channel uses plain TCP, no TLS. It is
  intended for same-machine coordinator/GameServer pairs (the typical
  setup, since the coordinator spawns the process). Coordinators that
  want to drive remote GameServers should tunnel the connection through
  their own secured channel.
