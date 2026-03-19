### [Innago\.Shared\.HealthChecks\.Npgsql](../index.md 'Innago\.Shared\.HealthChecks\.Npgsql').[HealthChecksBuilderExtensions](index.md 'Innago\.Shared\.HealthChecks\.Npgsql\.HealthChecksBuilderExtensions')

## HealthChecksBuilderExtensions\.AddInnagoNpgsql Method

| Overloads | |
| :--- | :--- |
| [AddInnagoNpgsql\(this IHealthChecksBuilder\)](AddInnagoNpgsql.md#Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder) 'Innago\.Shared\.HealthChecks\.Npgsql\.HealthChecksBuilderExtensions\.AddInnagoNpgsql\(this Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder\)') | Adds the Innago Npgsql health check with default options\. |
| [AddInnagoNpgsql\(this IHealthChecksBuilder, Action&lt;NpgsqlHealthCheckOptions&gt;\)](AddInnagoNpgsql.md#Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions_) 'Innago\.Shared\.HealthChecks\.Npgsql\.HealthChecksBuilderExtensions\.AddInnagoNpgsql\(this Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder, System\.Action\<Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions\>\)') | Adds the Innago Npgsql health check with a configuration action\. |

<a name='Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder)'></a>

## HealthChecksBuilderExtensions\.AddInnagoNpgsql\(this IHealthChecksBuilder\) Method

Adds the Innago Npgsql health check with default options\.

```csharp
public static Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder AddInnagoNpgsql(this Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder builder);
```
#### Parameters

<a name='Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder).builder'></a>

`builder` [Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder')

The health checks builder\.

#### Returns
[Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder')  
The [Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder') for chaining\.

<a name='Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions_)'></a>

## HealthChecksBuilderExtensions\.AddInnagoNpgsql\(this IHealthChecksBuilder, Action\<NpgsqlHealthCheckOptions\>\) Method

Adds the Innago Npgsql health check with a configuration action\.

```csharp
public static Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder AddInnagoNpgsql(this Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder builder, System.Action<Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions> configure);
```
#### Parameters

<a name='Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions_).builder'></a>

`builder` [Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder')

The health checks builder\.

<a name='Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(thisMicrosoft.Extensions.DependencyInjection.IHealthChecksBuilder,System.Action_Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions_).configure'></a>

`configure` [System\.Action&lt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')[NpgsqlHealthCheckOptions](../NpgsqlHealthCheckOptions/index.md 'Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.action-1 'System\.Action\`1')

Action to configure [NpgsqlHealthCheckOptions](../NpgsqlHealthCheckOptions/index.md 'Innago\.Shared\.HealthChecks\.Npgsql\.NpgsqlHealthCheckOptions')\.

#### Returns
[Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder')  
The [Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder 'Microsoft\.Extensions\.DependencyInjection\.IHealthChecksBuilder') for chaining\.