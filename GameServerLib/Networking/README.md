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

## Spawn-time CLI contract

The coordinator passes its control endpoint to the GameServer via three
additional CLI args on the existing `GameServerConsole` invocation:

| Arg            | Type   | Notes                                                    |
|----------------|--------|----------------------------------------------------------|
| `--coord-host` | string | TCP host to dial (typically `127.0.0.1` for in-process). |
| `--coord-port` | int    | TCP port the coordinator is listening on.                |
| `--match-id`   | int    | Coordinator-supplied match identifier.                   |

All three are optional. **If `--coord-host` is empty the GameServer runs in
legacy/standalone mode** and never opens the channel. Existing tooling and
direct/manual launches continue to work without modification.

Example (from a coordinator that has just picked free port `45123` for
this match's UDP traffic and is listening on TCP `127.0.0.1:5500` for
the control channel):

```
GameServerConsole \
    --port 45123 \
    --config /tmp/lol-match-42.json \
    --coord-host 127.0.0.1 \
    --coord-port 5500 \
    --match-id 42
```

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
 spawn (with --coord-* args)
   │
   ├── parse JSON, bind UDP port
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
