**Version:** 1.1 (Post-Delphi Round 1)
**Date:** 2026-03-18
**Changes:** Vault file presence gates Strategy 3; parse error → Unhealthy; add IgnoreDiConnection (RabbitMQ) and ConnectionStringName (Npgsql) options; confirm "ready"/"live" tag convention; credential defaults tightened.

# Functional Specification: Dynamic Health Checks — RabbitMQ + Npgsql

## Overview

Two new NuGet packages — `Innago.Shared.HealthChecks.RabbitMq` and `Innago.Shared.HealthChecks.Npgsql` — provide drop-in health check registrations for ASP.NET Core services. They eliminate 18 copies of hand-rolled health check code by centralising the tri-strategy credential resolution pattern that handles both static startup config and Vault-rotated secrets.

---

## User Stories

### US-1: Static connection pattern (current majority of services)

As a service that reads its RabbitMQ credentials once at startup (from environment variables or IConfiguration),
I want a one-line health check registration,
so that I can confirm RabbitMQ connectivity without maintaining custom code.

**Acceptance criteria:**
- Calling `.AddInnagoRabbitMq()` registers a health check that passes when the broker is reachable.
- If `IConnection` is registered in DI, the check uses that existing connection (no new TCP connection opened).
- The service does not need to supply any configuration options; defaults work out of the box.
- The health check result is `Healthy` when the connection can be verified, `Unhealthy` otherwise.

### US-2: Vault-rotated credentials pattern (future state)

As a service where HashiCorp Vault rotates RabbitMQ credentials periodically by writing them to `/vault/secrets/appsettings.json`,
I want the health check to re-read those credentials on every probe,
so that a credential rotation does not cause the health check to report stale credentials.

**Acceptance criteria:**
- If `/vault/secrets/appsettings.json` exists and contains `rabbit_host`, the check reads it fresh on every invocation.
- No caching of vault file contents between probes.
- File read failures (including malformed JSON) are treated as `Unhealthy`. Do **not** fall through to Strategy 3.
- The presence of the vault file at `VaultSecretsPath` gates Strategy 3: if the file exists (even if incomplete), IConfiguration is never used as a fallback.
- File I/O and parse errors are caught and reported with the error message in `HealthCheckResult.Description`.

### US-3: Vault-injected env var pattern (intermediate state)

As a service where Vault injects credentials as environment variables that IConfiguration reads automatically,
I want the health check to discover connection parameters from IConfiguration,
so that no vault-file-specific plumbing is required.

**Acceptance criteria:**
- Strategy 3 only fires when the vault file is **absent** at `VaultSecretsPath`.
- For RabbitMQ the check tries these keys in priority order: `RabbitMQ:Host`, `MassTransit:RabbitMq:Host`, `rabbit_host`.
- For Npgsql the check tries: `ConnectionStrings:{ConnectionStringName}`, `ConnectionStrings:DefaultConnection`, `ConnectionStrings:Database`, then builds a connection string from `db_host` / `db_username` / `db_password` / `db_name` / `DB_HOST` / `DB_NAME`.
- If no configuration key yields a usable value, the check returns `Unhealthy` with a descriptive message (not an exception).

### US-4: PostgreSQL health check — same tri-strategy pattern

As a service that uses PostgreSQL,
I want a one-line health check registration equivalent to the RabbitMQ check,
so that the same consistent pattern applies to both broker and database connectivity.

**Acceptance criteria:**
- `.AddInnagoNpgsql()` registers a health check that passes when the database is reachable.
- The check issues `SELECT 1` (configurable via `CommandText`) to verify the connection is live.
- Strategy resolution mirrors US-1 through US-3 but with Npgsql-specific config keys.

### US-5: Consumer configuration options

As a consuming service,
I want to optionally override the default vault secrets path, connection timeout, health check tags, and other behaviour,
so that non-standard deployments can still use the package without forking it.

**Acceptance criteria:**
- `AddInnagoRabbitMq(options => { ... })` and `AddInnagoNpgsql(options => { ... })` overloads exist.
- `VaultSecretsPath` defaults to `/vault/secrets/appsettings.json`; overridable.
- `ConnectionTimeout` defaults to `TimeSpan.FromSeconds(5)`; overridable.
- `Tags` defaults to `["ready"]`; overridable. `"live"` is the other Innago standard tag for liveness probes.
- `Name` (health check registration name) defaults to `"rabbitmq"` / `"npgsql"`; overridable.
- `IgnoreDiConnection` (RabbitMQ only) defaults to `false`. When `true`, Strategy 1 is skipped entirely. Use for vault-rotation services where the DI `IConnection` is managed by MassTransit and should not be used as the health signal.
- `ConnectionStringName` (Npgsql only) defaults to `null`. When set, Strategy 1 uses only `GetConnectionString(ConnectionStringName)` rather than trying `DefaultConnection` and `Database`.

