# CS2M Development Notes

## Current State (2026-02-06)

- Repository is in active refactor state with substantial local edits in progress.
- Main mod build path is healthy:
  - `dotnet build CS2M/CS2M.csproj -v minimal` succeeds.
  - UI bundle build succeeds (`npm run build` in `CS2M.UI`).
- Full solution build currently fails because `CS2M.Test` is missing local game/Unity references in this environment.
- Latest local `dotnet build CS2M/CS2M.csproj -v minimal` succeeds with two non-blocking warnings:
  - unused fields in `CS2M/Networking/NetworkingSystem.cs`.
- IDA MCP session is healthy and was used for M4 function bookmarking.

## Latest Pass (2026-02-06, UI reliability)

- Added explicit session-exit path from UI:
  - New backend trigger `LeaveSession` in `CS2M/UI/UISystem.cs`.
  - Added `Leave Session` action to hub/join/host screens when state is blocked by an active client/server session.
  - `LeaveSession` routes to `NetworkInterface.Instance.StopServer()` (which leads to `LocalPlayer.Inactive()` cleanup).
- Reduced false "button is broken" states:
  - Seeded default username in `UISystem` from local player name or OS username, then pushed it to `NetworkInterface`.
  - This prevents first-open host/join actions from being disabled only because username is empty.
- Improved launcher behavior:
  - `CS2M.UI/src/extends/main-menu.tsx` now hides the floating Multiplayer launcher while any modal menu is open.
- Cohtml CSS hardening:
  - Removed unsupported CSS patterns from shared input/chat styles (`-webkit-*` pseudo-elements, `gap`, and problematic focus shadow expression).
  - Kept styling while reducing parse warnings that can destabilize UI behavior.
- Localization:
  - Added `LeaveSession` and hub state-hint keys in `lang/en-US.json`, `lang/de-DE.json`, `lang/pl-PL.json`.
- Verification:
  - `npm run build` in `CS2M.UI`: success.
  - `dotnet build CS2M/CS2M.csproj -v minimal`: success (2 existing unused-field warnings).
- IDA bookmarks (new session `E37BAB38`):
  - `0x1810` `UI.ShowMultiplayerMenu`
  - `0x18b0` `UI.ShowJoinGameMenu`
  - `0x1910` `UI.ShowHostGameMenu`
  - `0x1bb0` `UI.LeaveSession`
  - `0x3270` `LocalPlayer.Inactive`

## Active Workstream

1. M2 runtime verification:
   - validate host/client building placement replication behavior in live game.
2. M3 deterministic sync hardening:
   - tune authority refresh cadence for speed/money under longer sessions.
   - add aggregated divergence telemetry summaries.
3. M4 late-join reliability:
   - run manual repeated late-join validation against hardened transfer/retry path.
4. M5 gameplay surface expansion:
   - validate authoritative bulldoze + zoning + roads scaffolds in host/client runtime.

## Completed In This Pass

- M1 network foundation pass:
  - Added explicit connected vs joined peer tracking with duplicate guards.
  - Added explicit `JoinRequestCommand -> JoinAcceptedCommand -> WorldTransfer -> JoinReadyCommand` flow.
  - Added `JoinReadyCommand` (`client -> server`) so server only marks peer joined after client load completion.
  - Enforced server-side command gating: peers must be connected and joined before non-handshake commands are accepted.
  - Added disconnect cleanup path to remove peers from both connected/joined lists.
  - Removed the `WaitingToJoin -> DownloadingMap` shortcut and made transition explicit on join acceptance.
  - Added broader local cleanup in `LocalPlayer.Inactive()` (network stop, stream reset, remote list reset, UI progress/error reset).
