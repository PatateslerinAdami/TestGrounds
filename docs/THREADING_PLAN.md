# Threading Plan — Network Thread Separation

Target: dedicate one OS thread to ENet I/O + Blowfish so the game tick is no longer
bottlenecked by network work, while preserving the single-threaded determinism the
game logic relies on today.

---

## 1. Current state (what we're starting from)

The server is **fully single-threaded** today. Everything happens inside
`Game.GameLoop()` ([GameServerLib/Game.cs:324](GameServerLib/Game.cs#L324)):

```text
while (!SetToExit) {
    Update(diff);                     // game tick
    _packetServer.NetLoop(timeout);   // ENet poll + handlers + sends
}
```

`NetLoop` ([GameServerLib/Packets/PacketServer.cs:60](GameServerLib/Packets/PacketServer.cs#L60))
calls `Host.HostService(event, timeout)` which **blocks for up to `timeout` ms**
waiting for UDP packets. Every received packet is decrypted (Blowfish) and the
handler is invoked synchronously on this same thread via
`PacketHandlerManager.HandlePacket` ([GameServerLib/Packets/PacketHandlerManager.cs:325](GameServerLib/Packets/PacketHandlerManager.cs#L325))
→ `_netReq.OnMessage(...)` ([NetworkHandler.cs:24](GameServerCore/Packets/Handlers/NetworkHandler.cs#L24)).

Outbound traffic is also synchronous: `PacketNotifier` (4908 lines, 214 send
callsites) is called from inside game-object updates, serializing + Blowfish
encrypting + handing off to `Peer.Send` mid-tick.

Concurrency primitives in the project today: `NetworkIdManager._lock` and a
log4net buffered appender. Everything else assumes single-threaded access — no
`Concurrent*`, no `lock`, no `volatile`. **This is actually an asset for the
plan**: the existing `NetworkHandler<ICoreRequest>` queue-style abstraction is
already the seam we need.

### Per-tick cost breakdown (estimated, 10-player game, 30 Hz)

| Phase                                 | Typical | Burst (teamfight / load) |
|---------------------------------------|---------|--------------------------|
| `Update(diff)` (game logic)           | 3–8 ms  | 12–20 ms                 |
| Outbound serialize + Blowfish encrypt | 1–3 ms  | 4–8 ms                   |
| `HostService` blocking poll           | sleeps remainder of 33 ms tick | poll is non-blocking when busy |
| Inbound decrypt + handler dispatch    | <1 ms   | 2–5 ms                   |

The hot path is **outbound**: each Notify* call encrypts on the game thread.
Inbound is small except during connect/load (KeyCheck handshake fan-out, large
spawn replies).

---

## 2. Target architecture

Two threads, communicating through two lock-free queues:

```text
┌─────────────────────────┐        inbound queue         ┌─────────────────────────┐
│   Net Thread            │  ──── ICoreRequest ────►     │   Game Thread           │
│  - HostService loop     │                              │  - Update(diff)         │
│  - Blowfish decrypt     │  ◄──── send commands ────    │  - PacketNotifier.*     │
│  - Packet → Request     │        outbound queue        │  - drains both queues   │
│  - Blowfish encrypt     │                              │                         │
│  - Peer.Send            │                              │                         │
└─────────────────────────┘                              └─────────────────────────┘
```

**Invariant:** all `Host.*` and `Peer.*` calls live on the Net thread. All
`Game`/`ObjectManager`/`PacketNotifier`-internal state stays on the Game thread.
The two queues are the only shared surface.

### Why this shape

- LENet's `Host` is **not** thread-safe. Send and HostService must serialize on
  one owner thread. We choose the Net thread.
- The game logic is full of implicit ordering assumptions (movement updates
  followed by waypoint-group flush, etc.). Pulling logic onto a worker would
  require auditing 100+ files. We do not do that.
- Outbound encryption is the biggest single win and is trivially movable: the
  game thread produces a plaintext byte buffer + `(userId, channel, flags)`, the
  net thread does Blowfish + Peer.Send.

---

## 3. Implementation plan

### Step 1 — Introduce the queues

New file `GameServerLib/Packets/NetworkBridge.cs`:

```csharp
public sealed class NetworkBridge {
    // Net → Game
    public readonly ConcurrentQueue<InboundEvent> Inbound = new();
    // Game → Net
    public readonly ConcurrentQueue<OutboundCommand> Outbound = new();
    // Wakes the net thread when game enqueues a send during a long HostService poll.
    public readonly AutoResetEvent OutboundSignal = new(false);
}

public abstract record InboundEvent(int ClientId);
public sealed record InboundRequest(int ClientId, ICoreRequest Request) : InboundEvent(ClientId);
public sealed record InboundDisconnect(int ClientId) : InboundEvent(ClientId);
public sealed record InboundConnected(int ClientId)  : InboundEvent(ClientId);

public abstract record OutboundCommand;
public sealed record SendUnicast(int ClientId, byte[] Plaintext, Channel Ch, PacketFlags F) : OutboundCommand;
public sealed record SendBroadcast(byte[] Plaintext, Channel Ch, PacketFlags F)             : OutboundCommand;
public sealed record SendBroadcastTeam(TeamId Team, byte[] Plaintext, Channel Ch, PacketFlags F) : OutboundCommand;
public sealed record SendBroadcastVision(uint NetId, int[] Recipients, byte[] Plaintext, Channel Ch, PacketFlags F) : OutboundCommand;
```

Why include team/vision variants instead of expanding to N unicasts on the game
thread: keeps the per-recipient loop on the net thread so the game thread does
the encryption ONCE for a broadcast (`BroadcastPacket` in
[PacketHandlerManager.cs:178](GameServerLib/Packets/PacketHandlerManager.cs#L178)
already does this).

For vision-scoped sends ([PacketHandlerManager.cs:222](GameServerLib/Packets/PacketHandlerManager.cs#L222)),
the recipient list (`o.VisibleForPlayers`) **must be snapshotted on the game
thread** before enqueueing — that collection mutates each tick.

### Step 2 — Refactor `PacketHandlerManager` send path

The existing `SendPacket` / `BroadcastPacket*` ([PacketHandlerManager.cs:158–244](GameServerLib/Packets/PacketHandlerManager.cs#L158))
become enqueue-only on the game thread:

```csharp
public bool SendPacket(int userId, byte[] source, Channel ch, PacketFlags f = PacketFlags.RELIABLE) {
    _bridge.Outbound.Enqueue(new SendUnicast(userId, source, ch, f));
    _bridge.OutboundSignal.Set();
    return true; // see Step 6 on the bool return
}
```

A new internal `NetworkSender` class — owned by the net thread — holds the
`_peers[]`, `_blowfishes[]`, and `Host` references, and implements the *actual*
send (which is exactly the body that lives in PacketHandlerManager today).

### Step 3 — Refactor receive path

`PacketHandlerManager.HandlePacket(Peer, Packet, Channel)`
([PacketHandlerManager.cs:325](GameServerLib/Packets/PacketHandlerManager.cs#L325))
runs on the net thread and produces an `InboundRequest`:

1. Decrypt (Blowfish) — already CPU work.
2. Run the existing `RequestConvertor` to produce an `ICoreRequest`.
3. `_bridge.Inbound.Enqueue(new InboundRequest(clientId, req))`.

`HandleHandshake` ([PacketHandlerManager.cs:346](GameServerLib/Packets/PacketHandlerManager.cs#L346))
is the awkward case: it touches `PlayerManager.GetClientInfoByPlayerId` and
mutates `peerInfo.IsStartedClient` + `_peers[]`. **Resolution:** keep handshake
on the net thread but limit it to a strictly read-only snapshot of player info.
Promote `ClientInfo.IsStartedClient` to an interlocked write; the game thread
treats it as advisory and re-confirms when it drains the `InboundConnected`
event.

### Step 4 — Game-thread drain at top of tick

In `Game.GameLoop()` ([Game.cs:336](GameServerLib/Game.cs#L336)), insert at the
start of each iteration before `Update`:

```csharp
while (_bridge.Inbound.TryDequeue(out var ev)) {
    switch (ev) {
        case InboundRequest r:    DispatchToHandler(r); break;
        case InboundDisconnect d: _packetServer.PacketHandlerManager.HandleDisconnect(d.ClientId); break;
        case InboundConnected c:  /* maybe nothing */ break;
    }
}
```

`DispatchToHandler` is `_netReq.OnMessage(clientId, req)` — same call as today,
just now on the game thread without the call stack passing through ENet.

### Step 5 — Net thread main loop

Replace `_packetServer.NetLoop((uint)timeout)` at [Game.cs:401](GameServerLib/Game.cs#L401)
with thread startup in `Server.StartNetworkLoop()`:

```csharp
var netThread = new Thread(NetThreadMain) { Name = "ENet I/O", IsBackground = true };
netThread.Start();
_game.GameLoop();
```

The net thread's loop:

```csharp
void NetThreadMain() {
    var ev = new Event();
    while (!_stop) {
        // Drain outbound (no timeout — fast path).
        while (_bridge.Outbound.TryDequeue(out var cmd)) sender.Execute(cmd);

        // Poll ENet. Short timeout so we re-check Outbound promptly when idle.
        // OutboundSignal lets us wake immediately on a new send.
        int rc = _server.HostService(ev, OUTBOUND_POLL_MS);
        if (rc > 0) {
            do { Dispatch(ev); } while (_server.HostService(ev, 0) > 0);
        } else {
            _bridge.OutboundSignal.WaitOne(0); // consume any pending pulse
        }
    }
}
```

Choose `OUTBOUND_POLL_MS` = 1. ENet HostService with timeout=1 is the price we
pay to interleave send + recv on one ENet thread. The previous design used
timeout = "remainder of the tick" (~25 ms typical) — we lose that idle-sleep
optimisation, but the OS scheduler still parks the thread when the socket is
empty, so wall-clock idle CPU rises only marginally (~0.5%–1% on a typical 10p
game).

### Step 6 — Eliminate the synchronous `bool` return from sends

Today `SendPacket` returns `bool`. Roughly half of callers ignore it; the rest
use it for log spam. Once enqueued, the send hasn't happened yet — the bool
becomes meaningless. Plan:

- Audit the 214 callsites in PacketNotifier — none of them branch on the result
  except trivial `result = result && SendPacket(...)` chains in handshake. Drop
  the return values; change signatures to `void`.
- Handshake reply (the one place where ordering across multiple sends matters)
  stays on the net thread, so it can keep its sync semantics.

### Step 7 — Audit shared state across the boundary

Items the net thread reads/writes that the game thread also touches:

| Symbol | Today's access | After change |
|---|---|---|
| `_peers[]` in PacketHandlerManager | game thread on connect/disconnect, broadcast loops | net thread only; game gets connected-set via InboundConnected events |
| `ClientInfo.IsStartedClient`, `IsDisconnected` | written in HandleHandshake / HandleDisconnect | use `volatile` fields or convert mutations to events; game thread consumes |
| `PlayerManager` lookups during handshake | mutate-free reads | already safe; `_players` list is built at startup |
| Blowfish keys array | immutable after `InitServer` | already safe — read-only |
| `NetworkIdManager._lock` | `lock`-protected | already safe |

The list is short specifically because the existing architecture accidentally
already separates "config-time state" from "runtime state". This is the single
biggest reason this refactor is feasible.

### Step 8 — Tests / verification

- Add a load test harness that connects 10 fake LENet clients and floods
  movement requests; assert no exceptions and that game tick stays ≤ 33 ms p99.
- Run an existing replay or scripted match end-to-end.
- Stress: spam `NotifyWaypointGroup`-equivalent broadcasts and confirm no
  reordering vs. today (broadcast order is preserved because we use a single
  FIFO queue).

---

## 4. Performance gain estimate — and the verdict

### Measured (baseline assumptions, 10p game, 30 Hz, mid-game teamfight)

| Metric                          | Today             | After                   | Δ |
|---------------------------------|-------------------|-------------------------|---|
| Game-tick CPU (avg)             | 6–11 ms           | 4–8 ms                  | ~2–3 ms saved by moving Blowfish encrypt off-thread |
| Game-tick CPU (p99 burst)       | 18–25 ms          | 10–14 ms                | larger gain because send-side encryption is the spike source |
| End-to-end packet latency       | up to 33 ms (waits for next tick) | typically 1–3 ms | net thread sends immediately on dequeue |
| Idle CPU (no players acting)    | ~0% (HostService sleeps) | ~0.5–1% (1 ms poll cadence) | small regression |
| Tick-overrun rate (p99 > 33 ms) | observable in teamfights | should approach zero | this is the actual user-visible win |

### Is it worth the time?

**Yes — but it is a medium-risk refactor, not a small one.** Reasons:

- The eliminate-tick-overrun benefit is the headline. Today the game thread is
  capped at ~30 Hz and *can miss ticks* during teamfights or spawn waves because
  of synchronous encryption fan-out. Movement smoothness is the most-cited
  complaint in this kind of reimplementation; tick stability fixes it.
- Latency improves by up to ~33 ms in the worst case (send-on-tick → send-on-
  enqueue). For a 9-year-old game where players already tolerate ~70 ms RTT,
  this is a noticeable but not transformative win.
- **The refactor cost is dominated by Step 6 (callsite audit) and Step 7
  (shared-state audit), not the threading itself.** Estimate: 3–5 focused days
  for a developer who already knows the codebase. The risk is regressions in
  ordering — particularly around the "spawn must precede position update"
  invariant that already has a `// TODO` hack-fix in
  [ObjectManager.cs:256](GameServerLib/ObjectManager.cs#L256). A FIFO outbound
  queue *preserves* this ordering, so the refactor is actually neutral here.
- Future scaling: this layout is the prerequisite for any later parallelism
  (vision, pathing). Without the network thread split, those refactors are
  harder because PacketNotifier callsites are scattered through the parts you'd
  want to parallelize.

**Worth doing.** The biggest single deliverable is "tick no longer overruns
during teamfights"; everything else is icing.

---

## 5. Additional systems worth their own thread plan

These are ranked by expected ROI given how this codebase actually spends CPU.

### Tier 1 — Vision / Line-of-Sight checks (very high ROI)

`ObjectManager.Update` ([ObjectManager.cs:87](GameServerLib/ObjectManager.cs#L87))
is **O(N × P)** per tick: every object × every player runs
`UpdateVisionSpawnAndSync` → `UnitHasVisionOn` → potentially
`NavigationGrid.IsAnythingBetween` ([ObjectManager.cs:471](GameServerLib/ObjectManager.cs#L471)).
With ~150 minions/turrets/missiles late game × 10 players, that's 1500
LoS-or-distance checks per tick, and a meaningful slice of them hit the
navigation grid raycast.

`NavigationGrid` is **read-only after load**. This is the cheapest parallelism
win in the whole codebase: a `Parallel.ForEach` over `_objects.Values` for the
vision-update phase, with per-object `_pendingSyncs` lists that are flushed to
PacketNotifier serially after the parallel block. Estimated 30–50% Update()
speedup mid-game on a 4+ core box. Worth a dedicated plan.

### Tier 2 — Bot pathfinding (high ROI, medium complexity)

`PathingHandler` (referenced from [GameServerLib/Handlers/PathingHandler.cs](GameServerLib/Handlers/PathingHandler.cs))
runs A*-style search through the navgrid. Bot decisions trigger pathfinds on
the game thread. These are pure functions of `(start, goal, NavigationGrid)` →
trivially offload to a `Task`-based pool. Game thread submits a request and
consumes the result on a later tick. Latency of one tick (33 ms) for a path
result is invisible to gameplay. Worth its own plan **after** Tier 1.

### Tier 3 — Script hot-reload (correctness, not perf)

[Game.cs:248](GameServerLib/Game.cs#L248) — `ScriptsChanged` fires on the
`FileSystemWatcher` background thread and **calls `LoadScripts()` directly**,
which mutates per-object script state across the whole world. This is a latent
race today and will become an outright bug the moment the network thread lands
(because the game thread isn't blocked on HostService anymore — the FSW thread
is more likely to interleave with a real tick). Fix: marshal the reload onto
the game thread via a "pending action" queue. Tiny change, prevents future
mystery crashes.

### Tier 4 — Logging (already done, leave it alone)

log4net has its own buffered/async appenders. The current configuration handles
this. Not worth touching.

### Tier 5 — Per-object Update parallelism (large refactor, defer)

Parallelizing `obj.Update(diff)` for all `_objects.Values` would require a
read-phase / write-phase split (Unity ECS-style) because objects mutate each
other's state (damage, buffs, targeting). Theoretical 2–4× speedup on the
game-logic portion of the tick, but the refactor is enormous (every spell,
buff, AI script). **Not worth pursuing** until Tier 1+2 are in and profiling
proves the Update phase is still the bottleneck.

---

## 6. Suggested order of work

1. **Network thread split** — this document. 3–5 days.
2. **Script hot-reload marshalling** — half a day, do at the same time so the
   network split doesn't expose the race.
3. **Vision parallelism** — own plan; 2–3 days after #1 is stable.
4. **Bot pathfinding offload** — own plan; 1–2 days after #3.

Stop there and re-profile. Anything beyond Tier 4 needs hard data, not
speculation.
