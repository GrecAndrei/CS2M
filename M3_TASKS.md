# M3 Task List - Deterministic Core Sync Hardening

Status legend:
- `[ ]` pending
- `[-]` in progress
- `[x]` done

Updated: 2026-02-06

## Execution Order

1. `[x]` Enforce server-authoritative speed sync broadcast.
2. `[x]` Enforce server-authoritative money sync broadcast.
3. `[x]` Restrict speed/money command application to client role.
4. `[-]` Add periodic authority refresh cadence for drift correction.
5. `[x]` Add divergence telemetry hooks (speed/money/frame deltas).
6. `[ ]` Validate long-session host/client drift manually.

## Detailed Work Items

### 1) Server Authority
- `[x]` `TimeSystem` now sends only in server role.
- `[x]` `MoneySyncSystem` now sends only in server role.
- Files:
  - `CS2M.BaseGame/Systems/TimeSystem.cs`
  - `CS2M.BaseGame/Systems/MoneySyncSystem.cs`

### 2) Client Apply Rules
- `[x]` `SpeedCommandHandler` applies only when role is client.
- `[x]` `MoneyCommandHandler` applies only when role is client.
- `[x]` `MoneyCommandHandler` uses `MoneySyncSystem.SetMoney` to keep local sync state coherent.
- Files:
  - `CS2M.BaseGame/Commands/SpeedCommand.cs`
  - `CS2M.BaseGame/Commands/MoneyCommand.cs`

### 3) Drift Correction Cadence
- `[x]` Added periodic speed broadcast even when unchanged.
- `[x]` Added periodic money broadcast even when unchanged.
- `[ ]` Tune cadence constants with real host/client runs.
- Files:
  - `CS2M.BaseGame/Systems/TimeSystem.cs`
  - `CS2M.BaseGame/Systems/MoneySyncSystem.cs`

### 4) Divergence Telemetry
- `[x]` Added client-side drift correction logs for speed commands.
- `[x]` Added client-side drift correction logs for money commands.
- `[x]` Added client-side drift correction logs for frame commands.
- `[x]` Add aggregated periodic summary logs for long sessions.
- Files:
  - `CS2M.BaseGame/Commands/SpeedCommand.cs`
  - `CS2M.BaseGame/Commands/MoneyCommand.cs`
  - `CS2M.BaseGame/Commands/FrameCommand.cs`

### 5) Verification
- `[x]` `dotnet build CS2M/CS2M.csproj -v minimal`
- `[ ]` Manual long-session scenario:
  1. Host + client join
  2. Let sim run 10+ minutes
  3. Toggle speed/pauses several times
  4. Compare money/frame behavior for drift
