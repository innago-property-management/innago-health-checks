### [Innago\.Shared\.HealthChecks\.RabbitMq](../index.md 'Innago\.Shared\.HealthChecks\.RabbitMq').[HealthChecksBuilderExtensions](index.md 'Innago\.Shared\.HealthChecks\.RabbitMq\.HealthChecksBuilderExtensions')

## HealthChecksBuilderExtensions\.AddInnagoRabbitMq Method

| Overloads | |
| :--- | :--- |
| [AddInnagoRabbitMq\(this IHealthChecksBuilder\)](AddInnagoRabbitMq.md#Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder) 'Innago\.Shared\.HealthChecks\.RabbitMq\.HealthChecksBuilderExtensions\.AddInnagoRabbitMq\(this Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder\)') | Adds the Innago RabbitMQ health check with default options\. Tags default to `["ready"]`; use the overload with [System\.Action&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1') for customisation\. |
| [AddInnagoRabbitMq\(this IHealthChecksBuilder, Action&lt;RabbitMqHealthCheckOptions&gt;\)](AddInnagoRabbitMq.md#Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions_) 'Innago\.Shared\.HealthChecks\.RabbitMq\.HealthChecksBuilderExtensions\.AddInnagoRabbitMq\(this Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder, System\.Action\<Innago\.Shared\.HealthChecks\.RabbitMq\.RabbitMqHealthCheckOptions\>\)') | Adds the Innago RabbitMQ health check with full options configuration\. |

<a name='Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder)'></a>

## HealthChecksBuilderExtensions\.AddInnagoRabbitMq\(this IHealthChecksBuilder\) Method

Adds the Innago RabbitMQ health check with default options\.
Tags default to `["ready"]`; use the overload with [System\.Action&lt;&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1') for customisation\.

```csharp
public static Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder AddInnagoRabbitMq(this Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder builder);
```
#### Parameters

<a name='Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder).builder'></a>

`builder` [Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder')

#### Returns
[Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder')

<a name='Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions_)'></a>

## HealthChecksBuilderExtensions\.AddInnagoRabbitMq\(this IHealthChecksBuilder, Action\<RabbitMqHealthCheckOptions\>\) Method

Adds the Innago RabbitMQ health check with full options configuration\.

```csharp
public static Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder AddInnagoRabbitMq(this Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder builder, System.Action<Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions> configure);
```
#### Parameters

<a name='Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions_).builder'></a>

`builder` [Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder')

<a name='Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[RabbitMqHealthCheckOptions](../RabbitMqHealthCheckOptions/index.md 'Innago\.Shared\.HealthChecks\.RabbitMq\.RabbitMqHealthCheckOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

#### Returns
[Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder')