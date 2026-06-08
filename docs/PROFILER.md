# CPU Profiler

The server has a built-in CPU profiler that records named scopes into a Perfetto-compatible trace file. It produces a flame-chart view of where every tick spends its time, which threads are doing what, and how long individual script callbacks take.

It is off by default. When on, it adds an `if (enabled)` branch per scope (effectively free when disabled) and a few hundred kilobytes of memory per tick for buffered events. Traces are flushed to disk only when the server shuts down cleanly.

---

## Capturing a trace

1. Open `GameServerConsole/Settings/GameInfo.json` (or the bots template) and set:

   ```json
   "PROFILER_ENABLED": true
   ```

2. Launch the server and play (or run bots) for as long as you want to profile. Tracing starts as soon as the game initializes, and every tick is recorded.

3. Exit cleanly (let the game finish or use the normal shutdown path; do not kill -9). On shutdown, the profiler flushes a file named `logs/profile_<yyyy-MM-dd_HH-mm-ss>.perfetto-trace` relative to the binary's working directory.

4. Open [https://ui.perfetto.dev](https://ui.perfetto.dev), click "Open trace file", and pick the `.perfetto-trace`. The trace renders as horizontal thread tracks with nested slices stacked vertically.

If the server crashes mid-game, the trace is lost; the profiler buffers everything in memory and only writes at shutdown. See "Limitations" at the bottom.

---

## How to read a trace

### Thread tracks

You will see one row per OS thread that produced any scope. The two threads always present are:

- **`GameLoop`** the main game thread. Every `Tick N` slice and everything inside it (game logic, scripts, packet handlers dispatched on the game thread) lives here.
- **`ENet I/O`** the dedicated network thread. Shows `NetOutboundDrain N` slices when the game thread queued outbound packets, and `NetDispatch N` slices when received ENet packets are processed.
- **`CoordinatorReader`** appears only when the server is connected to a match coordinator. Each frame received from the coordinator becomes a `CoordinatorFrame:<PayloadCase>` slice.

The thread row labels come from the thread's `.Name` property in the C# code. If you spawn a new thread without setting `Name`, its row will appear as `Thread-<id>`.

### Slice hierarchy

Each slice in a row sits inside its parent slice, forming a call tree. The top-level slice on `GameLoop` is always `Tick N`, where `N` is the tick number (so adjacent ticks never visually merge in Perfetto). Inside `Tick N` you'll typically see:

```
Tick N
├── DrainInboundEvents              (packet handlers from the net thread)
└── Game.Update
    ├── Map.Update
    │   ├── CollisionHandler.Update
    │   ├── PathingHandler.Update
    │   ├── MapScript.Update
    │   └── Surrenders.Update
    ├── ObjectManager.Update
    │   ├── Objects.Update
    │   │   ├── Update:Champion        (one per object, named by C# type)
    │   │   │   ├── AttackableUnit.Timers
    │   │   │   ├── AttackableUnit.Buffs
    │   │   │   │   └── buff:Name.OnUpdate     (one per active buff)
    │   │   │   ├── AttackableUnit.StatsTick   (every 500 ms)
    │   │   │   ├── AttackableUnit.Replication
    │   │   │   ├── AttackableUnit.Move
    │   │   │   ├── script:CharScriptXxx.OnUpdate
    │   │   │   ├── script:XxxBot.OnUpdate
    │   │   │   ├── ObjAI.SpellsUpdate
    │   │   │   │   └── spell:Owner/Name.OnUpdate  (one per spell slot)
    │   │   │   ├── ObjAI.InventoryUpdate
    │   │   │   ├── ObjAI.AssistMarkers
    │   │   │   └── ObjAI.UpdateTarget
    │   │   ├── Update:Minion
    │   │   ├── Update:LaneTurret
    │   │   ├── Update:SpellMissile
    │   │   └── ...
    │   ├── Objects.Cleanup
    │   ├── Objects.VisionAndLateUpdate
    │   ├── PacketNotifier.NotifyOnReplication
    │   ├── PacketNotifier.NotifyWaypointGroup
    │   └── PacketNotifier.NotifyFXCreateGroupBatch
    ├── ProtectionManager.Update
    ├── ChatCommands.Update
    └── GameScriptTimers.Update
```

Click any slice and Perfetto's bottom panel shows its name, category, duration, start time, and the thread it ran on. Right-clicking a slice gives "Zoom to selection" which is the fastest way to drill in.

### Slice naming conventions

The name format encodes what kind of thing is running. Once you know the conventions, scanning a trace is much faster.

| Prefix or shape           | What it represents                                                                                | Example                                              |
| ------------------------- | ------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| `Tick N`                  | One full game-loop iteration on the game thread.                                                  | `Tick 749`                                           |
| `Update:<TypeName>`       | One `obj.Update(diff)` call inside `ObjectManager.Update`, named by the C# type of the object.    | `Update:Champion`, `Update:LaneTurret`               |
| `spell:<Owner>/<Name>.X`  | A spell script callback. Owner is the unit's `Model`; Name is the spell's data name (`SpellName`).| `spell:Ezreal/EzrealMysticShot.OnUpdate`             |
| `buff:<Name>.X`           | A buff script callback. Name is the buff's data name.                                             | `buff:ExhaustDebuff.OnUpdate`                        |
| `script:<Class>.X`        | A script callback that isn't a spell or buff. Class is the runtime C# class.                      | `script:EzrealBot.OnUpdate`, `script:CharScriptZyra.OnUpdate` |
| `NetOutboundDrain N`      | The net thread draining queued outbound packets for tick N.                                       | `NetOutboundDrain 749`                               |
| `NetDispatch N`           | The net thread handling inbound ENet events for tick N.                                           | `NetDispatch 749`                                    |
| `CoordinatorFrame:<Type>` | A frame received from the match coordinator.                                                      | `CoordinatorFrame:Shutdown`                          |

Categories are also encoded on each slice (`game`, `scripts`, `network`). Perfetto's left sidebar lets you toggle category visibility.

### Finding hotspots

A few common workflows:

- **Find the heaviest tick.** Open the trace at the global zoom level, look for visibly wider `Tick N` slices on the `GameLoop` row, zoom into one.
- **Find which object type costs the most.** Zoom into a representative tick, look at the widths of `Update:Champion` vs `Update:LaneTurret` vs `Update:SpellMissile`, etc. Wider equals more time spent.
- **Find a specific spell or buff.** Use Perfetto's search (the magnifying glass icon top-left, or hit `/`). Searches match slice names, so typing `Ezreal` highlights every Ezreal-related slice across the whole trace. Hit Enter to step through matches.
- **Trace a single packet flow.** A user click sends a packet which arrives in `NetDispatch` on the `ENet I/O` row; the handler runs inside the next tick's `DrainInboundEvents` on the `GameLoop` row. Cross-thread causality is not drawn (Chrome JSON doesn't support flow events well), so you correlate by timestamp.

