# CS2M Architecture Documentation

## System Overview

This document provides comprehensive technical documentation of the CS2M multiplayer architecture.

### Core Components

#### 1. Command Architecture

```
BaseCommand (MessagePack serializable)
    └── CommandBase (API commands)
        ├── Internal Commands (Join, Connect, etc.)
        │   ├── JoinRequestCommand
        │   ├── JoinReadyCommand
        │   ├── JoinAcceptedCommand
        │   ├── PreconditionsCheckCommand
        │   ├── WorldTransferCommand
        │   └── ChatMessageCommand
        │
        └── BaseGame Commands (Gameplay sync)
            ├── MoneyCommand
            ├── FrameCommand
            ├── BuildingCreateCommand
            ├── BulldozeCommand
            ├── RoadApplyCommand
            └── ZoneApplyCommand
```

**Key Features:**
- Automatic type detection via `CommandType` property
- MessagePack compression with Lz4 algorithm
- Security tokens for replay attack prevention
- Timestamps for debugging and ordering
- Built-in validation in every command

#### 2. Handler Hierarchy

```
CommandHandler (abstract)
    ├── ClientCommandHandler<TCommand>
    │   └── Validates client-side
    │
    └── ServerCommandHandler<TCommand>
        └── Validates server authority

Example Handlers:
    - FrameCommandHandler (server authority)
    - MoneyCommandHandler (client updates from server)
    - JoinRequestHandler (server-only processing)
    - BuildingCreateCommandHandler (validation + execution)
```

**Handler Responsibilities:**
1. Validate sender authority
2. Validate command data integrity
3. Execute business logic
4. Send responses or broadcast changes
5. Log for debugging

#### 3. Network Stack

```
LiteNetLib (UDP backbone)
    ├── NAT Hole Punching
    ├── Connection Management
    └── Packet Delivery
    
CS2M Abstraction Layer
    ├── NetworkManager (state machine)
    │   ├── ConnectionState enum
    │   ├── Thread-safe peer tracking
    │   ├── Graceful shutdown
    │   └── Event processing loop
    │
    └── SerializationPipeline
        ├── Serialize → Compression → Send
        └── Receive → Decompress → Deserialize → Route
```

**State Machine States:**
1. **Disconnected** - Initial state
2. **Initializing** - NetManager starting
3. **Initialized** - Ready to connect
4. **Connecting** - Establishing connection
5. **Connected** - Active session
6. **NatHolePunching** - NAT traversal active
7. **ServerStarting** - Host initialization
8. **ServerRunning** - Listening for connections
9. **Failed** - Error state requiring restart

#### 4. Game Synchronization Systems

**FrameSyncSystem**
- Server: Broadcasts frame index every 60 frames (~1 second)
- Clients: Interpolates frames with smoothing factor 0.9
- Anti-cheat: Rejects jumps > 300 frames
- Latency compensation using timestamps

**MoneySyncSystem**
- Authority epoch system prevents out-of-order updates
- Rate limiting: Max 100k change per frame
- Pattern detection for suspicious activity
- Client smoothing history queue (10 samples max)

## Data Flow Examples

### Example 1: Player Join Sequence

```
Client                          Server
  |                               |
  |-- PreconditionsCheck -------->|
  |                               |  [Username validation]
  |                               |  [Password check]
  |--<<Success/Error>>------------|
  |                               |
  |<-- JoinRequest ---------------|
  |                               |  [World transfer start]
  |--<<Save Stream>>------------->|
  |                               |
  |<-- JoinAccepted ------------- |
  |                               |  [Connection confirmed]
  |                               |
  |-- Ready --------------------->|
  |=== JOINED SESSION ====>       |
```

### Example 2: Building Creation

```
Client                          Server
  |                               |
  |-- BuildingCreate ----------- ->|
  |                               |  [Validate coordinates]
  |                               |  [Check rate limit]
  |                               |  [Resolve prefab entity]
  |                               |  
  |                              [Create ECS entity]
  |                               |
  |-- Ack ----------------------->|
  |                               |
  |--<<Building Placed>>----------|==>[Broadcast to clients]
  |                               |
  |<--<<Entity Created>>-----------|
  |=== BUILDING APPEARED ==========
```

### Example 3: Money Update Cycle

```
Every ~500ms on Server:
  |
  |-- Sample current money amount
  |
  |-- Validate against rules:
  |    [-50k < delta < +50k] 
  |    [Pattern check passed]
  |
  |-- Send MoneyCommand {
  |      Money: 100000
  |      AuthorityEpoch: N+1
  |      Timestamp: now
  |  }
  |
  |===[Broadcast to all connected clients]===>

Clients:
  |-- Receive MoneyCommand
  |
  |-- Validate AuthorityEpoch > Current
  |
  |-- Apply to MoneySyncSystem
  |
  |-- Smooth interpolation update
  |
  [UI displays new money value]
```

