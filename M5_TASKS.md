# M5 Task List - Gameplay Surface Expansion

Status legend:
- `[ ]` pending
- `[-]` in progress
- `[x]` done

Updated: 2026-02-06

## Execution Order

1. `[-]` Bulldoze: authoritative request/apply/replicate path.
2. `[-]` Zoning: authoritative request/apply/replicate path.
3. `[-]` Roads: authoritative request/apply/replicate path.

## Detailed Work Items

### 1) Bulldoze
- `[x]` Add bulldoze command payload with entity identity and nonce.
- `[x]` Add bulldoze Harmony patch around `BulldozeToolSystem.Apply`.
- `[x]` Add client request forwarding + local apply suppression.
- `[x]` Add server local apply replication command emission.
- `[x]` Add handler with nonce dedupe and role-based request/replication handling.
- `[x]` Add bulldoze apply service that queues `CreationDefinition` with `CreationFlags.Delete`.
- `[x]` Guard against unsynchronized drag/multi-selection bulldoze by blocking it for now.
- `[ ]` Validate single-object bulldoze in host/client runtime session.
- `[ ]` Validate drag/multi-selection bulldoze behavior.
- Files:
  - `CS2M.BaseGame/Commands/BulldozeCommand.cs`
  - `CS2M/BaseGame/BulldozePatch.cs`
  - `CS2M/BaseGame/BulldozeService.cs`
  - `CS2M/Commands/Handler/BaseGame/BulldozeCommandHandler.cs`

### 2) Zoning
- `[x]` Define zoning command payload.
- `[x]` Patch `ZoneToolSystem.Apply` tool capture for client requests and server replication.
- `[x]` Add replay service that applies zoning commands through `SnapPoint`/`UpdateDefinitions`.
- `[x]` Add role-based zoning handler with nonce dedupe.
- `[x]` Add runtime safety guards for problematic zoning modes if desyncs are observed.
- `[ ]` Add runtime validation.
- Files:
  - `CS2M.BaseGame/Commands/ZoneApplyCommand.cs`
  - `CS2M/BaseGame/ZoneSyncPatch.cs`
  - `CS2M/BaseGame/ZoneSyncService.cs`
  - `CS2M/Commands/Handler/BaseGame/ZoneApplyCommandHandler.cs`

### 3) Roads
- `[x]` Define road command payload.
- `[x]` Patch `NetToolSystem.Apply` capture for client requests and server replication.
- `[x]` Add road replay service that restores tool state/control points and replays `Update` + `Apply`.
- `[x]` Add role-based road handler with nonce dedupe.
- `[x]` Block unsupported net modes (`Replace`, `Point`, upgrade/service-upgrade) to reduce desync risk.
- `[ ]` Add runtime validation.
- Files:
  - `CS2M.BaseGame/Commands/RoadApplyCommand.cs`
  - `CS2M/BaseGame/RoadSyncPatch.cs`
  - `CS2M/BaseGame/RoadSyncService.cs`
  - `CS2M/Commands/Handler/BaseGame/RoadApplyCommandHandler.cs`

### 4) Verification
- `[x]` `dotnet build CS2M/CS2M.csproj -v minimal`
- `[ ]` In-game manual scenario:
  1. Host and client join.
  2. Client bulldozes a single building.
  3. Host confirms delete applied.
  4. Client zones a small area; host and client verify matching zone result.
  5. Client places a simple straight road; host and client verify matching road segment.
