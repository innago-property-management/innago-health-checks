### [Innago\.Shared\.HealthChecks\.RabbitMq](../index.md 'Innago\.Shared\.HealthChecks\.RabbitMq')

## RabbitMqHealthCheckOptions Class

Configuration options for the Innago RabbitMQ health check\.

```csharp
public sealed class RabbitMqHealthCheckOptions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') &#129106; RabbitMqHealthCheckOptions

| Properties | |
| :--- | :--- |
| [ConnectionTimeout](ConnectionTimeout.md 'Innago\.Shared\.HealthChecks\.RabbitMq\.RabbitMqHealthCheckOptions\.ConnectionTimeout') | Timeout for opening a test connection\. Default: 5 seconds\. |
| [IgnoreDiConnection](IgnoreDiConnection.md 'Innago\.Shared\.HealthChecks\.RabbitMq\.RabbitMqHealthCheckOptions\.IgnoreDiConnection') | When true, Strategy 1 \(DI IConnection\) is skipped\. Default: false\. |
| [Name](Name.md 'Innago\.Shared\.HealthChecks\.RabbitMq\.RabbitMqHealthCheckOptions\.Name') | Health check registration name\. Default: `"rabbitmq"`\. |
| [Tags](Tags.md 'Innago\.Shared\.HealthChecks\.RabbitMq\.RabbitMqHealthCheckOptions\.Tags') | Health check tags\. Default: `["ready"]`\. |
| [VaultSecretsPath](VaultSecretsPath.md 'Innago\.Shared\.HealthChecks\.RabbitMq\.RabbitMqHealthCheckOptions\.VaultSecretsPath') | Path to the Vault\-injected secrets file\. Default: `/vault/secrets/appsettings.json`\. |