## Security Models

### Defense Layers

**Layer 1: Input Validation**
```csharp
// Every command validates its own structure
public override bool Validate()
{
    if (string.IsNullOrWhiteSpace(Username)) return false;
    if (Username.Length > 64) return false;
    // ... additional checks
    return true;
}
```

**Layer 2: Rate Limiting**
```csharp
// Prevent spam/DoS attacks
private const int MAX_REQUESTS_PER_SECOND = 3;
ConcurrentDictionary<int, JoinRequestThrottle> _throttling;

if (requests >= MAX_REQUESTS_PER_SECOND)
    throw new SecurityException("Rate limit exceeded");
```

**Layer 3: Authority Checks**
```csharp
// Only server can make authoritative changes
protected override bool ValidateSender(ServerCommand cmd, NetPeer peer)
{
    return IsAuthenticatedPeer(peer);
}
```

**Layer 4: Game Logic Validation**
```csharp
// Economic values must be reasonable
if (delta > MAX_REASONABLE_RATE_OF_CHANGE)
    RejectUpdate();
```

## Performance Characteristics

### Metrics Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Latency | < 50ms average | Round-trip time |
| Frame Sync Deviation | < 10 frames | Interpolation lag |
| Money Accuracy | < 1% drift | Compared to server |
| Throughput | 10-100 KB/s | Depending on activity |
| CPU Usage | < 2% per core | During gameplay |

### Optimization Techniques

1. **Lz4 Compression**: Reduces packet size by 3-5x
2. **Object Pooling**: Reuses frequently allocated objects
3. **Batch Processing**: Groups related operations
4. **Async I/O**: Non-blocking world transfers
5. **Lock-free Data Structures**: ConcurrentDictionary usage

## Testing Strategy

### Unit Tests
- Command serialization/deserialization
- Validator implementations
- Rate limiter logic
- Network state transitions

### Integration Tests
- Full join sequence
- Building placement under load
- Money synchronization accuracy
- Network stress testing

### Performance Tests
- 10+ concurrent players
- Large building clusters
- Extended gameplay sessions (1+ hours)
- Memory profiling

## Extension Points

### Adding New Commands

1. Create command class inheriting `BaseCommand`:
```csharp
[MessagePackObject]
public class MyCustomCommand : CommandBase
{
    [Key(0)] public int PropertyA { get; set; }
    [Key(1)] public string PropertyB { get; set; }
    
    public override bool Validate() => true;
}
```

2. Create handler:
```csharp
public class MyCustomCommandHandler : ServerCommandHandler<MyCustomCommand>
{
    protected override void OnValidatedCommand(MyCustomCommand cmd)
    {
        // Process command
    }
}
```

3. Register in initializer:
```csharp
CommandRegistry.RegisterHandler<MyCustomCommandHandler>();
```

### Adding New Sync Systems

1. Create Unity system inheriting `GameSystemBase`:
```csharp
public partial class MySyncSystem : GameSystemBase
{
    protected override void OnUpdate()
    {
        switch (Command.CurrentRole)
        {
            case MultiplayerRole.Server:
                HandleServerLogic();
                break;
            case MultiplayerRole.Client:
                HandleClientLogic();
                break;
        }
    }
}
```

2. Register in Mod.cs:
```csharp
updateSystem.UpdateAt<MySyncSystem>(SystemUpdatePhase.GameSimulation);
```

## Troubleshooting Guide

### Common Issues

**Issue: "No handler found for command"**
- Solution: Ensure handler is registered before first use
- Check: `CommandRegistry.LogRegistrations()` output

**Issue: Serialization fails**
- Check: Type has `[MessagePackObject]` attribute
- Check: All properties have `[Key(n)]` attributes

**Issue: Rate limiting blocking legitimate users**
- Solution: Increase throttle thresholds or add whitelist

**Issue: Frame interpolation stutter**
- Cause: High network jitter
- Fix: Adjust INTERPOLATION_SPEED constant

**Issue: Money desync**
- Cause: Multiple servers or save corruption
- Fix: Force authority update or reload save

## Debugging Tools

```bash
# Enable debug logging
mod.settings.loggingLevel = DEBUG

# View network statistics
Mod.Commands.PrintNetworkStats()

# Reset command registry (for testing)
Mod.Commands.ResetCommandHandlers()

# Force world save transfer
Mod.Commands.ForceWorldTransfer()
```

## API Reference

Complete API documentation available in XML comments throughout source code. Key interfaces:

- `INetworkManager` - Connection orchestration
- `ICommandHandler` - Command processing
- `IGameSyncSystem` - Gameplay synchronization
- `ISerializationProvider` - Data conversion

## Version History

v2.0.0 (Current): Complete architectural rewrite with focus on stability and security

v1.x.x: Original implementation with basic functionality

---

For questions or contributions, please refer to the main project repository.
