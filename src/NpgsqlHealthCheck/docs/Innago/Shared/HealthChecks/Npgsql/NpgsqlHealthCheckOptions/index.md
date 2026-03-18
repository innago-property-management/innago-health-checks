### [Innago\.Shared\.HealthChecks\.Npgsql](../index.md 'Innago\.Shared\.HealthChecks\.Npgsql')

## NpgsqlHealthCheckOptions Class

Configuration options for the Innago Npgsql health check\.

```csharp
public sealed class NpgsqlHealthCheckOptions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') &#129106; NpgsqlHealthCheckOptions

| Properties | |
| :--- | :--- |
| [CommandText](CommandText.md 'Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions\.CommandText') | SQL command to verify database connectivity\. Default: `"SELECT 1"`\. |
| [ConnectionStringName](ConnectionStringName.md 'Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions\.ConnectionStringName') | Explicit connection string name\. Default: null \(tries DefaultConnection then Database\)\. |
| [ConnectionTimeout](ConnectionTimeout.md 'Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions\.ConnectionTimeout') | Timeout for opening a test connection\. Default: 5 seconds\. |
| [Name](Name.md 'Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions\.Name') | Health check registration name\. Default: `"npgsql"`\. |
| [Tags](Tags.md 'Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions\.Tags') | Health check tags\. Default: `["ready"]`\. |
| [VaultSecretsPath](VaultSecretsPath.md 'Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions\.VaultSecretsPath') | Path to the Vault\-injected secrets file\. Default: `/vault/secrets/appsettings.json`\. |
