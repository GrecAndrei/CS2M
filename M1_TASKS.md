# M1 Task List - Network/State Foundation

Status legend:
- `[ ]` pending
- `[-]` in progress
- `[x]` done

Updated: 2026-02-06

## Execution Order

1. `[x]` Define and enforce state gates for incoming commands.
2. `[x]` Separate `connected` vs `joined` peer handling in `NetworkInterface`.
3. `[x]` Implement join handshake completion path (remove temporary shortcuts).
4. `[x]` Implement server/client disconnect cleanup (lists, roles, local state).
5. `[x]` Add targeted logs for state transitions and command drops.
6. `[-]` Run smoke build + host/client join-leave test loop.

## Detailed Work Items

### 1) Command Gating
- `[x]` Block non-precondition commands from peers not in allowed state.
- `[x]` Explicitly allow only handshake commands before joined state.
- Files:
  - `CS2M/Networking/NetworkManager.cs`
  - `CS2M/Networking/NetworkInterface.cs`

### 2) Connected vs Joined Lists
- `[x]` Add methods for moving peer from connected -> joined.
- `[x]` Ensure removal from both lists on disconnect.
- `[x]` Avoid duplicate local/remote entries.
- Files:
  - `CS2M/Networking/NetworkInterface.cs`

### 3) Join Handshake Path
- `[x]` Replace temporary `WaitingToJoin -> DownloadingMap` shortcut with explicit step logic.
- `[x]` Ensure client status transitions are deterministic.
- `[x]` Add `JoinRequestCommand` and `JoinAcceptedCommand` handshake step before world transfer.
- `[x]` Add client->server `JoinReadyCommand` to mark joined state on server.
- Files:
  - `CS2M/Networking/LocalPlayer.cs`
  - `CS2M/Commands/Data/Internal/JoinRequestCommand.cs`
  - `CS2M/Commands/Data/Internal/JoinAcceptedCommand.cs`
  - `CS2M/Commands/Handler/Internal/JoinRequestHandler.cs`
  - `CS2M/Commands/Handler/Internal/JoinAcceptedHandler.cs`
  - `CS2M/Commands/Data/Internal/JoinReadyCommand.cs`
  - `CS2M/Commands/Handler/Internal/JoinReadyHandler.cs`
  - `CS2M/Commands/Handler/Internal/PreconditionsSuccessHandler.cs`

### 4) Disconnect Cleanup
- `[x]` Clean client-specific state on disconnect.
- `[x]` Clean server-side remote player state on disconnect.
- `[x]` Verify role reset (`Command.CurrentRole`) and UI state reset.
- Files:
  - `CS2M/Networking/LocalPlayer.cs`
  - `CS2M/Networking/NetworkManager.cs`
  - `CS2M/Networking/NetworkInterface.cs`

### 5) Verification
- `[x]` `npm run build` in `CS2M.UI`
- `[x]` `dotnet build CS2M/CS2M.csproj -v minimal`
- `[ ]` Manual host/client loop:
  1. Host
  2. Join
  3. Disconnect client
  4. Rejoin
  5. Stop host
