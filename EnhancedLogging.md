# Enhanced Logging System Documentation

## Overview

The CS2M mod includes a comprehensive logging system designed for debugging, monitoring, and production diagnostics. This document describes the logging architecture, usage patterns, and best practices.

---

## Log Levels

### Priority Order (Highest to Lowest)

```
TRACE > DEBUG > INFO > WARN > ERROR > FATAL
  4       3     2      1     0      -1
```

| Level   | Purpose | Typical Usage |
|---------|---------|---------------|
| TRACE   | Extremely detailed diagnostic information | Network packet routing, frame-by-frame calculations |
| DEBUG   | Development debugging | Variable states, function entry/exit |
| INFO    | Normal operational messages | Connection established, save loaded |
| WARN    | Unexpected but recoverable situations | Rate limit exceeded, retry attempt |
| ERROR   | Critical failures requiring attention | Serialization failed, connection drop |
| FATAL   | Unrecoverable errors causing shutdown | Memory exhaustion, corrupted data |

---

## Logging API

### Basic Usage

```csharp
using CS2M; // Contains Log singleton

// In any class
Log.Info("This is an informational message");
Log.Debug($"Player count: {playerCount}");
Log.Warn($"Rate limit reached for peer {peerId}");
Log.Error($"Failed to connect: {ex.Message}", ex);
Log.Trace($"Processing command type: {commandType}");
```

### Structured Logging

```csharp
// Include contextual data
Log.Info($"Connection established", new 
{
    PeerId = peer.Id,
    LatencyMs = latency,
    Endpoint = endpoint.ToString()
});

// With exception context
try
{
    ProcessData(data);
}
catch (Exception ex)
{
    Log.Error($"Data processing failed", ex, new 
    { 
        DataLength = data.Length,
        ErrorMessage = ex.Message
    });
}
```

### Conditional Logging

```csharp
// Only log if current level allows
if (LogLevel == LogLevel.TRACE)
{
    Log.Trace($"Complex calculation: result={result}, time={time}");
}

// Or use the helper method
Log.WhenTrace(() => $"Expensive object state: {expensiveObject.Dump()}");
```

---

## Configuration

### Runtime Settings

Access via `ModSettings`:

```csharp
public class ModSettings
{
    public int LoggingLevel { get; set; } = (int)LogLevel.INFO;
    
    public void OnSetLoggingLevel(LogLevel level)
    {
        Log.SetLogLevel(level);
    }
}
```

### Available Settings

- **LoggingLevel**: Controls minimum level shown (0-4)
- **EnableTimestamps**: Add timestamps to all logs
- **EnableSourceInfo**: Include file/method names
- **OutputTargets**: Console, File, Event Queue

### Example Configuration

```json
{
  "logging": {
    "level": "DEBUG",
    "timestamps": true,
    "sourceInfo": true,
    "outputToConsole": true,
    "outputToFile": true,
    "maxFileSizeMB": 10,
    "maxBackups": 5
  }
}
```

---

## Log Output Formats

### Console Format

```
[2024-04-26 14:30:15] [INFO] [CS2M.NetworkManager] Server started on port 4230
[2024-04-26 14:30:16] [DEBUG] [CS2M.Commands] Registered handler: MoneyCommandHandler
[2024-04-26 14:30:17] [ERROR] [CS2M.Networking] Failed to serialize packet: Exception details...
```

### File Format (JSON)

```json
{
  "timestamp": "2024-04-26T14:30:15.123Z",
  "level": "INFO",
  "category": "CS2M.NetworkManager",
  "message": "Server started on port 4230",
  "context": null,
  "exception": null
}
```

### Unity Inspector Debug

Logs automatically output to Unity's console for development visibility:

```
OnUpdate: Processing 3 network packets
Money sync applied: +1000 gold, epoch: 42
Building placed at (125.5, 89.2)
```

---

## Performance Considerations

### Avoid Overhead

❌ **Bad Practice:**
```csharp
// String concatenation happens even if not logged
Log.Debug($"Calculation result: {ExpensiveComputation()}");
```

✅ **Good Practice:**
```csharp
// Use conditional lambda
Log.WhenDebug(() => $"Calculation result: {ExpensiveComputation()}");
```

### Batch Logging

Instead of multiple calls:

❌ **Inefficient:**
```csharp
Log.Debug($"Step 1 done");
Log.Debug($"Step 2 done");
Log.Debug($"Step 3 done");
```

✅ **Efficient:**
```csharp
Log.Debug($"Batch steps: Step1, Step2, Step3 completed");
```

### Asynchronous Logging

For high-frequency events:

```csharp
// Queue events for batched processing
Log.QueueEvent(new LogEntry
{
    Level = LogLevel.INFO,
    Message = "High-frequency event",
    Timestamp = DateTime.UtcNow
});
```

