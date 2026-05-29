# CS2M Quick Start Guide

## Overview

This document provides quick reference for using and extending the CS2M multiplayer mod.

## Quick Reference

### Network Commands

```bash
# Show help for available commands
Mod.Commands.Help()

# View network statistics
Mod.Commands.PrintNetworkStats()

# Force world save transfer (server)
Mod.Commands.ForceWorldTransfer()

# Reset command handlers (debugging)
Mod.Commands.ResetCommandHandlers()
```

### Mod Settings

The mod automatically loads settings from the game options UI:

- **Network Port**: Default 4230
- **Password**: Optional server password
- **Logging Level**: ERROR, WARN, INFO, DEBUG, TRACE
- **API Server**: Connection endpoint for NAT traversal

### Common Operations

#### Hosting a Game

1. Open multiplayer hub (default key binding)
2. Click "Host Game"
3. Configure port and optional password
4. Click "Start Server"
5. Share token/IP with players

#### Joining a Game

1. Open multiplayer hub
2. Click "Join Game"
3. Enter connection method:
   - **Token**: Quickest, uses NAT punchthrough
   - **IP/Port**: Direct connection
4. Enter password if required
5. Click "Connect"

## Development Quick Reference

### Creating New Commands

```csharp
using MessagePack;
using CS2M.API.Commands;

namespace CS2M.Commands.Data
{
    // 1. Define command with serialization attributes
    [MessagePackObject]
    public class MyCommand : CommandBase
    {
        [Key(0)] public int Value { get; set; }
        [Key(1)] public string Data { get; set; }
        
        [Key(2)] public override string CommandType => nameof(MyCommand);
        
        // 2. Validate command data
        public override bool Validate()
        {
            return !string.IsNullOrEmpty(Data) && Value >= 0;
        }
    }
}
```

### Creating Handlers

```csharp
using CS2M.API.Commands;
using LiteNetLib;

// 3. Create server-side handler
public class MyCommandHandler : ServerCommandHandler<MyCommand>
{
    protected override bool IsAuthorized(NetPeer peer)
    {
        return true; // Or implement actual authorization
    }

    protected override void OnValidatedCommand(MyCommand cmd)
    {
        // Process the command
        Log.Info($"Received: {cmd.Value}, {cmd.Data}");
        
        // Optionally send response
        var response = new MyResponseCommand 
        { 
            OriginalValue = cmd.Value 
        };
        
        Command.SendToServer(response);
    }
}
```

### Registering Handlers

Add to `CommandHandlerInitializer.InitializeAll()`:

```csharp
RegisterHandler<MyCommandHandler>();
```

### Adding Sync Systems

```csharp
using Unity.Entities;

public partial class MySyncSystem : GameSystemBase
{
    protected override void OnUpdate()
    {
        switch (Command.CurrentRole)
        {
            case MultiplayerRole.Server:
                // Broadcast updates
                break;
                
            case MultiplayerRole.Client:
                // Apply received updates
                break;
        }
    }
}
```

Register in `Mod.OnLoad()`:

```csharp
updateSystem.UpdateAt<MySyncSystem>(SystemUpdatePhase.GameSimulation);
```

## Debugging Tips

### Enable Detailed Logging

```csharp
// In ModSettings
public int LoggingLevel { get; set; } = (int)LogLevel.DEBUG;
```

Log levels:
- **ERROR** (0): Critical failures only
- **WARN** (1): Unexpected but recoverable
- **INFO** (2): Normal operational messages
- **DEBUG** (3): Debug information
- **TRACE** (4): Very detailed tracing

### Network Diagnostics

```csharp
// Check network status
var status = NetworkInitializer.Summary;
Log.Info($"Active: {status.IsActive}, State: {status.ManagerState}");

// List registered handlers
CommandSystem.ListHandlers();

// View statistics
var stats = NetworkStatistics.GetSummary();
Log.Info($"Tx: {stats.TotalBytesSent}B, Peers: {stats.ActivePeers}");
```

### Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Serialization fails | Ensure `[MessagePackObject]` on all types |
| Handler not found | Call `RegisterHandler<T>()` before use |
| Rate limiting blocks user | Adjust constants or whitelist |
| Frame desync | Check network jitter and latency |
| Money out of sync | Verify authority epoch handling |

## Performance Guidelines

### Do's
- ✅ Use MessagePack for all network commands
- ✅ Validate input data early
- ✅ Batch related operations when possible
- ✅ Use async for I/O operations
- ✅ Pool frequently allocated objects

### Don'ts
- ❌ Perform heavy work in update loops
- ❌ Send large data chunks every frame
- ❌ Block main thread on network calls
- ❌ Use reflection-heavy code in hot paths
- ❌ Ignore rate limits

## API Quick Ref

### Serialization
```csharp
byte[] Serialize(BaseCommand cmd);
T Deserialize<T>(byte[] data) where T : BaseCommand;
bool CanSerialize(BaseCommand cmd);
int EstimateSize(BaseCommand cmd);
```

### Command Registry
```csharp
void RegisterHandler<T>() where T : CommandHandler;
T GetHandler<T>() where T : CommandHandler;
bool HasHandler(Type commandType);
void Clear(); // Testing only
```

### Network Manager
```csharp
bool StartServer(ConnectionConfig config);
bool Connect(ConnectionConfig config);
void SendToAll(CommandBase msg);
void SendToClient(NetPeer peer, CommandBase msg);
void SendToServer(CommandBase msg);
```

## File Structure

```
CS2M/
├── API/
│   ├── Commands/          # Command base classes
│   │   ├── BaseCommand.cs
│   │   ├── CommandBase.cs
│   │   ├── CommandRegistry.cs
│   │   └── SerializationHelper.cs
│   └── Networking/        # Player and connection types
│       ├── Player.cs
│       └── PlayerType.cs
│
├── BaseGame/             # Base game integration
│   ├── BuildingPlacementService.cs
│   ├── BulldozeService.cs
│   └── RoadSyncService.cs
│
├── Commands/             # Command implementations
│   ├── Data/             # Command definitions
│   └── Handler/          # Handler implementations
│       ├── Internal/     # Connection/gameplay handlers
│       └── BaseGame/     # Game feature handlers
│
├── Helpers/              # Utility classes
│   └── NetworkStatistics.cs
│
├── Networking/           # Network layer
│   ├── NetworkManager.cs
│   ├── NetworkInterface.cs
│   └── NetworkInitializer.cs
│
└── Systems/              # Unity ECS systems
    ├── FrameSyncSystem.cs
    ├── MoneySyncSystem.cs
    └── EconomyInspectorSystem.cs
```

## Troubleshooting

### Build Errors

**Error**: "Cannot find namespace 'CS2M'"  
**Solution**: Ensure project references are correct and dependencies restored

**Error**: "MessagePack compiler errors"  
**Solution**: Run `dotnet restore` and rebuild

**Error**: "Unity Entities not found"  
**Solution**: Verify Cities Skylines II managed assemblies path is configured

### Runtime Errors

**Crash on startup**: Check logs in `%LOCALAPPDATA%\Colossal Order\Cities Skylines II\CS2M\Log.txt`

**No network activity**: Verify firewall allows connections on configured port

**Desynchronization**: Try forcing world save or reconnecting

## Getting Help

- Check `IMPROVEMENTS_SUMMARY.md` for recent changes
- Review `ARCHITECTURE.md` for system details
- Search GitHub issues for known problems
- Contact maintainers for questions

---

For comprehensive documentation, see individual source file comments.
