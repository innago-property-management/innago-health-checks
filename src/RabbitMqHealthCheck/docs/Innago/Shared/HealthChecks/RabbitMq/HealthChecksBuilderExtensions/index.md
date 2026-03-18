### [Innago\.Shared\.HealthChecks\.RabbitMq](../index.md 'Innago\.Shared\.HealthChecks\.RabbitMq')

## HealthChecksBuilderExtensions Class

Extension methods for registering the Innago RabbitMQ health check\.

```csharp
public static class HealthChecksBuilderExtensions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') &#129106; HealthChecksBuilderExtensions

| Methods | |
| :--- | :--- |
| [AddInnagoRabbitMq\(this IHealthChecksBuilder\)](AddInnagoRabbitMq.md#Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder) 'Innago\.Shared\.HealthChecks\.RabbitMq\.HealthChecksBuilderExtensions\.AddInnagoRabbitMq\(this Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder\)') | Adds the Innago RabbitMQ health check with default options\. Tags default to `["ready"]`; use the overload with [System\.Action&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1') for customisation\. |
| [AddInnagoRabbitMq\(this IHealthChecksBuilder, Action&lt;RabbitMqHealthCheckOptions&gt;\)](AddInnagoRabbitMq.md#Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions_) 'Innago\.Shared\.HealthChecks\.RabbitMq\.HealthChecksBuilderExtensions\.AddInnagoRabbitMq\(this Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder, System\.Action\<Innago\.Shared\.HealthChecks\.RabbitMq\.RabbitMqHealthCheckOptions\>\)') | Adds the Innago RabbitMQ health check with full options configuration\. |