A slice with no children doesn't necessarily mean nothing else ran inside it; it means nothing inside it was instrumented. "Gaps" inside an instrumented parent slice are uninstrumented work. If you want to know what's in a gap, you (or someone else) needs to add `Profiler.Scope` inside it.

---

## How to use it in your code

### The basic shape

Wrap any block of work in a `using` statement that opens a `Profiler.Scope`. When you next run the server with the profiler on, that block will show up as a slice in the trace with whatever name you gave it.

```csharp
using LeagueSandbox.GameServer.Logging;

using (Profiler.Scope("MyHeavyOperation"))
{
    // ... your work ...
}
```

That's the whole API. The string you pass in becomes the slice's name in Perfetto. Anything that runs inside the `using` block is timed.

### You can leave these in your script

The profiler is off by default. When it's off, the `Profiler.Scope` call does effectively nothing; you do not need to wrap it in an `if`, comment it out before merging, or worry that you're slowing the game down. Sprinkle them wherever you find useful.

One small caveat: if your scope name is built with string interpolation (the `$"..."` syntax with values in `{}` braces), C# still builds that string every time, even when the profiler is off. For almost every script this is a non-issue, building a short string is microseconds. The only place it matters is in a tight loop that runs thousands of times per tick, and those usually want a cached name anyway (see "Tips for picking good names" below).