### US-6: MassTransit co-existence

As a service already using MassTransit >= 8,
I want clear documentation that MassTransit registers its own RabbitMQ health check via `AddMassTransit()`,
so that I do not accidentally register a duplicate that causes double-counting or misleading health endpoints.

**Acceptance criteria:**
- Package README explicitly calls out the MassTransit duplication concern.
- A startup `Warning` log is emitted if `AddInnagoRabbitMq` detects any existing `IHealthCheck` registration whose name matches `options.Name`.
- A startup `Debug` log names the concrete type of `IConnection` found during Strategy 1 probing.
- `IgnoreDiConnection = true` is the recommended option for MassTransit services.
- No compile-time or runtime conflict if both are registered (duplicate is harmless, just redundant).

---

## Tri-Strategy Resolution Contract

### RabbitMQ strategy evaluation order

| Priority | Strategy | Condition for selection | Skipped when | Connection proof |
|----------|----------|------------------------|--------------|-----------------|
| 1 | DI IConnection | `IServiceProvider.GetService<IConnection>() != null` | `IgnoreDiConnection = true` | `connection.IsOpen == true` |
| 2 | Vault secrets file | File at `VaultSecretsPath` exists | — | Open a new `IConnection` via `ConnectionFactory`, check `IsOpen`, dispose |
| 3 | IConfiguration fallback | Always evaluated **only if vault file was absent** | Vault file existed (even if incomplete) | Same as strategy 2 |

**Vault gate rule:** If the vault file exists at `VaultSecretsPath`, Strategy 3 is never attempted regardless of Strategy 2's outcome. A present-but-incomplete vault file returns `Unhealthy`, not a Strategy 3 fallthrough.

If no strategy yields a usable configuration, the check returns `Unhealthy` with message `"No RabbitMQ connection or configuration found."`.

### Npgsql strategy evaluation order

| Priority | Strategy | Condition for selection | Connection proof |
|----------|----------|------------------------|-----------------|
| 1 | IConfiguration connection string | `GetConnectionString(ConnectionStringName ?? "DefaultConnection")` or `GetConnectionString("Database")` is non-null/non-empty | `OpenAsync` + `SELECT 1` (or `CommandText`) |
| 2 | Vault secrets file | File at `VaultSecretsPath` exists; reads `db_host`, `db_username`, `db_password`, `db_name` | Same |
| 3 | Individual config keys | `db_host` / `DB_HOST` + credentials from IConfiguration | Same |

Same vault gate rule applies: if the vault file exists, Strategy 3 is never attempted.

If no strategy yields a usable configuration, the check returns `Unhealthy` with message `"No Npgsql connection string or configuration found."`.

---

## Configuration Keys — Exhaustive Reference

### RabbitMQ vault secrets file keys (read from `VaultSecretsPath`)

| JSON key | Maps to | Behaviour if missing |
|----------|---------|--------------------|
| `rabbit_host` | Hostname | Required — if absent, `Unhealthy("Vault file present but rabbit_host is missing.")` |
| `rabbit_username` | Username | Required — if absent when host is present, `Unhealthy("Vault file present but rabbit_username is missing.")` |
| `rabbit_password` | Password | Required — if absent when host is present, `Unhealthy("Vault file present but rabbit_password is missing.")` |
| `rabbit_virtualhost` | Virtual host | Optional, defaults to `"/"` |
| `rabbit_port` | Port | Optional, defaults to `5672` |

Credentials do **not** fall back to `"guest"/"guest"` when read from the vault file. Missing credentials when the host key is present is a misconfiguration, returned as `Unhealthy`.

### RabbitMQ IConfiguration fallback keys (Strategy 3 only)

```
RabbitMQ:Host  →  MassTransit:RabbitMq:Host  →  rabbit_host
RabbitMQ:Username  →  MassTransit:RabbitMq:Username  →  rabbit_username
RabbitMQ:Password  →  MassTransit:RabbitMq:Password  →  rabbit_password
RabbitMQ:VirtualHost  →  MassTransit:RabbitMq:VirtualHost  →  rabbit_virtualhost
```

Username and password default to `"guest"` only in Strategy 3 where vault is not involved.

### Npgsql vault secrets file keys

