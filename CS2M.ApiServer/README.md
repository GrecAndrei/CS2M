# CS2M API Server

Backend service for the [Cities: Skylines 2 Multiplayer (CS2M)](https://github.com/GrecAndrei/CS2M) mod. Listens on UDP/4242 for game server registrations and port-reachability checks, and exposes a REST API for querying registered servers.

## Quick start

```bash
docker compose up
```

This starts the API server (UDP/4242 + HTTP/8080), PostgreSQL 16, and Adminer (HTTP/8081).

**Without Docker:**

```bash
cd CS2M.ApiServer

# Start Postgres (adjust connection string in appsettings.json or
# set ConnectionStrings__Postgres env var)
dotnet run --project src/CS2M.ApiServer.Host
```

The server listens on `http://127.0.0.1:5000` for HTTP and `0.0.0.0:4242` for UDP.

## Protocol spec (wire format)

### Transport

- **UDP** — port 4242 by default.
- One MessagePack payload per datagram. Max payload ~1 KB; no streaming.
- The mod uses LiteNetLib's `SendUnconnectedMessage`, but the server uses a plain `UdpClient`. This works because the wire format is pure MessagePack, not LiteNetLib-framed.

### MessagePack configuration

The mod produces its serializer options via `MessagePack.Attributeless`:

```csharp
IFormatterResolver resolver = CompositeResolver.Create(
    MessagePack.Unity.Extension.UnityBlitResolver.Instance,
    MessagePack.Unity.UnityResolver.Instance,
    StandardResolver.Instance);
var options = MessagePackSerializerOptions.Standard
    .WithResolver(resolver)
    .Configure()
    .SubType(typeof(ApiCommandBase), typeof(PortCheckRequestCommand))    // key 0
    .SubType(typeof(ApiCommandBase), typeof(PortCheckResultCommand))     // key 1
    .SubType(typeof(ApiCommandBase), typeof(ServerRegistrationCommand))  // key 2
    .Build();
```

The server uses the same library and registration order so wire bytes are identical.

### Command types

| Command | Direction | Fields | Semantics |
|---------|-----------|--------|-----------|
| `ServerRegistrationCommand` | Game server → API server | `Token` (string), `LocalIp` (string), `LocalPort` (int) | "I am reachable at *LocalIp:LocalPort*; find me by *Token*." Sent every 5 min as keepalive. |
| `PortCheckRequestCommand` | Game server → API server | `Port` (int) | "TCP-probe this port on my registered IP and tell me if it's reachable." |
| `PortCheckResultCommand` | API server → Game server | `State` (enum: Reachable/Unreachable/Error), `Message` (string) | "Here's the answer to your last port check." |

### Token rules

- 1–128 characters from the set `[A-Za-z0-9._-]`.
- Currently self-issued by the game server (`connectionConfig.Token`). The server treats tokens as opaque bearer credentials.
- A pre-shared / admin-issued token scheme can be added by implementing the `POST /api/v1/tokens` endpoint.

## REST API

| Endpoint | Returns |
|----------|---------|
| `GET /api/v1/servers` | List of `{token, publicEndpoint, lastHeartbeatAt, version, displayName}`. Query params: `publicOnly`, `since`, `take`. |
| `GET /api/v1/servers/{token}` | Full server record. |
| `GET /healthz` | Liveness probe (always 200). |
| `GET /readyz` | Readiness probe (DB reachable + UDP bound → 200, otherwise 503). |
| `GET /metrics` | Prometheus exposition format (HTTP request count/duration, UDP datagrams, etc.). |
| `GET /swagger` | Swagger UI (Development only). |

All errors return [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) problem details.

## Architecture

| Layer | Project | Responsibility |
|-------|---------|---------------|
| Core | `CS2M.ApiServer.Core` | Wire-format POCOs (`ApiCommandBase` subclasses), shared interfaces (`IApiCommandCodec`, `IPortCheckEnqueuer`, `IProbeReplyChannel`), token validation. |
| Protocol | `CS2M.ApiServer.Protocol` | UDP listener (`ApiUdpListener`), MessagePack codec (`ApiCommandCodec`), command dispatcher (`ApiCommandDispatcher`), per-IP rate limiter (`UdpRateLimiter`). |
| Storage | `CS2M.ApiServer.Storage` | EF Core `ApiServerDbContext` with PostgreSQL, `ServerRepository`, EF migration. |
| Workers | `CS2M.ApiServer.Workers` | `PortReachableChecker` (TCP probe), `StaleServerReaper` (cleanup), bounded work queue. |
| Host | `CS2M.ApiServer.Host` | ASP.NET Core 8 host, REST controllers, Serilog, Swagger, Prometheus metrics. |

### Extension: adding a new command type

1. Add the POCO to `CS2M.ApiServer.Core/Commands/` extending `ApiCommandBase`.
2. Add a `SubType` call in `ApiMessagePackOptions.Build()` in the correct position (same order as the mod's `ApiCommand.cs:RefreshModel`).
3. Add a handler class under `CS2M.ApiServer.Protocol/Dispatch/Handlers/` extending `ApiCommandHandler<T>`.
4. Register the handler in `Program.cs` as `AddScoped<IApiCommandHandler, YourHandler>()`.

## Rate limiting

| Layer | Limit |
|-------|-------|
| UDP (per source IP) | 10 msg/s, burst 10. Enforced by `UdpRateLimiter` (token bucket). |
| TCP probes | Max 32 concurrent probes (SemaphoreSlim), 3-second timeout per probe. |

## Testing

```bash
dotnet test tests/CS2M.ApiServer.Tests --logger "console;verbosity=normal"
```

Tests use an in-memory SQLite database (no Postgres needed). End-to-end tests spin up the full ASP.NET Core host via `WebApplicationFactory` and exercise the UDP + REST surface.

## Environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ConnectionStrings__Postgres` | `Host=127.0.0.1;Port=5432;Username=cs2m;Password=cs2m;Database=cs2m` | PostgreSQL connection string. |
| `ASPNETCORE_URLS` | `http://+:8080` | HTTP listen address. |
| `Udp__Port` | `4242` | UDP listen port. |

## Design decisions

**Why `UdpClient` instead of LiteNetLib?** The mod uses LiteNetLib only for its game-networking layer. The API server talks a different protocol (plain MessagePack over UDP). A raw `UdpClient` has fewer dependencies and is simpler to audit. This is explicitly documented so nobody tries to add LiteNetLib later.

**Why `MessagePack.Attributeless`?** The mod's command types don't have `[Key]` attributes. Attributeless's `Configure().SubType()` registers `SubTypeFormatter<TBase>` which auto-assigns subtype keys and auto-keys concrete type properties. Without Attributeless the mod couldn't serialize its own commands; the server wouldn't be able to deserialize them.

**Why SQLite in tests, not in-memory Postgres?** Testcontainers for Postgres would require Docker to be available on the CI runner and slow down test startup. The EF Core model works identically on SQLite for the schema in use; the few Postgres-specific types (timetz, bigserial) map correctly through EF Core's provider abstraction.

## License

MIT