- M2 authoritative building placement pass (initial implementation):
  - Replaced placeholder `BuildingCreateCommand` payload with prefab/transform/request fields.
  - Added `ObjectToolSystem.Apply` Harmony hook to capture placement intent.
  - Client path now sends placement request to server and blocks direct local apply.
  - Server path now rebroadcasts host placement and handles client placement requests.
  - Added runtime placement apply service that injects creation/object definitions.
  - Added nonce-based duplicate suppression for request and replication paths.
  - Added duplicate world-transfer-start protection per peer during join flow.
  - Narrowed capture/apply scope to `BuildingPrefab` only.
  - Added explicit CS2-side `BuildingCreateCommand` handler and removed obsolete empty base handler stub.
- M3 authority enforcement pass:
  - `TimeSystem` now broadcasts only from server role.
  - `MoneySyncSystem` now broadcasts only from server role.
  - Added periodic authority refresh cadence for speed and money broadcasts.
  - `SpeedCommandHandler` now applies only on clients.
  - `MoneyCommandHandler` now applies only on clients and uses `MoneySyncSystem.SetMoney`.
  - Added client-side drift correction logs for speed/money/frame command application.
  - Added periodic correction summary logs (count + average delta) for speed/money/frame.
- M4 late-join reliability hardening pass:
  - Added transfer integrity metadata (`TransferId`, `SliceIndex`) to world transfer packets.
  - Added server-side bounded world transfer retry attempts with delayed retries.
  - Moved `JoinAcceptedCommand` dispatch into transfer-attempt start so acceptance aligns with real transfer start.
  - Added client-side stale/sequence validation for world-transfer slices.
  - Added client-side bounded retry path when world load fails.
  - Hardened `SaveLoadHelper` save/load cleanup with timeout and `finally` safety.
  - Removed `SaveLoadHelper.OnUpdate()` crash behavior.
- M5 bulldoze authority scaffold pass:
  - Added `BulldozeCommand` payload with entity identity and nonce.
  - Added `BulldozeToolSystem.Apply` patch for client request forwarding and server replication.
  - Added temporary guard that blocks unsynchronized multi-selection bulldoze paths.
  - Added `BulldozeService` to queue delete definitions (`CreationFlags.Delete`) against target entities.
  - Added `BulldozeCommandHandler` with role-aware processing and nonce dedupe for request/replication paths.
- M5 zoning authority scaffold pass:
  - Added `ZoneApplyCommand` payload with serialized tool state and control-point snapshots.
  - Added `ZoneToolSystem.Apply` patch for client request forwarding and server replication.
  - Added `ZoneSyncService` to replay zoning updates through `SnapPoint` and `UpdateDefinitions`.
  - Added `ZoneApplyCommandHandler` with role-aware processing and nonce dedupe for request/replication paths.
  - Added runtime safety guard to block unsupported zoning modes (`FloodFill`, `Paint`) until explicitly synchronized.
- M5 roads authority scaffold pass:
  - Added `RoadApplyCommand` payload with `NetToolSystem` state + control-point snapshots.
  - Added `NetToolSystem.Apply` patch for client request forwarding and server replication.
  - Added replay suppression scope in patch so programmatic replays do not re-emit network commands.
  - Added `RoadSyncService` to restore net tool state and replay `Update` + `Apply`.
  - Added `RoadApplyCommandHandler` with role-aware processing and nonce dedupe.
  - Added temporary guard blocking unsupported net modes (`Replace`, `Point`, upgrade/service-upgrade).
- Repaired an interrupted edit corruption in `CS2M/UI/UISystem.cs` (literal `` `r`n `` text was present in source).
- Added join password flow from UI to backend:
  - UI binding + input field in `CS2M.UI/src/screens/join-game-menu.tsx`.
  - New `SetJoinPassword` trigger + `JoinPassword` value binding in `CS2M/UI/UISystem.cs`.
  - `JoinGame` now forwards password into `ConnectionConfig`.
- Hardened network receive/send paths in `CS2M/Networking/NetworkManager.cs`:
  - Wrapped packet handling in `try/catch`.
  - Added null-command and missing-handler guards.
  - Added safe-guard checks before client send-to-server.