| JSON key | Maps to | Behaviour if missing |
|----------|---------|---------------------|
| `db_host` | Host | Required — if absent, `Unhealthy("Vault file present but db_host is missing.")` |
| `db_username` | Username | Required — if absent when host is present, `Unhealthy` |
| `db_password` | Password | Required — if absent when host is present, `Unhealthy` |
| `db_name` | Database name | Required — if absent when host is present, `Unhealthy` |
| `db_port` | Port | Optional, defaults to `5432` |

### Npgsql IConfiguration individual key fallback (Strategy 3)

```
db_host  or  DB_HOST
db_username
db_password
db_name  or  DB_NAME
db_port  or  DB_PORT  (default 5432)
```

---

## Error Handling Contract

| Failure scenario | Result |
|-----------------|--------|
| Strategy 1: `IConnection` exists but `IsOpen == false` | `Unhealthy("RabbitMQ connection is not open.")` |
| Strategy 1: `IConnection` exists, open check throws | `Unhealthy("Connection probe failed. See application logs for details.")` — exception logged, not attached |
| Strategy 2: Vault file exists but JSON is malformed | `Unhealthy("Vault file could not be parsed. See application logs for details.")` — do NOT fall through |
| Strategy 2: Vault file exists, `rabbit_host` / `db_host` absent | `Unhealthy("Vault file present but <host key> is missing.")` — do NOT fall through |
| Strategy 2: Vault file exists, host present, credentials absent | `Unhealthy("Vault file present but <credential key> is missing.")` |
| Strategy 2 / 3: Connection open fails | `Unhealthy("Connection probe failed. See application logs for details.")` — full exception logged via ILogger, not attached to result |
| Vault file absent, no strategy applies | `Unhealthy("No <type> connection or configuration found.")` |
| `CancellationToken` cancelled during probe | Propagate `OperationCanceledException` (standard ASP.NET Core health check behavior) |

**Exception attachment rule:** No raw `Exception` is ever attached to `HealthCheckResult`. Full exceptions (including stack traces) are logged via `ILogger` at `Warning` level. Only sanitized string descriptions appear in health check results to prevent credential leakage via `IHealthCheckPublisher` implementations.

---

## Public API Surface

### `Innago.Shared.HealthChecks.RabbitMq`

```csharp
namespace Innago.Shared.HealthChecks.RabbitMq;

public static class HealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddInnagoRabbitMq(
        this IHealthChecksBuilder builder,
        string[]? tags = null) { }

    public static IHealthChecksBuilder AddInnagoRabbitMq(
        this IHealthChecksBuilder builder,
        Action<RabbitMqHealthCheckOptions> configure) { }
}

public sealed class RabbitMqHealthCheckOptions
{
    public string VaultSecretsPath { get; set; }     // default: /vault/secrets/appsettings.json
    public TimeSpan ConnectionTimeout { get; set; }  // default: 5 seconds
    public string[] Tags { get; set; }               // default: ["ready"]
    public string Name { get; set; }                 // default: "rabbitmq"
    public bool IgnoreDiConnection { get; set; }     // default: false
}
```

### `Innago.Shared.HealthChecks.Npgsql`

```csharp
namespace Innago.Shared.HealthChecks.Npgsql;

public static class HealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddInnagoNpgsql(
        this IHealthChecksBuilder builder,
        string[]? tags = null) { }

    public static IHealthChecksBuilder AddInnagoNpgsql(
        this IHealthChecksBuilder builder,
        Action<NpgsqlHealthCheckOptions> configure) { }
}

public sealed class NpgsqlHealthCheckOptions
{
    public string VaultSecretsPath { get; set; }      // default: /vault/secrets/appsettings.json
    public TimeSpan ConnectionTimeout { get; set; }   // default: 5 seconds
    public string[] Tags { get; set; }                // default: ["ready"]
    public string Name { get; set; }                  // default: "npgsql"
    public string CommandText { get; set; }           // default: "SELECT 1"
    public string? ConnectionStringName { get; set; } // default: null (tries DefaultConnection, Database)
}
```

Internal implementation types (`RabbitMqHealthCheck`, `NpgsqlHealthCheck`, `VaultSecretsReader`) are **not** public.

---

## Out of Scope

- MassTransit auto-detection (`skipIfMassTransit`) — documented instead; use `IgnoreDiConnection = true`
- Redis health check (use existing `AspNetCore.HealthChecks.Redis`)
- MariaDB health check
- Vault secrets path auto-discovery
- Circuit breaker / retry logic within the health check probe
