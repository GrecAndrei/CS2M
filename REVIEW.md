# CS2M Critical Review

Reviewed the `Cooperative Coordination & Spatial Awareness Suite` commit
(`89721cc`) plus the working tree on top of it. The headline: a lot of code
was written, very little of it actually works end-to-end. Most of the
"cooperative" features are stubs, several subsystems reject every input
they receive, and the build is broken at the time of writing.

This is grouped by severity, not file. Each item says where to look.

---

## P0 — Mod doesn't work

### 1. Build is broken
`CS2M.csproj:147` references `System.Reflection.Emit.dll` that the SDK
doesn't copy to `OutputPath`. ILRepack fails.

Fix: add `<PackAssemblies Include="$(OutputPath)\System.Reflection.Emit.dll"/>`
to the `PackAssemblies` item group, or, if it's actually unused, remove the
reference. Check the actual ILRepack behaviour — at least one of the
copies in `PackAssemblies` was probably auto-removed in a recent SDK.

### 2. Seven commands have no `[MessagePackObject]` / `[Key]`
These are produced and consumed by the live code path but will fail to
serialize / deserialize because the `[MessagePackObject]` attribute is
missing and the property keys aren't assigned.

| File | Class | Status |
|---|---|---|
| `CS2M.BaseGame/Commands/FrameCommand.cs:9` | `FrameCommand` | no attributes |
| `CS2M.BaseGame/Commands/SpeedCommand.cs:8` | `SpeedCommand` | no attributes |
| `CS2M.BaseGame/Commands/XPMilestoneCommand.cs:7` | `XPMilestoneCommand` | no attributes |
| `CS2M.BaseGame/Commands/AreaApplyCommand.cs:32` | `AreaApplyCommand` | no attributes |
| `CS2M.BaseGame/Commands/RoadApplyCommand.cs:32` | `RoadApplyCommand` | no attributes |
| `CS2M.BaseGame/Commands/ZoneApplyCommand.cs:32` | `ZoneApplyCommand` | no attributes |
| `CS2M.BaseGame/Commands/BulldozeCommand.cs:5` | `BulldozeCommand` | no attributes |

Worse, the nested snapshots used by Area/Road/Zone also lack attributes:
- `AreaControlPointSnapshot` (AreaApplyCommand.cs:5)
- `RoadControlPointSnapshot` (RoadApplyCommand.cs:5)
- `ZoneControlPointSnapshot` (ZoneApplyCommand.cs:5)

That means **building placement, road building, zoning, bulldozing,
frame sync, speed sync, and XP/milestone sync all silently fail to
serialize**, even if everything else worked.

### 3. Money sync rejects every update
Three layers of bug, all of which independently reject every update:

a. `MoneyCommand.Validate()` (`MoneyCommand.cs:49-52`) requires
`Math.Abs(now - Timestamp) < 60000` (60s). Anything older than 60s is
silently dropped. With network jitter and any saved buffer replay,
legitimate commands will trip this.

b. `MoneyCommandHandler.OnValidatedCommand` (`MoneyCommandHandler.cs:31-36`)
calls `GetAuthorityEpoch()` which returns literal `0`
(`MoneyCommandHandler.cs:86`). Then `command.AuthorityEpoch <= 0` is
always true → all updates logged out.

c. `MoneySyncSystem._authorityEpoch` is never incremented anywhere
(`MoneySyncSystem.cs:41`, only assigned in `ReceiveMoneyUpdate` to the
incoming value, then `command.AuthorityEpoch <= _authorityEpoch` is
true the next time around). The server side never increments, the
client side never accepts.

Net result: money sync does nothing.

### 4. `JoinRequestHandler` always rejects
`JoinRequestHandler.cs:122-131` `GetAndCheckAbsoluteTime` is broken:

```csharp
_lastRequestTime.AddOrUpdate(peerId, now, (id, old) =>
{
    long result = now;          // <-- always returns now
    return result;
});
```

The update factory always returns `now`, so `lastTime` is always the
current tick. Then line 114: `(now - absLastTime) < 500ms` → `0 < 500ms`
→ always true → always throws `SecurityException`. Every join request
is rejected on the first call.

The "if (absLastTime > 0 && ...)" guard doesn't help because `now` is
always > 0. The intent was clearly to track the *previous* timestamp
and reject if too close, but the factory discards the old value.

This means the entire join flow is dead.

### 5. `using var` on `System.Timers.Timer` is a real bug
`System.Timers.Timer` does not stop when it goes out of scope; only when
`Dispose` is called. `using var` calls `Dispose` at end of scope. Three
locations:

- `NetworkManager.cs:155-159` — NAT hole-punch timeout. Disposed when
  `SetupNatConnect` returns, *before* the 10s timer elapses. NAT
  timeout is dead.
- `NetworkManager.cs:234-238` — `Connect()` connect timeout. Same
  disposal. Connection timeout is dead.
