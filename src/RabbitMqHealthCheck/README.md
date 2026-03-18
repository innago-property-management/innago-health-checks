# Innago.Shared.HealthChecks.RabbitMq

Tri-strategy RabbitMQ health check for ASP.NET Core. Resolves connection credentials through three strategies in priority order, so it works across static config, Vault-rotated secrets, and DI-managed connections.

## Installation

```xml
<PackageReference Include="Innago.Shared.HealthChecks.RabbitMq" Version="2.*" />
```

## Usage

```csharp
builder.Services.AddHealthChecks()
    .AddInnagoRabbitMq();
```

With options:

```csharp
builder.Services.AddHealthChecks()
    .AddInnagoRabbitMq(opts =>
    {
        opts.Tags = ["live"];
        opts.IgnoreDiConnection = true; // recommended for MassTransit services
    });
```

## Strategy Resolution

| Priority | Strategy | Source | Condition |
|----------|----------|--------|-----------|
| 1 | DI `IConnection` | Service provider | Skipped when `IgnoreDiConnection = true` |
| 2 | Vault secrets file | `/vault/secrets/appsettings.json` | Keys: `rabbit_host`, `rabbit_username`, `rabbit_password` |
| 3 | `IConfiguration` fallback | App config / env vars | Only if vault file is **absent** |

**Vault gate rule:** If the vault file exists (even if incomplete or malformed), Strategy 3 is never attempted. A present-but-broken vault file returns `Unhealthy`.

### IConfiguration key priority (Strategy 3)

```
RabbitMQ:Host  >  MassTransit:RabbitMq:Host  >  rabbit_host
RabbitMQ:Username  >  MassTransit:RabbitMq:Username  >  rabbit_username  (default: guest)
RabbitMQ:Password  >  MassTransit:RabbitMq:Password  >  rabbit_password  (default: guest)
```

## MassTransit Warning

MassTransit >= 8 registers its own RabbitMQ health check via `AddMassTransit()`. To avoid duplicates, either:
- Set `IgnoreDiConnection = true` (recommended), or
- Don't add this package if MassTransit's built-in check is sufficient.

## API Documentation

[View full API docs](https://github.com/innago-property-management/innago-health-checks/tree/main/src/RabbitMqHealthCheck/docs)
