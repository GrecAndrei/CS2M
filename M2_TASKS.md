# M2 Task List - Authoritative Building Placement

Status legend:
- `[ ]` pending
- `[-]` in progress
- `[x]` done

Updated: 2026-02-06

## Execution Order

1. `[x]` Define placement command payload for prefab + transform + authority flag.
2. `[x]` Capture local object placement intent from tool apply hook.
3. `[x]` Route client placement through server authority (`request -> apply -> replicate`).
4. `[-]` Apply replicated placement commands on clients through tool-definition injection.
5. `[ ]` Run in-game host/client validation for place + relay behavior.

## Detailed Work Items

### 1) Command Payload
- `[x]` Replace placeholder building id with structured placement data.
- Files:
  - `CS2M.BaseGame/Commands/BuildingCreateCommand.cs`

### 2) Placement Capture Hook
- `[x]` Hook `ObjectToolSystem.Apply` with Harmony.
- `[x]` Block direct client apply and send server request command.
- `[x]` Mirror server-host local apply to remote clients.
- Files:
  - `CS2M/BaseGame/BuildingPlacementPatch.cs`

### 3) Server Authority Handler
- `[x]` Handle request command on server.
- `[x]` Apply placement request via entity-definition injection.
- `[x]` Broadcast authoritative placement command to clients.
- `[x]` Ignore duplicate request packets via nonce window.
- Files:
  - `CS2M/Commands/Handler/BaseGame/BuildingCreateCommandHandler.cs`
  - `CS2M/BaseGame/BuildingPlacementService.cs`

### 4) Client Replication Apply
- `[x]` Resolve prefab name to prefab entity at runtime.
- `[x]` Inject `CreationDefinition`/`ObjectDefinition` for tool pipeline.
- `[x]` Ignore duplicate replicated placement packets via nonce window.
- `[ ]` Confirm object appears once (no duplicates, no missing entity).
- Files:
  - `CS2M/BaseGame/BuildingPlacementService.cs`
  - `CS2M/Commands/Handler/BaseGame/BuildingCreateCommandHandler.cs`

### 5) Verification
- `[x]` `dotnet build CS2M/CS2M.csproj -v minimal`
- `[ ]` In-game manual scenario:
  1. Host game
  2. Client joins
  3. Client places one building
  4. Host confirms placement exists
  5. Client confirms exactly one replicated placement
