# Dynamic Health Checks — RabbitMQ + Npgsql

## Decision

Add two new NuGet packages to this repo alongside the existing TcpHealthProbe:
- `Innago.Shared.HealthChecks.RabbitMq`
- `Innago.Shared.HealthChecks.Npgsql`

Rename the repo from `innago-tcp-health-probe` to `innago-health-checks` (GitHub rename, cosmetic — NuGet package names are independent).

## Business Value

18 services currently have copy-pasted health check code from a prior coordinated effort. Those PRs are all open and unmergeable because the code doesn't handle both static (current) and dynamic vault (future) connection patterns. A shared NuGet package eliminates 18 copies and makes the fix a one-line dependency + registration change per service.

## Problem

Services need health checks that work for:
1. **Current state (static):** Connection strings/credentials read once at startup from config or vault-injected env vars
2. **Future state (dynamic):** Vault rotates credentials periodically; health check must re-read the vault secrets file on each probe

The v1 health checks (`DynamicRabbitMqHealthCheck`) always read from the vault file, which doesn't work for services still using static connections. Services using MassTransit have a third pattern (built-in health checks).

## End State

### Repo Structure
```
innago-health-checks/              (renamed from innago-tcp-health-probe)
├── HealthChecks.slnx              (renamed from TcpHealthProbe.slnx)
├── src/
│   ├── TcpHealthProbe/            (existing, unchanged)
│   │   └── Innago.Shared.HealthChecks.TcpHealthProbe.csproj
│   ├── RabbitMqHealthCheck/       (NEW)
│   │   ├── Innago.Shared.HealthChecks.RabbitMq.csproj
│   │   ├── RabbitMqHealthCheck.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   ├── PublicAPI.Shipped.txt
│   │   ├── PublicAPI.Unshipped.txt
│   │   └── README.md
│   └── NpgsqlHealthCheck/         (NEW)
│       ├── Innago.Shared.HealthChecks.Npgsql.csproj
│       ├── NpgsqlHealthCheck.cs
│       ├── ServiceCollectionExtensions.cs
│       ├── PublicAPI.Shipped.txt
│       ├── PublicAPI.Unshipped.txt
│       └── README.md
├── tests/
│   ├── UnitTests/                 (existing, add new tests)
│   ├── RabbitMqHealthCheck.Tests/ (NEW)
│   └── NpgsqlHealthCheck.Tests/   (NEW)
└── examples/
    └── ConsoleAppWithProbe/       (existing)
```

### NuGet Packages Produced
| Package | Dependencies | Purpose |
|---------|-------------|---------|
| `Innago.Shared.HealthChecks.TcpHealthProbe` | (existing) | TCP probe for console apps |
| `Innago.Shared.HealthChecks.RabbitMq` | `RabbitMQ.Client`, `Microsoft.Extensions.Diagnostics.HealthChecks` | RabbitMQ connectivity check |
| `Innago.Shared.HealthChecks.Npgsql` | `Npgsql`, `Microsoft.Extensions.Diagnostics.HealthChecks` | PostgreSQL connectivity check |

### RabbitMqHealthCheck — Tri-Strategy Pattern

The health check tries strategies in order, using the first that applies:

1. **DI IConnection** — If `RabbitMQ.Client.IConnection` is registered in DI, check `connection.IsOpen`. This proves the app's actual connection works (static pattern, current state).

2. **Vault secrets file** — If `/vault/secrets/appsettings.json` exists, read it fresh every probe. Extract `rabbit_host`, `rabbit_username`, `rabbit_password`, `rabbit_virtualhost`. Open a test connection. This handles dynamic secret rotation (future state).

3. **IConfiguration fallback** — Read from `IConfiguration` using common key patterns (`RabbitMQ:Host`, `MassTransit:RabbitMq:Host`, `rabbit_host`). Open a test connection. This handles vault-injected env vars that populate IConfiguration.

### NpgsqlHealthCheck — Tri-Strategy Pattern

Same approach for database:

1. **IConfiguration connection string** — `GetConnectionString("DefaultConnection")` or `GetConnectionString("Database")`. Standard .NET pattern.

2. **Vault secrets file** — Read `db_host`, `db_username`, `db_password`, `db_name` from `/vault/secrets/appsettings.json`. Build connection string dynamically.

3. **Individual config keys** — Read `db_username`, `db_password`, `db_host`, `DB_HOST`, `DB_NAME` from `IConfiguration`. Handles vault-injected env var pattern.

### Consumer API (Extension Methods)

```csharp
// In service's Program.cs or Startup.cs:

// RabbitMQ health check
services.AddHealthChecks()
    .AddInnagoRabbitMq(tags: new[] { "ready" });

// PostgreSQL health check
services.AddHealthChecks()
    .AddInnagoNpgsql(tags: new[] { "ready" });

// Both
services.AddHealthChecks()
    .AddInnagoRabbitMq(tags: new[] { "ready" })
    .AddInnagoNpgsql(tags: new[] { "ready" });
```

Optional configuration:
```csharp
.AddInnagoRabbitMq(options =>
{
    options.VaultSecretsPath = "/vault/secrets/custom-path.json";
    options.ConnectionTimeout = TimeSpan.FromSeconds(10);
    options.Tags = new[] { "ready", "rabbitmq" };
})
```

### MassTransit Considerations

Services using MassTransit >= 8 already get automatic RabbitMQ health checks from MassTransit's `AddMassTransit()`. For these services:
- Do NOT add `.AddInnagoRabbitMq()` — it would duplicate
- The NuGet package should document this clearly in the README
- Future: consider a `.AddInnagoRabbitMq(skipIfMassTransit: true)` option that detects MassTransit registration

### Conventions (from existing TcpHealthProbe)

- Target: `net9.0`
- `LangVersion`: latest
- Nullable: enable
- AOT-compatible: `PublishAot=true`, use `EnableConfigurationBindingGenerator`
- Analyzers: BannedApiAnalyzers, PublicApiAnalyzers, SonarAnalyzer
- Public API tracking via PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt
- `GeneratePackageOnBuild=true`
- Package published to GitHub Packages (ghcr.io/innago-property-management)
- CI via Oui-Deliver reusable workflows

### Test Strategy

- Unit tests with mocked IServiceProvider, IConfiguration
- Integration tests with testcontainers (RabbitMQ, PostgreSQL) — optional, lower priority
- Approval tests for public API surface

### What Changes in the 18 Service Repos

Each service's health check PR becomes:
1. Add NuGet reference: `<PackageReference Include="Innago.Shared.HealthChecks.RabbitMq" Version="1.0.0" />`
2. One line in registration: `.AddInnagoRabbitMq(tags: new[] { "ready" })`
3. Delete the copy-pasted `DynamicRabbitMqHealthCheck.cs`, `RabbitMqConfigService.cs`, `DynamicNpgSqlHealthCheck.cs`
4. Same for Npgsql if applicable

## Scope Decisions

- **In scope:** RabbitMQ health check, Npgsql health check, extension methods, tests
- **Out of scope:** MassTransit auto-detection (document in README instead), Redis health check (use existing AspNetCore.HealthChecks.Redis), MariaDB health check (separate concern)
- **Deferred:** Vault secrets path auto-discovery (hardcode default, allow override)