For the technically curious: when disabled, `Profiler.Scope` returns a default struct whose `Dispose` is a no-op. Net runtime cost is one boolean check; no heap allocations.

### Naming the scope

The name is whatever string you pass, that's exactly what shows up in the trace and what people search for. Picking a useful name is the difference between "I can see where the time went" and "there are 47 things called OnUpdate and I have no idea which is which".

A good name has two parts: a prefix that tells the reader what *kind* of work this is, and an identifier that tells them *which instance*. Reuse the existing prefixes (`spell:`, `buff:`, `script:`) when they fit; invent a new one if you have a new kind of work.

```csharp
// Hard to find amid hundreds of similar slices:
using (Profiler.Scope("OnUpdate", "scripts"))

// Easy to find, easy to filter:
using (Profiler.Scope($"spell:{owner.Model}/{spellName}.OnUpdate", "scripts"))
```

The `$"..."` part is C# string interpolation; the `{...}` slots get filled in with runtime values. That's how you get `spell:Ezreal/EzrealMysticShot.OnUpdate` instead of just `OnUpdate`.

If the same name appears many times back-to-back on the same row, Perfetto may visually merge them at low zoom. Including a counter or instance identifier in the name prevents that (this is why `Tick N`, `NetDispatch N` etc. include a counter).

### Categories

The optional second argument is the category. Perfetto's left sidebar lets you hide whole categories at once, which is handy when the trace is busy. The built-ins are `game` (default), `scripts`, and `network`, but categories are just strings; pass any name you like and it will show up automatically.

```csharp
using (Profiler.Scope("LoadHugeAsset", "io"))
{
    // ...
}
```

### If you use ApiEventManager, your script is already profiled

This is the friendly shortcut for anyone writing champion or buff scripts. If your script registers an event handler through `ApiEventManager` like this:

```csharp
ApiEventManager.OnSpellCast.AddListener(this, parent, OnCast);
```

then every fire of `OnCast` automatically shows up in the trace as `spell:<Owner>/<ParentSpellName>.OnCast`. You do **not** need to add a `Profiler.Scope` inside your handler; the dispatcher already wraps it for you. The same is true for buff listeners, they appear as `buff:<Name>.MethodName`.

If your script only ever interacts with the engine through `ApiEventManager` listeners (which most champion scripts do), you may never need to write `Profiler.Scope` yourself, the trace will already show your script's handlers with meaningful names.