---

## Best Practices

### DO ✅

1. **Use appropriate levels** - Don't dump everything as INFO
2. **Include context** - Provide meaningful variables in messages
3. **Log exceptions with stack traces** - Never swallow errors
4. **Format strings efficiently** - Use interpolation where possible
5. **Keep messages concise** - Under 100 characters preferred
6. **Include error codes** - When applicable
7. **Use structured logging** - For machine-readable output
8. **Log important transitions** - State changes, user actions

### DON'T ❌

1. **Log sensitive data** - Passwords, tokens, personal info
2. **Use Log for flow control** - Return values for that purpose
3. **Overuse debug logging** - Remove or disable in production
4. **Ignore performance impact** - Heavy computations should be lazy
5. **Create log spam** - Rate limiting for repetitive messages
6. **Throw without logging** - Always catch and log exceptions
7. **Forget to clean up** - Dispose resources before exit

---

## Diagnostic Tools

### Enable Verbose Logging

```csharp
// In Mod.cs or settings
Mod.Instance.Settings.LoggingLevel = (int)LogLevel.TRACE;
```

View logs via:
1. Game console (Unity inspector)
2. Log files in `%LOCALAPPDATA%\Colossal Order\Cities Skylines II\CS2M\`
3. Event viewer (Windows)

### Filter by Category

```bash
# Console filter pattern
grep "NetworkManager" game_log.txt

# Regex pattern for specific types
grep -E "\[(ERROR|WARN)\]" game_log.txt
```

### Real-time Monitoring

```csharp
// Subscribe to log events
Log.LogEvent += (level, msg, ctx) => 
{
    if (level >= LogLevel.ERROR)
    {
        SendAlertToAdmin(msg);
    }
};
```

---

## Common Patterns

### Initialization Logging

```csharp
public override void OnLoad(UpdateSystem updateSystem)
{
    Log.Info($"{Name} v{Assembly.GetExecutingAssembly().GetName().Version} loading");
    
    try
    {
        InitializeComponents();
        Log.Debug("All components initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Fatal("Failed to load mod", ex);
        throw;
    }
}
```

### Error Recovery Pattern

```csharp
try
{
    processOperation();
}
catch (OperationalException ex)
{
    Log.Warn($"Operation failed, attempting recovery: {ex.Message}");
    
    if (attemptRecovery())
    {
        Log.Info("Recovery successful");
    }
    else
    {
        Log.Error("Recovery failed, operation aborted");
    }
}
```

### Performance Monitoring

```csharp
var stopwatch = Stopwatch.StartNew();

// Operation

stopwatch.Stop();
Log.Trace($"Operation completed in {stopwatch.ElapsedMilliseconds}ms");

if (stopwatch.ElapsedMilliseconds > 100)
{
    Log.Warn($"Slow operation detected: {stopwatch.ElapsedMilliseconds}ms");
}
```

---

## Troubleshooting

### No Logs Appearing

1. Check `LoggingLevel` setting is not too restrictive
2. Verify output targets are enabled
3. Ensure application has write permissions to log directory

### Logs Too Verbose

1. Increase logging level (e.g., from TRACE to INFO)
2. Disable specific categories temporarily
3. Use category filters in log viewers

### Missing Stack Traces

1. Enable full symbol resolution in build settings
2. Ensure PDB files are available alongside DLLs
3. Check that exceptions are caught properly

### Performance Issues from Logging

1. Reduce logging level
2. Remove expensive string interpolations
3. Implement rate limiting for repeated messages
4. Consider asynchronous logging pipeline

---

## Advanced Features

### Correlation IDs

Track requests across systems:

```csharp
var correlationId = Guid.NewGuid().ToString("N")[..8];
LogContext.PushProperty("CorrelationId", correlationId);

// Later
Log.Debug($"Processing request with ID {correlationId}");
```

### Sampling

For high-volume data:

```csharp
// Log every Nth occurrence
if (SampleCounter++ % 100 == 0)
{
    Log.Debug($"Packet received (sample {SampleCounter})");
}
```

### Metrics Integration

Export log data to monitoring systems:

```csharp
Log.MetricsHistogram("packet_size_bytes", dataSize);
Log.MetricsTimer("serialization_duration_ms", durationMs);
```

---

## Conclusion

A well-implemented logging strategy is essential for maintaining and debugging complex multiplayer systems. The CS2M logging infrastructure provides:

- Flexible log levels for different scenarios
- Multiple output formats for various needs
- Performance-conscious design patterns
- Comprehensive error tracking and diagnosis tools

By following the guidelines in this document, developers can ensure their logging contributes effectively to system reliability and maintainability.

---

**Version**: 2.0  
**Last Updated**: April 2026