- Synced `Command.CurrentRole` with player type transitions in `CS2M/Networking/LocalPlayer.cs`.
- Redacted password mismatch logging in `CS2M/Commands/Handler/Internal/PreconditionsCheckHandler.cs`.
- UI salvage pass (usability-focused):
  - Added host password binding and host password input flow to backend `ConnectionConfig`.
  - Added join/host input validation (required username, required server IP for join, valid port range).
  - Reworked join error rendering to decode precondition payload details instead of showing raw error codes.
  - Added status labels for `CONNECTION_ESTABLISHED`, `WAITING_TO_JOIN`, and `PLAYING`.
  - Added missing localization keys for `de-DE` and `pl-PL` to avoid raw key leaks in UI.
- UI polish pass (presentation-only):
  - Unified Join/Host panel visual hierarchy (stronger contrast, cleaner section cards, consistent buttons).
  - Removed inline status/progress styling and moved to reusable SCSS classes.
  - Polished input styling for readability in dark overlays.
  - Reworked chat panel to match the menu theme, including bubble styling and cleaner composer row.
  - Added subtle load/message animations and disabled styling for send button.
- Join flow feature pass:
  - Added `JoinToken` backend/UI binding and token-based join path (`ConnectionConfig(token, password)`).
  - Added recent server presets in join UI (token or IP/port), with local persistence and one-click apply.
  - Kept passwords out of recent preset storage.

## Validation

- `dotnet build CS2M/CS2M.csproj -v minimal`: success.
- `npm run build` in `CS2M.UI`: success.

## Feature Audit (2026-02-06)

- `25` tracked feature lines in total:
  - `15` implemented and usable.
  - `7` partial/fragile.
  - `3` stub/no-op.
- Effective usable count for actual city-building gameplay actions is `0` (no authoritative place/bulldoze/roads/zoning yet).

### Implemented And Usable (`14`)

1. Host server from in-game menu.
2. Join server by IP/port from main menu.
3. Join password is wired from UI to `ConnectionConfig`.
4. NAT punch attempt with direct-connect fallback.
5. Client/server precondition checks (game/mod/dlc/mod list/username/password).
6. Full world save packaging and sliced transfer to joining clients.
7. Client world slice reception and load trigger.
8. Chat send/relay/display loop.
9. Join/download status + progress updates surfaced to UI bindings.
10. Server keepalive/registration pings to API server.
11. Base simulation sync commands (frame, speed, money) are present and wired.
12. Connected vs joined peer lifecycle tracking is now explicit in `NetworkInterface`.
13. Join-ready + server command gating + disconnect list cleanup is in place.
14. Deterministic join handshake now includes explicit `JoinRequest` and `JoinAccepted` commands.
15. Authoritative building placement protocol skeleton is wired (`request -> apply -> replicate`).
16. Core speed/money sync is now server-authoritative with periodic refresh broadcasts.

### Partial Or Fragile (`7`)

1. Server-side player/session metadata is still minimal (no richer state machine per remote player).
2. Port reachability and UPnP setup are still `TODO`.
3. Save/load stream wrapper now has improved guardrails, but runtime behavior under packet loss has not been manually validated yet.
4. UI remains partial: no discovery/server browser and still needs stronger UX polish, but basic validation and actionable join errors are now in place.
5. API port-check result path is only partially integrated (`TODO` to push state into panel flow).
6. Building placement apply path uses inferred component injection and is not in-game validated yet.
7. Full authoritative city-building surface (bulldoze/zoning/roads) is not implemented yet.

### Stub Or No-Op (`3`)

1. `CS2M.BaseGame/Systems/BuildingSystem.cs` update loop is empty.
2. `CS2M.BaseGame/Injections/BuildingCreate.cs` command handler is empty.
3. `LocalPlayer.Blocked()` is empty.
