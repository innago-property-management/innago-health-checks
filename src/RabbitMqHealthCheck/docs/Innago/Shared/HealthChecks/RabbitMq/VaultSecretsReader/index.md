### [Innago\.Shared\.HealthChecks\.RabbitMq](../index.md 'Innago\.Shared\.HealthChecks\.RabbitMq')

## VaultSecretsReader Class

```csharp
internal static class VaultSecretsReader
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') &#129106; VaultSecretsReader

| Methods | |
| :--- | :--- |
| [TryRead\(string, ILogger\)](TryRead(string,ILogger).md 'Innago\.Shared\.HealthChecks\.RabbitMq\.VaultSecretsReader\.TryRead\(string, Microsoft\.Extensions\.Logging\.ILogger\)') | Returns:   HasSucceeded \+ null         = file was absent; caller may fall through   HasSucceeded \+ VaultSecrets = file read and parsed; caller uses \.Values   HasFailed    \+ Exception    = file is present but unreadable/unparseable;                                 caller must return Unhealthy — do NOT fall through |
