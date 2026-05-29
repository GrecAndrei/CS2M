# M4 Task List - Late Join Reliability

Status legend:
- `[ ]` pending
- `[-]` in progress
- `[x]` done

Updated: 2026-02-06

## Execution Order

1. `[x]` Harden world transfer save packaging path.
2. `[x]` Add bounded transfer retries on server join flow.
3. `[x]` Add transfer integrity checks on client slice receive path.
4. `[x]` Add bounded client retry flow for world load failures.
5. `[-]` Run repeated late-join runtime validation.

## Detailed Work Items

### 1) Save/Load Guard Rails
- `[x]` Add save-system busy timeout in `SaveGame`.
- `[x]` Ensure auto-save + stream hooks are always restored via `finally`.
- `[x]` Reject empty/invalid streams before load.
- `[x]` Remove `OnUpdate` crash path from helper system.
- Files:
  - `CS2M/Helpers/SaveLoadHelper.cs`

### 2) Server Transfer Retry
- `[x]` Retry world transfer attempts with bounded retry count and delay.
- `[x]` Move `JoinAcceptedCommand` send into transfer attempt start.
- `[x]` Disconnect peer after retry budget is exhausted to avoid indefinite stuck states.
- Files:
  - `CS2M/Networking/NetworkInterface.cs`
  - `CS2M/Commands/Handler/Internal/JoinRequestHandler.cs`

### 3) Transfer Integrity
- `[x]` Add `TransferId` and `SliceIndex` fields to world transfer packets.
- `[x]` Ignore stale transfer starts/slices on client.
- `[x]` Validate contiguous slice order before append.
- Files:
  - `CS2M/Commands/Data/Internal/WorldTransferCommand.cs`
  - `CS2M/Networking/LocalPlayer.cs`

### 4) Client Retry Behavior
- `[x]` Add bounded retry path for load failures (`RetryWorldLoad`).
- `[x]` Reset transfer state on retry/success/inactive transitions.
- `[x]` Keep join request semantics (`WaitingToJoin`) for retry path.
- Files:
  - `CS2M/Networking/LocalPlayer.cs`

### 5) Verification
- `[x]` `dotnet build CS2M/CS2M.csproj -v minimal`
- `[ ]` In-game manual late-join scenario:
  1. Host game and keep simulation running.
  2. Join client during active simulation.
  3. Confirm world download/load completes.
  4. Disconnect/rejoin same client repeatedly.
  5. Confirm no stuck `DOWNLOADING_MAP` or `LOADING_MAP`.
