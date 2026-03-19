# Innago.Shared.HealthChecks.Npgsql

Tri-strategy PostgreSQL health check for ASP.NET Core. Resolves connection credentials through three strategies in priority order, so it works across connection strings, Vault-rotated secrets, and individual config keys.

## Installation

```xml
<PackageReference Include="Innago.Shared.HealthChecks.Npgsql" Version="2.*" />
```

## Usage

```csharp
builder.Services.AddHealthChecks()
    .AddInnagoNpgsql();
```

With options:

```csharp
builder.Services.AddHealthChecks()
    .AddInnagoNpgsql(opts =>
    {
        opts.Tags = ["live"];
        opts.ConnectionStringName = "MyDb";
        opts.CommandText = "SELECT 1";
    });
```

## Strategy Resolution

| Priority | Strategy | Source | Condition |
|----------|----------|--------|-----------|
| 1 | Connection string | `IConfiguration.GetConnectionString()` | Tries `ConnectionStringName`, then `DefaultConnection`, then `Database` |
| 2 | Vault secrets file | `/vault/secrets/appsettings.json` | Keys: `db_host`, `db_username`, `db_password`, `db_name` |
| 3 | Individual config keys | `IConfiguration` / env vars | Only if vault file is **absent** |

**Vault gate rule:** If the vault file exists (even if incomplete or malformed), Strategy 3 is never attempted. A present-but-broken vault file returns `Unhealthy`.

### Individual config keys (Strategy 3)

```
db_host  or  DB_HOST
db_username
db_password
db_name  or  DB_NAME
db_port  or  DB_PORT  (default: 5432)
```

## API Documentation

[View full API docs](https://github.com/innago-property-management/innago-health-checks/tree/main/src/NpgsqlHealthCheck/docs)
