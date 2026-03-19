### [Innago\.Shared\.HealthChecks\.RabbitMq](../index.md 'Innago\.Shared\.HealthChecks\.RabbitMq').[VaultSecretsReader](index.md 'Innago\.Shared\.HealthChecks\.RabbitMq\.VaultSecretsReader')

## VaultSecretsReader\.TryRead\(string, ILogger\) Method

Returns:
  HasSucceeded \+ null         = file was absent; caller may fall through
  HasSucceeded \+ VaultSecrets = file read and parsed; caller uses \.Values
  HasFailed    \+ Exception    = file is present but unreadable/unparseable;
                                caller must return Unhealthy — do NOT fall through

```csharp
internal static Innago.Shared.TryHelpers.Result<System.Nullable<Innago.Shared.HealthChecks.RabbitMq.VaultSecrets>> TryRead(string path, Microsoft.Extensions.Logging.ILogger logger);
```
#### Parameters

<a name='Innago.Shared.HealthChecks.RabbitMq.VaultSecretsReader.TryRead(string,Microsoft.Extensions.Logging.ILogger).path'></a>

`path` [System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a name='Innago.Shared.HealthChecks.RabbitMq.VaultSecretsReader.TryRead(string,Microsoft.Extensions.Logging.ILogger).logger'></a>

`logger` [Microsoft\.Extensions\.Logging\.ILogger](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger 'Microsoft\.Extensions\.Logging\.ILogger')

#### Returns
[Innago\.Shared\.TryHelpers\.Result&lt;](https://learn.microsoft.com/en-us/dotnet/api/innago.shared.tryhelpers.result-1 'Innago\.Shared\.TryHelpers\.Result\`1')[System\.Nullable&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[Innago\.Shared\.HealthChecks\.RabbitMq\.VaultSecrets](https://learn.microsoft.com/en-us/dotnet/api/innago.shared.healthchecks.rabbitmq.vaultsecrets 'Innago\.Shared\.HealthChecks\.RabbitMq\.VaultSecrets')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1 'System\.Nullable\`1')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/innago.shared.tryhelpers.result-1 'Innago\.Shared\.TryHelpers\.Result\`1')