For the technically curious: the listener machinery in [GameServerLib/API/ApiEventManager.cs](GameServerLib/API/ApiEventManager.cs) reflects on the callback delegate when you call `AddListener`, derives a friendly name (pattern-matching on the dispatcher's source: `Spell -> spell:Owner/Name`, `Buff -> buff:Name`, anything else -> `script:Class`), caches it on the listener, and wraps the call site with `Profiler.Scope` so there is no per-fire reflection cost.

### Drilling inside a single script

The auto-profiling tells you `script:EzrealBot.OnUpdate` is taking, say, 8 ms per tick. Useful, but it doesn't tell you which part of `OnUpdate` is slow. To see inside, add scopes around the logical phases of your method.

Concrete example from [EzrealBot.cs](Content/LeagueSandbox-Scripts/AIScripts/Champion AI/EzrealBot.cs):

```csharp
using LeagueSandbox.GameServer.Logging;   // add this at the top of the file

public void OnUpdate(float diff)
{
    if (EzrealInstance == null) return;
    _gameTime += diff / 1000f;
    if (_gameTime - _lastUpdateTime < OnUpdateInterval) return;
    _lastUpdateTime = _gameTime;

    using (Profiler.Scope("EzrealBot.LaneSelection", "scripts"))
    {
        if (!_hasSelectedLane && ShouldSelectLanes()) SelectLane();
        if (_isRecallingForLane) HandleRecallForLane();
    }

    using (Profiler.Scope("EzrealBot.Movement", "scripts"))
    {
        // ... movement logic ...
    }

    using (Profiler.Scope("EzrealBot.Combat", "scripts"))
    {
        // ... combat logic ...
    }
}
```

In the trace, `script:EzrealBot.OnUpdate` now has those three child slices nested inside it. You can tell immediately whether the time is going to lane selection, movement, or combat; if movement is the worst offender, you go back and add more scopes inside it to subdivide further.

A few practical notes:

- Prefix the inner scope names with the script's class name (`EzrealBot.LaneSelection`, not bare `LaneSelection`). It keeps related slices easy to scan for and easy to filter in Perfetto's search.
- Scripts are compiled at runtime by [CSharpScriptEngine](GameServerLib/Scripting/CSharp/CSharpScriptEngine.cs). If you have hot reload enabled (see `Game.EnableHotReload`), you can add scopes and save the file; the changes appear on the next captured trace without a server restart.
- You can wrap as little as one expensive function call or as much as a whole block. The cost of adding one scope is microseconds when the profiler is on and zero when it's off, so err on the side of more scopes when you're actively investigating.

### When to add a scope (and when not to)

Add one when:

- You're writing something that runs every tick and you want to see how much it costs.
- You're investigating a specific slowness and want to break a big slice into smaller pieces.
- You're calling into something that might block (file IO, native code, network).
- You have a long script callback and want to know which phase inside it is the expensive one.

Skip one when:

- The work is genuinely tiny (a math op, a few field assignments). The cost of measuring would be bigger than the work itself.
- The block already runs inside another meaningful scope and you have no specific reason to subdivide it.

### Tips for picking good names

- Match the convention table from "How to read a trace". Readers learn to scan for those prefixes: `spell:Owner/Name.Method`, `buff:Name.Method`, `script:Class.Method`.
- Put the identifier of the thing in the name, not just the method. `spell:Ezreal/EzrealMysticShot.OnUpdate` is something a teammate can search for; `Script.OnUpdate` is not.
- If you're rebuilding the same name on every call inside a tight loop (e.g. `$"...{obj.GetType().Name}..."` running thousands of times per tick), build it once and cache it in a field. The `ApiEventManager` listener does exactly this; copy the pattern if you need it.

---

## Configuration knobs

All knobs live in `gameInfo` inside `GameInfo.json` (and the WithBots variant). Each has a default that takes effect when the key is missing, listed alongside the knob below.

### `PROFILER_ENABLED` (default: `false`)

Master switch. When `false`, `Profiler.Init` does no I/O, no thread buffers exist, and every `Profiler.Scope` call short-circuits to a no-op struct. There is no measurable cost to leaving the calls in production code. When `true`, every `Profiler.Scope` records an event into a per-thread in-memory buffer; on shutdown the buffers are serialized to `logs/profile_<timestamp>.perfetto-trace`.

### `BASESPELL_EMPTY` (default: `true`)

Declutter knob for the `BaseSpell` placeholder pattern. Every `ObjAIBase` constructor (see [ObjAIBase.cs:244-263](GameServerLib/GameObjects/AttackableUnits/AI/ObjAIBase.cs#L244-L263)) fills unused rune, extra, respawn, and use slots with a `Spell` instance literally named `BaseSpell`. The script behind it ([Content/.../Global/BaseSpell.cs](Content/LeagueSandbox-Scripts/Characters/Global/BaseSpell.cs)) is an empty placeholder with no overrides.

When `true` (the default), the `HasEmptyScript` check at [Spell.cs:134](GameServerLib/GameObjects/Spell/Spell.cs#L134) treats `BaseSpell` as empty and [Spell.cs:2030](GameServerLib/GameObjects/Spell/Spell.cs#L2030) skips the `Script.OnUpdate(diff)` call. This is a tiny CPU win regardless of whether you're profiling (no-op calls on 30+ slots per unit per tick add up), and when you do capture a trace, you no longer see dozens of identical `spell:Owner/BaseSpell.OnUpdate` slices.

Set it to `false` only if you're specifically investigating placeholder spells (debugging the placeholder pattern itself, or verifying that some `BaseSpell` instance isn't accidentally doing work). In that mode, every placeholder slot reappears in the trace.

### The "declutter knob" pattern in general

`BASESPELL_EMPTY` is one example of a broader pattern: a config flag whose only purpose is to suppress trace noise from instrumented code that you don't currently care about. If you find yourself looking at a trace dominated by some uninteresting recurring slice, the right fix is often a flag like this rather than removing the instrumentation entirely. Other people may still want to see it.

---

## How the system works

This section is for people changing the profiler itself or adding new transport/aggregation features.

### Layout

All the runtime code is in one file: [GameServerLib/Logging/Profiler.cs](GameServerLib/Logging/Profiler.cs). It exposes:

- `Profiler.Enabled` (read-only bool)
- `Profiler.Init(string logDir, bool enabled)` called once during `Game.Initialize`
- `Profiler.Shutdown()` called once at the end of `Game.GameLoop`
- `Profiler.Scope(string name, string category = "game")` returns a `ScopeHandle`
- `Profiler.ScopeHandle` a `readonly struct : IDisposable`

The init/shutdown wiring is in [Game.cs](GameServerLib/Game.cs); the loop instrumentation (Tick, DrainInboundEvents, Game.Update, etc.) is also there. Other instrumentation lives next to the work it measures.

### Per-thread buffers

Each thread that calls `Profiler.Scope` gets a `ThreadBuffer` lazily allocated via `[ThreadStatic]` storage. The buffer is also registered in a `ConcurrentDictionary<int, ThreadBuffer>` keyed by managed thread id, so the flush path can enumerate every thread that produced events. Each thread appends to its own `List<TraceEvent>`; there is no lock on the hot path.

Thread names come from `Thread.CurrentThread.Name`, captured on first use of the buffer. If you spawn a new thread you want to see labelled in Perfetto, set its `Name` before it makes its first `Profiler.Scope` call.

### Timestamps

`Stopwatch.GetTimestamp()` is sampled at `Init` time and subtracted from every later sample to get a delta in `Stopwatch` ticks, then converted to microseconds. Microsecond resolution is plenty for a 30 Hz game (each tick is 33 ms = 33000 microseconds).

### On-disk format

The output is a binary [Perfetto trace](https://perfetto.dev/docs/reference/trace-packet-proto) (the protobuf format Perfetto's tooling treats as native). The schema lives in [GameServerLib/Logging/perfetto_trace.proto](GameServerLib/Logging/perfetto_trace.proto), a verbatim copy of the upstream `perfetto_trace.proto` (the fused single-file schema). Grpc.Tools compiles it at build time, emitting C# classes under the `Perfetto.Protos` namespace.

The profiler uses a small slice of the schema:

- One `TrackDescriptor` `TracePacket` per thread, wrapping a `ThreadDescriptor` with pid, tid, and thread name; this declares the track Perfetto draws as a row.
- Per scope, two `TrackEvent` `TracePacket`s on that thread's track: one with `Type.SliceBegin` at the start timestamp (and the slice name + category), and one with `Type.SliceEnd` at the end timestamp. Perfetto pairs them by track, in order, to form a single nested slice.

Timestamps are nanoseconds (Perfetto's default). Internally we record microseconds (cheap on the hot path) and multiply by 1000 on the way out.

JSON was the earlier format because it was simpler to bootstrap (~50 lines of code, no schema dependency). The switch to protobuf was driven by file size; a 1 GB JSON trace is roughly a 100-200 MB `.perfetto-trace` for the same content, and Perfetto loads it faster too. The `Profiler.Scope` API is unchanged.

### Lifecycle

`Profiler.Init` is called from `Game.Initialize` after `Config` is loaded. If `enabled` is false, `Init` returns immediately without creating the output path or marking `Enabled` true; every subsequent `Profiler.Scope` will short-circuit to `default`.

`Profiler.Shutdown` is called from `Game.GameLoop` after the net thread joins. It sets `Enabled = false` and writes the JSON file. There is no AppDomain.ProcessExit hook; if the process dies abnormally, the trace is lost.

### Auto-naming in ApiEventManager

The listener machinery in [ApiEventManager.cs](GameServerLib/API/ApiEventManager.cs) caches a `ProfileName` per listener at `AddListener` time. The name is derived by:

1. Boxing the callback (a generic `CBType`) to a `Delegate`.
2. Pattern-matching the dispatcher's `source` parameter: `Spell` -> `spell:<Owner>/<SpellName>`, `Buff` -> `buff:<Name>`, anything else -> `script:<DelegateTargetType>`.
3. Appending `.{MethodName}` from `Delegate.Method`.

The dispatch loop then wraps `Call(listener.Callback)` with `Profiler.Scope(listener.ProfileName, "scripts")`. This means every event consumer registered through `ApiEventManager.OnXxx.AddListener(...)` is profiled automatically with no per-script work, and a meaningful name is shown even when many spells share an inherited base class.

If you add a new dispatcher with a new source type and you want better names than `script:<Class>`, extend the pattern match in `Listener`'s constructor.

### Where the existing instrumentation lives

| Layer                         | File                                                                                                                       |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Game loop / tick boundaries   | [Game.cs](GameServerLib/Game.cs)                                                                                           |
| Object iteration / cleanup    | [ObjectManager.cs](GameServerLib/ObjectManager.cs)                                                                         |
| Map systems                   | [Handlers/MapScriptHandler.cs](GameServerLib/Handlers/MapScriptHandler.cs)                                                 |
| Protection                    | [ProtectionManager.cs](GameServerLib/ProtectionManager.cs)                                                                 |
| Net thread                    | [Packets/PacketServer.cs](GameServerLib/Packets/PacketServer.cs)                                                           |
| Coordinator thread            | [Networking/CoordinatorClient.cs](GameServerLib/Networking/CoordinatorClient.cs)                                           |
| Per-unit update internals     | [GameObjects/AttackableUnits/AttackableUnit.cs](GameServerLib/GameObjects/AttackableUnits/AttackableUnit.cs), [.../ObjAIBase.cs](GameServerLib/GameObjects/AttackableUnits/AI/ObjAIBase.cs) |
| Direct script lifecycle calls | [GameObjects/Spell/Spell.cs](GameServerLib/GameObjects/Spell/Spell.cs), [GameObjects/Buff.cs](GameServerLib/GameObjects/Buff.cs)                                                 |
| Auto-named listener dispatch  | [API/ApiEventManager.cs](GameServerLib/API/ApiEventManager.cs)                                                             |

When you add a major new system, instrument its top-level tick entry point at minimum; add deeper scopes only when you have a concrete reason.

---

## Limitations

- **Buffered, not streamed.** Events live in memory until `Shutdown`. A process crash loses the entire trace. If this becomes a real problem, the cheapest mitigation is a periodic flush from `Game.GameLoop` (every N ticks or every M seconds); the cost is a small IO hitch on the flush tick.
- **No cross-thread causality.** Chrome JSON supports "flow" events for connecting work across threads, but the writer here doesn't emit them. Net thread -> game thread handoffs are visible by timestamp but not visually connected.
- **No CPU sampling.** This is an instrumentation profiler, not a sampling profiler. Gaps inside instrumented parents are blind spots; you fix that by adding more scopes. For wall-clock micro-optimization at the assembly level, use `dotnet-trace` or `perf` instead.
- **No thread CPU time.** Every duration is wall-clock. If a thread sleeps inside an instrumented scope, the scope shows the full elapsed time, not the CPU time. Mostly fine for game-loop work, but worth knowing when you see a multi-millisecond slice that "shouldn't be that slow".
- **File size scales with time.** A 30 Hz match with hundreds of scopes per tick produces a `.perfetto-trace` growing at roughly 100-200 KB per minute (was ~1 MB per minute in the old JSON format). Multi-hour traces are still hefty; if you need them slimmer still, the next lever is enabling string interning via Perfetto's `InternedData`, which the writer in [Profiler.cs](GameServerLib/Logging/Profiler.cs) does not currently use.