- `NetworkManager.cs:532-536` — peer registration timeout (server
  disconnects a peer that didn't preconditions-check within 10s). Same.
  Any pre-auth peer is allowed to stay forever.

The `timeoutTimer.Enabled = false` at `NetworkManager.cs:185` doesn't
matter because the timer is already disposed.

### 6. `FrameCommandHandler.IsValidFrameJump` is inverted
`FrameCommandHandler.cs:26`:

```csharp
if (IsValidFrameJump(command.Frame))
{
    Log.Warn($"Frame jump too large: ...");
    return;
}
```

But `IsValidFrameJump` returns `true` for *valid* jumps
(`FrameCommandHandler.cs:62-64`: `if (diff >= 0 && diff <= MAX_FRAME_JUMP)
return true;`). So **valid frames are rejected**, invalid frames are
accepted. Either the call site should be `if (!IsValidFrameJump(...))`
or the function should be renamed to `IsInvalidFrameJump` and its
returns inverted. The net behaviour: with the current code, any frame
in the `[lastFrame, lastFrame + 300]` range is dropped, and any frame
out of that range (potentially a malicious or replayed value) is
accepted.

### 7. `NetworkManager` is created twice
`LocalPlayer.GetServerInfo` (`LocalPlayer.cs:46`) and
`LocalPlayer.Playing(connectionConfig)` (`LocalPlayer.cs:410`) both
do `new NetworkManager()`. The `NetworkInitializer` already creates
one. So every time you start hosting, you have two `NetManager`
instances (two sockets, two listener sets) — only the one held by
`LocalPlayer._networkManager` is actually started; the other is leaked.
This isn't a crash, but the two-instance pattern combined with the
`NetworkInterface` singleton is confusing and error-prone.

`NetworkInterface.Instance.LocalPlayer` is the same `LocalPlayer`, so
both code paths use the same dispatcher, but the `NetManager` being
"the wrong one" is fragile.

### 8. `_uiSystem` can NRE on first connection failure
`LocalPlayer.cs:56-62`, `99-110` etc. call `_uiSystem.SetJoinErrors(...)`
but `_uiSystem` is only initialised lazily in `OnUpdate` (line 452-455).
If the connection fails before `OnUpdate` has run, NRE. The pattern of
resolving `World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<UISystem>()`
should happen up front in `LocalPlayer` (in `GetServerInfo`), not
deferred.

### 9. `RemotePlayer.PlayerTypeChanged` logs an error on every construction
`RemotePlayer.cs:36-38` calls `Log.ErrorWithStackTrace` whenever
`PlayerType` is set — and the `RemotePlayer` constructor at line 10-17
sets `PlayerType = playerType`, which fires the event. So **every
`new RemotePlayer(...)` logs an error**. That's every player joining
or reconnecting. Critical UX issue — the log is full of stack traces.

### 10. `ModInitializer.Initialize` and `NetworkInitializer.Initialize` are dead
`Mod.cs:42-80` `OnLoad` does the init in-place and never calls
`ModInitializer.Initialize`. `NetworkInitializer` is only called from
`ModInitializer.Initialize`. So the whole "centralised init sequence"
that the comments describe does not run. The actual working init is
`CommandInternal.Instance = new CommandInternal()` (line 58) and
`ModSupport.Instance.Init()` (line 63) — which calls
`CommandInternal.Instance.RefreshModel()`. That part works.

`ModInitializer.Shutdown` calls `world.Dispose()` (line 164) which
would nuke the game's ECS World. Fortunately it's never called from
`Mod.cs:OnDispose`, but the code is one wiring accident away from
destroying the game.

---

## P1 — Architectural problems

### 11. Three command registries, no one knows which is canonical
- `CS2M.Commands.CommandInternal._cmdMapping`
  (`CommandInternal.cs:25-26`) — populated by `RefreshModel()`. Used by
  the actual dispatcher in `NetworkManager.ListenerOnNetworkReceiveEvent`.
- `CS2M.Commands.Handler.Internal.CommandSystem._handlers`
  (`CommandSystem.cs:15`) — populated by `Initialize()` (which is
  itself only called from dead code). `ExecuteCommand` is the only
  consumer, and `ExecuteCommand` is never called.
- `CS2M.API.Commands.CommandRegistry._handlers`
  (`CommandRegistry.cs:16`) — populated by
  `CommandHandlerInitializer.RegisterHandler<T>()` (also called from
  dead `NetworkInitializer.Initialize`). Not used by anyone.

Pick one. Right now `CommandInternal._cmdMapping` is the live one.
Delete the other two and the dead init paths.

### 12. `ModSupport.LoadModConnections` finds handlers in third-party mods
`ModSupport.cs:52-87` walks **all mod assemblies** for
`ModConnection` subclasses. The `BaseGameConnection`
(`BaseGameConnection.cs:6`) is one of them, `Enabled = true`,
`CommandAssemblies = [BaseGameMain.Assembly]`. The command assemblies
list is then used by `CommandInternal.RefreshModel` to find
`CommandHandler` types (`CommandInternal.cs:152-163`).

If a hostile mod adds a class extending `CommandHandler` and is on
the `CommandAssemblies` list (or a benign one slips in), the resolver
will pull in its types and they become part of the wire format. This
is a side door for accidental or malicious wire-format manipulation.

### 13. Two `ConnectionState` enums
`CS2M.API.Networking.ConnectionState` (`ConnectionState.cs:8-54`) and
`CS2M.Networking.ConnectionState` (`NetworkManager.cs:605-616`). Same
underlying integer values, same names for most members, but
`ServerStarting` is local-only and `Disconnecting` is API-only. The
local one shadows the API one inside `CS2M.Networking` because of
namespace lookup rules. Pick one. The API one is the public contract;
make `NetworkManager` use it.

### 14. `BaseCommand` mutable + `SecurityToken` is theatre
`BaseCommand.cs:13` — `CommandType` has a public setter, so a
malicious client can craft a command with an arbitrary `CommandType`
string and confuse the registry / dispatcher. The base class also has
`SecurityToken` (line 33) but **no code reads or writes it anywhere
in the codebase**. Either implement the replay/anti-spoof mechanism
or remove the field.

### 15. Time bases are inconsistent
Three different time encodings in active use:

- `BaseCommand.Timestamp` — `DateTime.UtcNow.Ticks` (100ns ticks).
- `Player.LastActivityTime` — `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` (ms).
- `MoneyCommand.Timestamp` — ms since epoch.
- `FrameCommand.Timestamp` — `Stopwatch.GetTimestamp()` (platform ticks, unitless).
- `CooperativeSyncSystem._lastCursorBroadcastTime` — ms since epoch.

Pick one canonical encoding and stick to it. Mixing them leads to
bugs like "Money updates older than 60s rejected" (issue #3) where
the time base shift is invisible.

### 16. `CooperativeSyncSystem` static state in an ECS system
`CooperativeSyncSystem.cs:50-52` — `_remoteCursors`, `_remotePings`,
`_activityLog` are `static readonly` collections. The system itself
is a `SystemBase` that gets created per World. If the World is
disposed and recreated (new game load), the static state persists
across worlds. Then `OnUpdate` does `Clear()` on PlayerStatus !=
PLAYING (line 80-83), so on joining state transitions the state
disappears. State shouldn't be `static` on an ECS system.

The same is true for the static `LockObj` — one lock shared across
all instances of the system in any world.

### 17. `CooperativeSyncSystem.OnCreate` overwrites registry
`CooperativeSyncSystem.cs:67` — `OnActivityRegistered = (lambda)`,
not `+=`. If any other code (BaseGame, third-party mod) had
registered an activity callback, this clobbers it. The lambda
captures `this` of the destroyed system instance, so the registry
holds a dead reference. Use a proper subscribe/unsubscribe API.

### 18. `Log.Initialize` is never called, but the Log has "initialised" guards
`Mod.cs` never calls `Log.Initialize`. So `_logger` in `Log.cs` is
always null. Then `Log.Info` calls `Logger.Info` which hits the
`Logger` property (`Log.cs:82`) which does
`LogManager.GetLogger("CS2M")` — **a fresh logger instance on
every call**. The configured `LogLevelThreshold` (line 87) is read
from `_logLevelThreadLocal` which is **never set by the
`LoggingLevel` property setter** in the way you'd think — the `set`
writes to it, but the read on line 94 always returns 0, which then
falls through to `DEFAULT_LOG_LEVEL = 2`. So **changing the
log-level dropdown in settings has no effect** other than calling
`Log.SetLoggingLevel` which calls `SetLogLevelInternal` which does
nothing if `_logger` is null. Three layers of "no-op".

### 19. `Log.cs:130` ambiguous overload
Two `Info` methods: `Info(string message)` and
`Info(string message, params object[] context)`. C# overload
resolution picks the non-`params` one for single-arg calls. Good.
But the "context" overload calls `FormatWithContext` and then the
`Log.Info(string)` (line 258). So calling `Log.Info("x", obj)` builds
the context string and then logs it — fine. But the `Info(string)`
overload is the one without context. If you wanted structured
context, use a different name.

There's also `WhenInfo(Func<string>)` (line 277-285) that does the
same as `Info(string)` but with a deferred evaluation. Three ways to
log info. Pick one.

### 20. `ModEventSystem.Publish` fires handlers **twice**
`ModEventSystem.cs:57-97` — `Publish<T>` invokes handlers
synchronously under lock (lines 64-83) and then **queues the same
handlers for async invocation** (lines 85-97). The comment says
"Queue for async processing if needed" but it always queues. Every
subscriber sees the event twice — once sync, once async via
`ProcessEvents`. If this is intentional, it's a very bad design. If
not, it's a bug.

### 21. `ModEventSystem.SubscribeWeak` is a lie
`ModEventSystem.cs:322-325` — comment says "prevents memory leaks"
but the implementation is just `ev => handler(ev)`. The lambda
captures `handler` strongly. The subscriber target and the lambda's
closure object are both strongly rooted. Not a weak reference at
all.

### 22. `ModEventSystem.SubscribeOnce` mutates list during iteration
`ModEventSystem.cs:330-345` — `wrapper` lambda calls
`ModEventSystem.Unsubscribe<T>(wrapper)` from inside its own handler.
`Publish` iterates the `Handlers` list under lock. Unsubscribe calls
`Handlers.Remove(handler)` — this modifies the list while it's being
iterated. C#'s `List<T>.foreach` will throw
`InvalidOperationException: Collection was modified` once it notices
the version bump. You can't unsubscribe from inside a handler on
the same list.

### 23. `JoinRequestHandler` rate-limit state leaks
`JoinRequestHandler.cs:20, 25` — `_throttling` and `_lastRequestTime`
are `ConcurrentDictionary<int, ...>` keyed by peer ID. When a peer
disconnects, nothing removes their entries. Long-running servers
accumulate stale entries forever. Memory leak.

### 24. `PreconditionsCheckHandler._usernameLog` leaks too
`PreconditionsCheckHandler.cs:20` — `ConcurrentBag<string>` that
only ever gets `.Add` called on it. Never read, never cleared. Pure
leak.

### 25. `PreconditionsCheckHandler` doesn't escape the loop
`PreconditionsCheckHandler.cs:57-67` iterates
`NetworkInterface.Instance.PlayerListConnected` under a lock. The
list itself is `List<Player>`, not thread-safe — the lock only
protects `_usernameLog` here, not the list. The iteration can throw
`InvalidOperationException` if a peer is added/removed concurrently
on the network event thread.

### 26. Mark-nonce sets treat `nonce == 0` as a wildcard
Every handler (`BuildingCreateCommandHandler.cs:110-113`,
`AreaApplyCommandHandler.cs:120-123`, `RoadApplyCommandHandler.cs:134-137`,
`ZoneApplyCommandHandler.cs:136-139`, `BulldozeCommandHandler.cs:108-111`)
treats nonce `0` as "no nonce" and always allows the operation.
A malicious client can spam with `nonce=0` and bypass dedup, rate
limits, and the cost of any dedup logic. Make `0` a normal nonce
(or reserve `int.MinValue` for "no nonce" and reject `0`).

### 27. Command handlers mutate the incoming command and rebroadcast
`BuildingCreateCommandHandler.cs:64-65`,
`AreaApplyCommandHandler.cs:64-65`,
`RoadApplyCommandHandler.cs:64-65`,
`ZoneApplyCommandHandler.cs:64-65`,
`BulldozeCommandHandler.cs:65-66`:

```csharp
command.RequestOnly = false;
Command.SendToClients?.Invoke(command);
```

Mutating the received command (and then rebroadcasting it) is risky:
- If a re-send happens for any reason, the second arrival has
  `RequestOnly = false`, but the dedup is keyed on the nonce, not on
  `RequestOnly`, so it gets processed again.
- The original sender's `BuildingCreateCommand` is now mutated.

Make a copy with `RequestOnly = false` and send that.

### 28. Re-entrancy: client re-patch fires on server replies
The Harmony patches split the world into "client" and "server"
roles. On the **client**, when a replication command is received, the
service's `TryReplayApply` calls the tool's `Apply` (e.g.
`AreaSyncService.cs:128-137`). The client's Harmony Prefix fires
again. For `AreaSyncPatch` and `RoadSyncPatch` the
`IsReplayActive` guard prevents the loop. For
`BuildingPlacementPatch`, `BulldozePatch`, and `ZoneSyncPatch`
**there is no `IsReplayActive` check** — the client tries to send
a request back to the server. Infinite feedback loop until the
server's nonce dedup stops it (which it will, but you waste a
packet per replication). Add `IsReplayActive` to all replay-style
patches, and make the flag a single static on a `ReplayScope`
manager.

### 29. No optimistic local apply
`BuildingPlacementPatch.Prefix` (line 30-39) for the client: it
builds a request, sends to server, and **returns `false` to skip the
original `Apply`**. The original `Apply` is what would have placed
the building. So the client UI shows the building ghost while
dragging, but on click, the ghost disappears (because the original
`Apply` was skipped) and nothing happens for ~RTT until the server
replies. ~200ms+ of nothing where the building should be.

Same for `BulldozePatch`, `ZoneSyncPatch`, `AreaSyncPatch`,
`RoadSyncPatch`. Apply locally first, then send; reconcile on
disagreement.

### 30. `NetDebug.Logger = new NetLogWrapper()` set on unloaded logger
`Mod.cs:61` — `NetDebug.Logger` is the LiteNetLib logger, set
**before** the rest of init. The wrapper calls into `Log` (CS2M),
which has `_logger == null` (since `Log.Initialize` isn't called),
so each call goes through `Logger` property → `LogManager.GetLogger(...)`
on every call. Performance + weirdness.

### 31. `_commandTypes` and `_handlers` in `CommandRegistry` are static and never reset
Across disconnects / reloads / re-init, the static dictionaries
accumulate handlers. The `Clear` method exists but isn't called from
anywhere. (Though as noted, this is dead code anyway.)

### 32. `PlayerListConnected` / `PlayerListJoined` are `List<Player>`, not thread-safe
`NetworkInterface.cs:41-46` declares them as `List<Player>`. They are
mutated from:
- network event callbacks (LiteNetLib may invoke callbacks on
  threads other than the main thread depending on your `PollEvents`
  strategy).
- `LocalPlayer` methods called from the game thread.

Iteration on the game thread (e.g. `CooperativeSyncSystem.UpdateUiBindings`
line 684) while a peer disconnects can throw
`InvalidOperationException`. Use `ConcurrentDictionary` or
synchronise explicitly.

### 33. `NetworkInterface` singleton has races
`NetworkInterface.cs:54` — `Instance => _instance ??= new NetworkInterface();`
Two threads can race and both create one. Make this an `Interlocked.CompareExchange`
or just use a static initializer.

### 34. The full MoneySyncSystem replay path is a no-op
`MoneySyncSystem.cs:142-153` `UpdateSmoothedMoney`:
```csharp
double target = _citySystem.moneyAmount;
_smoothedMoney = LinearInterpolate(_smoothedMoney, target, _smoothingAlpha);
```
The "target" is just the local money. So we interpolate the local
money towards the local money — the smoothing does nothing. The
historical replay path in `ProcessMoneyHistory` (line 120-140) is
also never reached in practice because money updates are always
rejected (issue #3). The whole smoothing system is dead code.

### 35. `FrameSyncSystem.SetEffectiveFrame` corrupts simulation
`FrameSyncSystem.cs:165-168`:
```csharp
private void SetEffectiveFrame(uint frame)
{
    _simulationSystem.SetPrivateProperty("frameIndex", frame);
}
```
This **rewrites the client's `SimulationSystem.frameIndex` to the
server's frame value**. Cities: Skylines 2 is a deterministic
simulator; changing the frame index mid-run produces different
simulation results. Even if you wanted to "catch up", the
simulation state at frame N on the server is *not* the same as the
simulation state at frame N produced by running the client
simulation forward from frame 0 — there are RNGs, time-dependent
events, etc. Forcing the client to "snap" to the server frame is
a one-way ticket to desync. The interpolation in
`GetEffectiveFrame` (line 155-163) returns the interpolated value
*if samples are present*, but `SetEffectiveFrame` is what actually
mutates `_simulationSystem`. And `_simulationSystem.frameIndex` is
the authoritative tick; mutating it without rolling back all
dependent state is unsafe.

### 36. `FrameCommandHandler` static counters grow unbounded
`FrameCommandHandler.cs:16-18` and `SpeedCommand.cs:17-18`:
```csharp
private static int _correctionCount;
private static long _totalCorrectionDelta;
```
Never reset. The float precision of `_totalCorrectionDelta`
degrades over a long session. These are debug-only — remove or
gate behind a debug flag.

### 37. `WorldSaveLoadHelper` is a stub that conflicts with `SaveLoadHelper`
`WorldSaveLoadHelper.cs:43-64` `SaveGameAsync` is a stub that
serializes a `GameStateSnapshot` with timestamp + version, but the
real "money"/"buildings" fields are never populated. It also defines
its own `SlicedPacketStream` struct (line 273) that shadows the
class in `SaveLoadHelper.cs:25`. **Delete `WorldSaveLoadHelper`** —
it's not used, and the two `SlicedPacketStream` types are a footgun.

### 38. `SlicedPacketStream` is in the wrong namespace
The class `SlicedPacketStream` (used for world transfer) is in
`CS2M.Helpers`. The struct `SlicedPacketStream` (dead stub) is in
`CS2M.Helpers` too. Both have a `GetSlices()` method but different
signatures. Rename the struct out of existence (delete the file).

### 39. `_uiSystem` is mutated by `OnUpdate` instead of injected
`LocalPlayer.cs:36` does
`_saveLoadHelper = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SaveLoadHelper>()`
in the constructor. The `World` may not exist yet. The `_uiSystem`
lazy-loads in `OnUpdate` (line 452-455) — meaning the
`SetJoinErrors` calls during state transitions before the first
`OnUpdate` will NRE. Move to explicit init.

### 40. `IJsonWritable` chat messages are not escaped
`ChatPanel.cs:35-46` `Message.Write` writes user-supplied text into
JSON without escaping. A username with `"` will produce invalid JSON.
The UI's JSON parser may handle it, but at minimum, malformed JSON
is bad practice.

---

## P2 — Performance / hot-path

### 41. `UpdateUiBindings` runs every frame and allocates a lot
`CooperativeSyncSystem.cs:519-759` is called from `OnUpdate` every
frame. It builds a `StringBuilder` JSON document with:
- Reflection to look up `CameraControllerSystem` in all loaded
  assemblies (line 377-392, duplicated at 454-468).
- Reflection to call `Camera.WorldToScreenPoint` and unpack
  `Vector3` (line 543-595, 632-666, etc.).
- For each remote cursor and ping, a fresh `Activator.CreateInstance`
  for a `Vector3` and `Convert.ToSingle` calls on every field
  read.
- `_activityLog` is read under lock; `RemoveAt(0)` is O(N) when
  trimming (line 131-134).

For 4 players, 80 activities, and 60 FPS, this is measurable.
Cache the assembly type lookups; cache the `MethodInfo` for
`WorldToScreenPoint`; replace `Vector3` reflection with direct
calls (`using UnityEngine;` is already imported).

### 42. `GetLocalCursorTerrainPoint` does heavy reflection on every broadcast
`CooperativeSyncSystem.cs:287-367` runs every ~100ms. Inside, it
uses `ReflectionHelper.GetAttr` for `m_LastRaycastPoint` and then
reflection to read its `m_Position` field. If the path is missing,
it falls back to a 30+ line reflection sequence that pulls
`ScreenPointToRay` from `Camera` via `GetMethod`, then reads
`m_Origin` and `m_Direction` from the resulting `Ray`. **You
already have `using UnityEngine;`; just call `ScreenPointToRay`
directly.** The reflection is "IL2CPP / AOT stripping" workaround
that is not needed in 99% of cases.

### 43. `GetLocalCameraFocusPoint` enumerates assemblies every call
`CooperativeSyncSystem.cs:369-415` does
`AppDomain.CurrentDomain.GetAssemblies()` on every call. Cache the
type once.

### 44. `ResolveUsername` and `IsPeerConnected` are O(N) Linq on every packet
`NetworkInterface.cs:140-156` — `PlayerListConnected.Where().Cast().FirstOrDefault(...)`
runs through the list twice (once for `Any`, once for `FirstOrDefault`)
per packet received. With 8 players this is 8² = 64 iterations per
packet. Use a `Dictionary<int, RemotePlayer>` indexed by peer ID.

### 45. `NetworkManager.SendToServer` allocates via Linq
`NetworkManager.cs:324` — `_netManager.ConnectedPeerList.ToArray()`
allocates on every send. Either use a single-element buffer or
`SendToFirstPeer` if LiteNetLib has one.

### 46. `PlayerCursorCommand` and `MapPingCommand` have no rate limit
These are sent on user input (cursor move, ping button). A client
can spam 10x / second (the throttle in `BroadcastLocalCursor`) —
that's fine. But there's no size cap. The `string ActivePrefab`
field has no length limit; a malicious client could fill it with
megabytes and broadcast. Add a `string` length cap on the wire.

### 47. Static `HashSet` in CooperativeSyncSystem grows
`_activityLog` is capped at 80 (line 131). `_remotePings` is
capped at 6s lifetime (line 100). `_remoteCursors` is *not* capped
except by the broadcast timeout (`now - LastUpdateTime > 4000`).
A malicious client could send a `PlayerCursorCommand` per frame
with a new `TargetPlayerId` to grow the dictionary unbounded. Add
a cap or evict by age.

### 48. `_uiBindingsHelper` reflection on every method invocation
`ReflectionHelper.Call(...)` uses `GetMethod().Invoke(...)` on
every call. Each call site that uses reflection should cache the
`MethodInfo` (e.g. in a static dictionary) rather than re-resolving
on every invocation.

---

## P3 — Security / anti-cheat

### 49. `MapPingCommand.Validate` returns `true`
`MapPingCommand.cs:30-33` — no bounds checking. A client can
broadcast a ping at any 3D position, including ones outside the
map.

### 50. `PlayerCursorCommand.Validate` returns `true`
`PlayerCursorCommand.cs:42-45` — no bounds checking. Cursor can
be at any 3D position; name strings have no length cap.

### 51. `UpdateRemoteCursor` accepts attacker-controlled `TargetPlayerId`
`CooperativeSyncSystem.cs:160-178` writes to
`_remoteCursors[command.TargetPlayerId]`. A client can set
`TargetPlayerId = 0` and the cursor of player 0 gets hijacked.
The server should validate that `TargetPlayerId == peer.PlayerId`.

### 52. Server-side validation of money is naive
`MoneySyncSystem.cs:172-197` `ApplyAntiCheatChecks` only checks
rate-of-change, then `SetMoney(_lastMoney)` reverts if exceeded.
**But this is the server, not the client.** The server's
`_lastMoney` is the server's last accepted value. If a single
game tick produces a legitimate huge delta (e.g. a tech unlock
rewarding 1M), the server reverts to the previous value, and the
**clients see the server's money decrease after the reward**. False
positives break gameplay.

### 53. `MoneyCommand.MaxMoney` cap
`MoneyCommand.cs:46` and `MoneySyncSystem.cs:248` both cap at
1 trillion. Some game setups (mega-cities, mods) can exceed this.
A legit player with 1.001T money will have their money
clamped/reverted by the server. Make the cap configurable or
remove it on the server side (only enforce on the client for
display).

### 54. `ForceUpdateMoney` is public
`MoneySyncSystem.cs:332-346` — `public`, no role check, no auth. If
the chat `/money` slash command is the only entry point that's
fine, but the API should enforce a role check internally.

### 55. `BuildingCreateCommand.Validate` checks quaternion magnitude
`BuildingCreateCommand.cs:79-102` — `magnitude` of `(rx,ry,rz,rw)` in
`[0.9, 1.1]`. **This is the magnitude of a *non-normalized*
quaternion, not the unit test for normalized quaternions.** A
normalized quaternion has magnitude exactly 1.0. The check
`magnitude < 0.9 || magnitude > 1.1` rejects valid normalized
quaternions by the floating-point precision (magnitude 1.0 + 1e-6
is still 1.0; the test should be `> 1.0 + epsilon` or use
`Mathf.Approximately`). Probably works in practice but the band is
too tight.

### 56. No replay protection on the server
The server has `IsReplayActive` in `AreaSyncPatch` and
`RoadSyncPatch` (for client-side). The server has no replay
protection — if a malicious peer sends a `RoadApplyCommand` with
`RequestOnly = true` and the same nonce repeatedly, the server
rejects the duplicate (MarkNonce) but the server still spends CPU
on the request until the dedup catches it. Add a per-peer
rate-limit (a la `JoinRequestHandler`).

### 57. `BaseCommand.SecurityToken` is unused
See #14. Implement or remove.

### 58. No `command.Validate()` calls for many command types
`CommandHandler<TCommand>.InternalHandle` (`CommandHandler.cs:121-131`)
only routes to `Handle(typedCommand)`. The `Validate()` is called
only by the *role-specific* subclasses
(`ClientCommandHandler<T>.Handle` and
`ServerCommandHandler<T>.Handle`). The base game handlers all
extend `CommandHandler<TCommand>` (not the role-specific ones), so
**`Validate()` is never called for the base game commands**. The
server happily applies any `AreaApplyCommand` regardless of
`RequestOnly` (line 46: `if (!command.RequestOnly) return;`
bounces non-requests, but never validates the request itself).

### 59. `PreconditionsSuccessCommand` is sent but no further auth
`PreconditionsCheckHandler.cs:84-85` sends success, then
`PlayerConnected(remotePlayer)` (line 89). The client is now in
`PlayerListConnected` and the server will accept any command from
them. There's no challenge-response, no per-session token, no
proof-of-work. A malicious client can pass preconditions and then
spam.

### 60. Client side: `BuildingCreateCommand.RequestOnly` is attacker-controlled
`BuildingCreateCommandHandler.HandleOnServer` checks
`command.RequestOnly` — but the client sets it. The server should
ignore `RequestOnly` from the wire and treat **every** building
command as a request. (Currently it works because the client
patch sets it to true; but a malicious client could set false and
bypass the dedup nonce check on the server side, since dedup only
runs if `RequestOnly` is true.)

---

## P4 — Misc / cleanup

### 61. `BaseGame/Log.cs` is in the wrong namespace
File `CS2M.BaseGame/Log.cs` declares `namespace CS2M.BaseGame.Systems`.
Either move the file or fix the namespace. Will cause import
confusion for any code that does `using CS2M.BaseGame;` and expects
`Log` to be there.

### 62. `BaseGameMain.cs` is an empty class
`BaseGameMain.cs:1-7` — `public class BaseGameMain {}`. The class
exists only to be referenced by `typeof(BaseGameMain).Assembly` in
`BaseGameConnection.cs:13`. Move the assembly reference to
`typeof(SomeRealType).Assembly` and delete `BaseGameMain.cs`.

### 63. `BuildingSystem.cs` is dead code
`CS2M.BaseGame/Systems/BuildingSystem.cs:1-32` — empty `OnUpdate`,
a query that's not consumed. Not registered in `Mod.cs`. Delete.

### 64. `EconomyInspectorSystem` is committed debug code
`CS2M/Systems/EconomyInspectorSystem.cs:1-80` — runs once at frame
600, logs 50+ lines of internal Game-assembly type names. This is
clearly an exploration tool that was committed by accident. Remove
or gate behind a config flag.

### 65. `CooperativeActivityRegistry` action signature is loose
`CooperativeActivityRegistry.cs:10` —
`Action<string, string, float, float, float>`. Floats for
position loses precision over long distances. Use a `float3` or
`Vector3`. The signature is also positional-only and not
self-describing.

### 66. Build artifacts in repo
- `CS2M.UI/build_error.txt`, `CS2M.UI/build_log.txt`,
  `CS2M.UI/build_log_2.txt` — previous build outputs, should be
  in `.gitignore`.
- `dist/Mods/CS2M/CS2M.mjs`, `CS2M.mjs.LICENSE.txt` — built
  artifacts.
- `tmp_pdx_bundle.js` — what is this? Looks like a generated
  bundle for Paradox.
- `libs/Colossal.Logging.dll` — vendored game DLL, should not be
  in source. Use a NuGet reference or copy in a build target.
- `last_build.log` — debug artifact.

### 67. `_handlers` field on `CommandHandler` (`BaseCommand`) is mis-named
`CommandHandler.cs:21` has `public bool TransactionCmd { get; set; } = true;`.
`TransactionCmd` is never read. `RelayOnServer` (line 26) is also
never read. Dead configuration on the base class.

### 68. `Eco Inspector System` in `ModInitializer` is doubly-registered
`ModInitializer.cs:95` adds `EconomyInspectorSystem` to the world.
`Mod.cs:72` also adds it. Probably idempotent but redundant.

### 69. `FrameCommand` and `MoneyCommand` handler constructors set
`TransactionCmd` and `RelayOnServer` in `BuildingCreateCommandHandler`
etc., but not in `FrameCommandHandler` and `MoneyCommandHandler`.
The defaults (true) are wrong: a server should not relay an
authoritative frame/money command back to itself.

### 70. `CooperativeActivityRegistry` callback captures destroyed instance
See #17. Even after `CooperativeSyncSystem.OnDestroy`, the
`CooperativeActivityRegistry.OnActivityRegistered` still points
at the lambda which captures `this` of the destroyed system.
The lambda calls `RegisterActivity` which mutates a static
dictionary. If a new `CooperativeSyncSystem` is created (e.g. new
game), the new `OnCreate` overwrites the old lambda, so the old
instance's collections stop being updated. **Stale data
accumulation.**

### 71. `UpdateUiBindings` uses `UnityEngine.Camera.main` per cursor
`CooperativeSyncSystem.cs:551` (and similar in the pings block) —
`Camera.main` is a property that does a tag-based lookup. Doing
this per cursor per frame is wasteful. Cache once at the start of
`UpdateUiBindings`.

### 72. `Memory leak in CooperativeSyncSystem` static state
Already noted in #16, but the consequence: world A's activities
remain visible in world B. On a new game load (which destroys and
recreates the World), the static state is reused.

### 73. `CooperativeSyncSystem.OnUpdate` runs `Input.GetKeyDown` from ECS
`CooperativeSyncSystem.cs:88-91` — `Input.GetKeyDown(KeyCode.G)` from
inside an ECS system. ECS systems can run on threads other than
the main thread depending on `World.Flags`. If this system is
ever scheduled on a worker thread, Unity will throw. Add
`[UpdateInGroup(typeof(PresentationSystemGroup))]` or
`RequireForUpdate` semantics to keep it on the main thread.

### 74. `Logger.Info` is wrapped in rate-limit state mutation
`Log.cs:308-323` `RateLimited` uses
`ConcurrentDictionary<string, RateLimitState>` and locks per-key.
The `RateLimitState` is a private nested class. The rate-limit
gates on `Level.Warn` (line 310) — but the caller passes
arbitrary level, not just warn. So you can never use this for
`Log.Info` etc. Comment says "max once per second by default" but
the function only fires for warn-level.

### 75. `LogInfo` overload ambiguity
Already in #19.

### 76. `ReflectionExtensions.SetPrivateProperty` doesn't search base class
`CS2M.BaseGame/Systems/ReflectionExtensions.cs:8-18` — `GetProperty`
and `GetField` on the derived type with
`BindingFlags.NonPublic | BindingFlags.Instance` don't find
inherited private members without
`BindingFlags.FlattenHierarchy`. If `CitySystem` inherits
`_citySystem` from a base class, this will return null and
silently no-op (`prop?.SetValue(obj, value)` is a no-op when
`prop == null`). Add FlattenHierarchy and verify with a
test or runtime check.

### 77. `IPUtil.CACHE_TTL_SECONDS` is dead
`IPUtil.cs:17` — `CACHE_TTL_SECONDS = 300` is defined but
**never used**. The purge on line 136-150 clears everything on a
global interval, not per-entry TTL. The constant is misleading.

### 78. `IPUtil.GetLocalIpAddress` may return `IPAddress.Any`
`IPUtil.cs:185-201` — fallback to `IPAddress.Any` (`0.0.0.0`).
This is **not a valid local IP** — it's the bind-any address.
Returning it from "get local IP" is a footgun for any caller
that uses the result to advertise itself.

### 79. `IPUtil.ValidateHostname` doesn't accept IPv6 brackets or underscores
`IPUtil.cs:115-131` — only `[a-zA-Z0-9.-]`. So `"[::1]"` is
rejected. And many hostnames (in internal/test environments)
have underscores.

### 80. `IPUtil.Dns.GetHostEntry(...).AddressList[0]` is fragile
`IPUtil.cs:81` — takes the first address without checking
family. On a system with IPv6 DNS records, this may return
`::1` (IPv6 loopback), and the connection will fail.

### 81. `CSMWebClient` is legacy and incomplete
`CSMWebClient.cs:1-52` — extends `WebClient` (obsolete), forces
IPv4 with a `BindIPEndPointDelegate`. The status code logic is
questionable (calls `GetWebResponse` on a stored request — this
makes a second request?). Use `HttpClient` (modern) and only
force IPv4 if the server is known not to support v6.

### 82. `DateTimeExtensions.ToUnixTimeMilliseconds` re-implements
`MoneySyncSystem.cs:391-397` — `new DateTimeOffset(dateTime).ToUnixTimeMilliseconds()`.
`DateTimeOffset` already has `ToUnixTimeMilliseconds`. The
extension is a thin wrapper that adds confusion.

### 83. `ModEvents` event bag is mostly unused
`ModEventSystem.cs:202-312` — defines 10 event types. None of
them are `Publish`'d in the codebase. Dead.

### 84. `NetworkingSystem` is a one-liner
`NetworkingSystem.cs:1-20` — `OnUpdate` calls
`NetworkInterface.Instance.OnUpdate()`. The whole system exists
to be scheduled in `PreSimulation` phase. Could just be
`Mod.cs:70` calling the interface directly without an ECS
system. Or keep it but make it actually do something
(heartbeat, latency tracking, etc.).

### 85. `WorldTransferCommand.NewTransfer` debug log
`NetworkManager.cs:306-308` — different log message depending on
`NewTransfer` is fine, but the string interpolation happens
before the call to `Log.Debug`, even if debug is disabled.

### 86. `NetworkManager.SendToClient` doesn't check role
`NetworkManager.cs:293-316` — anyone can call this on the
client side too. Should reject with a warning if
`LocalPlayer.PlayerType == CLIENT`. Same for `SendToAllClients`.

### 87. `StopwatchExtensions.Elapsed` is a passthrough
`Log.cs:443-446` — `Elapsed(this Stopwatch sw) => sw.Elapsed;`.
Pointless. Remove.

### 88. `StopwatchExtensions.StartEvent` is a static field never set
`Log.cs:441` — `public static string StartEvent;`. Unused.

### 89. `FrameCommandHandler._processingTimes` is O(N) on `RemoveAt(0)`
`FrameCommandHandler.cs:79-84` — `RemoveAt(0)` is O(N). Use a
queue or a circular buffer.

### 90. `MoneyCommandHandler._lastUpdateTimes` is O(N) on `RemoveAt(0)`
Same as #89. `MoneyCommandHandler.cs:65-69`.

### 91. `_peerStats` in `NetworkStatistics` is a `ConcurrentDictionary` but accesses are lock-guarded
`NetworkStatistics.cs:13` — `ConcurrentDictionary<int, PeerStats>`.
`RecordBytesSent` (line 27) calls `GetOrAdd` and then locks the
result. This is a common mistake: `GetOrAdd` may return a
different instance on race. The lock is on the wrong object.
Use `ConcurrentDictionary` methods like `AddOrUpdate`.

### 92. `NetworkStatistics` is mostly write-only
`RecordBytesSent`, `RecordPacketSent` are never called. The
counters never grow. `LogCurrentStats` will report zeros. Either
wire it up or delete the class.

### 93. `BaseCommand.Validate` is `virtual` but most overrides are missing
Only `BuildingCreateCommand.Validate`,
`MoneyCommand.Validate` override it. The rest (`MapPingCommand`,
`PlayerCursorCommand`) return `true` literally. The pattern of
"validate is opt-in" is dangerous — defaults to `true` is the
"trust everything" choice. Better: make base `Validate` abstract
or return `false`.

### 94. `_lastReceivedFrame` reset in `Reset()` only
`FrameSyncSystem.cs:227-242` — `_lastReceivedFrame` is reset in
`Reset()`. If `Reset()` is never called, the value persists
forever. Is `Reset()` ever called? No, it's a public method
without callers.

### 95. UI: `cooperative-overlay` binding every frame
`CooperativeSyncSystem.UpdateUiBindings` runs every frame and
calls `UISystem.CooperativeDataBinding?.Update(string)`. The
binding's `Update` may or may not coalesce; if it sends the JSON
to the React UI on every frame, that's 60+ JSON serializations
per second. The React UI must re-parse the JSON, re-render the
cursor list, etc. Throttle to 10Hz or only on data change.

### 96. `LocalPlayer.Inactive` resets state but `NetworkInterface.ResetRemotePlayers` may race
`LocalPlayer.cs:440` — `Inactive()` calls
`NetworkInterface.Instance.ResetRemotePlayers()` which mutates
`PlayerListConnected` and `PlayerListJoined`. If a network event
fires concurrently (peer disconnect on the listener thread), the
list could be in an inconsistent state. Lock around
`ResetRemotePlayers` and the listener's mutation.

---

## File-level index (where the most-buggy things live)

| Path | Severity | What |
|---|---|---|
| `CS2M/Mod.cs` | P0#7-10 | Init flow is wrong; init subsystems never called |
| `CS2M/Helpers/ModInitializer.cs` | P0#10 | Dead init; `world.Dispose()` would nuke the game |
| `CS2M/Helpers/SaveLoadHelper.cs` | P0#2 | World transfer (the working one) |
| `CS2M/Helpers/WorldSaveLoadHelper.cs` | P1#37 | Dead stub; conflicting SlicedPacketStream |
| `CS2M/Networking/NetworkManager.cs` | P0#5, P1#15 | Timers disposed early; dual NetworkManager |
| `CS2M/Networking/NetworkInterface.cs` | P1#32-33 | List race; singleton race |
| `CS2M/Networking/LocalPlayer.cs` | P0#7-8 | Two NetworkManagers; _uiSystem NRE |
| `CS2M/Networking/RemotePlayer.cs` | P0#9 | Logs error on every construction |
| `CS2M/Networking/NetworkingSystem.cs` | P3#84 | One-liner, no real value |
| `CS2M/Networking/ConnectionConfig.cs` | ok | Multiple ctors with overlapping parameters |
| `CS2M/Networking/NetworkInitializer.cs` | P0#10 | Dead; `UpdatePlayerType` is a stub |
| `CS2M/Commands/CommandInternal.cs` | P1#11 | One of three registries |
| `CS2M/Commands/Handler/Internal/CommandSystem.cs` | P1#11 | Another registry; never called |
| `CS2M/Commands/Handler/Internal/JoinRequestHandler.cs` | P0#4 | Always rejects |
| `CS2M/Commands/Handler/Internal/PreconditionsCheckHandler.cs` | P1#23-25 | Username log leak; non-thread-safe iter |
| `CS2M/Commands/Handler/BaseGame/*.cs` | P0#2, P1#26-28 | Many missing attributes; mutate+rebroadcast; no `Validate` |
| `CS2M/Systems/CooperativeSyncSystem.cs` | P1#16-17, P2#41-43 | Static state, registry overwrite, hot-path reflection |
| `CS2M/Systems/EconomyInspectorSystem.cs` | P3#64 | Committed debug code |
| `CS2M/Util/Log.cs` | P1#18-19 | Init never called; logger per call; overloads |
| `CS2M/Util/ModEventSystem.cs` | P1#20-22 | Double-fires; weak ref is a lie; iter-during-mutate |
| `CS2M/Util/IPUtil.cs` | P3#77-80 | CACHE_TTL unused; IPv4/IPv6 handling |
| `CS2M/Util/PreconditionsUtil.cs` | ok | O(N*M) set membership |
| `CS2M/Util/VersionUtil.cs` | P3 | CURRENT_MOD_VERSION unused |
| `CS2M/Util/MessagePackExtensions.cs` | ok | Resolver; `wrappedWriter.context` re-use is suspect |
| `CS2M/Util/CSMWebClient.cs` | P3#81 | Obsolete `WebClient`; questionable status logic |
| `CS2M/Settings/ModSettings.cs` | P1#18 | Level change has no effect |
| `CS2M/UI/ChatPanel.cs` | P3#40, P2 | Slash commands; XSS-style unsanitised messages |
| `CS2M/UI/UISystem.cs` | ok | UI plumbing looks reasonable |
| `CS2M/Helpers/AssemblyHelper.cs` | ok | Walks mod assemblies for handlers |
| `CS2M/Helpers/NetworkStatistics.cs` | P3#91-92 | Counters never written; locking pattern wrong |
| `CS2M/Helpers/ReflectionHelper.cs` | P2#48 | Reflection per call; cache missing |
| `CS2M/Mods/ModCompat.cs` | not read | — |
| `CS2M/Mods/DlcCompat.cs` | not read | — |
| `CS2M/Mods/ModSupport.cs` | P1#12, ok | Walks third-party mods; refreshes data model |
| `CS2M/BaseGameConnection.cs` | ok | Empty handlers (RegisterHandlers/UnregisterHandlers) |
| `CS2M/BaseGame/AreaSyncPatch.cs` | P1#28-29 | Has replay guard; no optimistic local apply |
| `CS2M/BaseGame/AreaSyncService.cs` | P1 | Nonce generator wraparound is wrong |
| `CS2M/BaseGame/BuildingPlacementPatch.cs` | P1#28-29 | No replay guard |
| `CS2M/BaseGame/BulldozePatch.cs` | P1#28-29 | No replay guard |
| `CS2M/BaseGame/RoadSyncPatch.cs` | P1#28-29 | Has replay guard |
| `CS2M/BaseGame/ZoneSyncPatch.cs` | P1#28-29 | No replay guard |
| `CS2M/BaseGame/Commands/*.cs` | P0#2 | Many missing [MessagePackObject] |
| `CS2M/BaseGame/Systems/FrameSyncSystem.cs` | P0#6, P1#35, P1#36 | Inverted validation; corrupts simulation; static counters |
| `CS2M/BaseGame/Systems/MoneySyncSystem.cs` | P0#3, P1#34 | Epoch never increments; smoothing is no-op; `public` cheat vector |
| `CS2M/BaseGame/Systems/TimeSystem.cs` | P0#2 | SpeedCommand has no attributes |
| `CS2M/BaseGame/Systems/XPMilestoneSyncSystem.cs` | P0#2 | XPMilestoneCommand has no attributes |
| `CS2M/BaseGame/Systems/BuildingSystem.cs` | P3#63 | Empty dead class |
| `CS2M/BaseGame/Systems/ReflectionExtensions.cs` | P3#76 | Inherited members not found |
| `CS2M/BaseGame/Log.cs` | P3#61 | Wrong namespace |
| `CS2M/BaseGame/BaseGameMain.cs` | P3#62 | Empty class |
| `CS2M.API/Commands/*.cs` | P0#1, P1#14-15 | `CommandType` mutable; unused security token; dead methods |
| `CS2M.API/Networking/Player.cs` | P1#15, P3 | Inconsistent time bases; `SessionToken` from GetHashCode |
| `CS2M.API/Networking/ConnectionState.cs` | P1#13 | One of two; not the local one |
| `CS2M.API/Chat.cs` | ok | Interface; `MessageType` enum has unused branches |
| `CS2M.API/Connection.cs` | ok | `ModConnection` base class; `Name` should be a property |
| `CS2M.API/CooperativeActivityRegistry.cs` | P1#17, P3#65 | Single-slot registry; loose signature |
| `CS2M.API/Log.cs` | ok | Trivial wrapper |

---

## What's actually good

- The patch flow (Prefix/Postfix with `__state`) is correct in
  shape for `AreaSyncPatch` and `RoadSyncPatch`.
- The `SlicedPacketStream` class in `SaveLoadHelper.cs` is a
  well-formed implementation of `Stream` for save-game transfer.
- The `JoinRequestThrottle` and `IsPeerConnected` flow is correct
  in principle (modulo the broken `GetAndCheckAbsoluteTime`).
- `ModSettings` correctly uses Colossal's settings API.
- The `ChatPanel` UI binding setup is fine.
- The connection / preconditions flow in
  `PreconditionsCheckHandler.HandleOnServer` is well-structured
  (modulo the username leak).
- The transport choice (LiteNetLib) is reasonable for a
  city-builder.

---

## Recommended order of fixes

1. **Build first** — get it compiling, then you can iterate.
2. **Add `[MessagePackObject]` / `[Key]` to the seven commands**
   (#2). Until then, nothing in the live path works.
3. **Fix the join flow** (#4) so the mod can actually accept a
   client.
4. **Fix the `using var` Timers** (#5) so timeouts actually fire.
5. **Fix money epoch** (#3) and **fix the inverted `IsValidFrameJump`**
   (#6) so sync actually progresses.
6. **Fix the dual `NetworkManager`** (#7) and **fix the
   `RemotePlayer` log error** (#9) so the user doesn't see "ERROR"
   on every join.
7. **Delete the dead init paths** (`ModInitializer`,
   `CommandHandlerInitializer.InitializeAll`, `WorldSaveLoadHelper`,
   `EconomyInspectorSystem`, `BuildingSystem`, `BaseGameMain`).
8. **Then** worry about optimistic local apply, replay protection,
   security validation, performance.
