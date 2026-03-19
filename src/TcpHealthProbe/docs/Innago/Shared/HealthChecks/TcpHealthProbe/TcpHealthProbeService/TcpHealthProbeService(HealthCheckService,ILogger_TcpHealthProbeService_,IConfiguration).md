### [Innago\.Shared\.HealthChecks\.TcpHealthProbe](../index.md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe').[TcpHealthProbeService](index.md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe\.TcpHealthProbeService')

## TcpHealthProbeService\(HealthCheckService, ILogger\<TcpHealthProbeService\>, IConfiguration\) Constructor

Initializes a new instance of the [TcpHealthProbeService](index.md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe\.TcpHealthProbeService') class\.

```csharp
public TcpHealthProbeService(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthCheckService, Microsoft.Extensions.Logging.ILogger<Innago.Shared.HealthChecks.TcpHealthProbe.TcpHealthProbeService> logger, Microsoft.Extensions.Configuration.IConfiguration configuration);
```
#### Parameters

<a name='Innago.Shared.HealthChecks.TcpHealthProbe.TcpHealthProbeService.TcpHealthProbeService(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService,Microsoft.Extensions.Logging.ILogger_Innago.Shared.HealthChecks.TcpHealthProbe.TcpHealthProbeService_,Microsoft.Extensions.Configuration.IConfiguration).healthCheckService'></a>

`healthCheckService` [Microsoft\.Extensions\.Diagnostics\.HealthChecks\.HealthCheckService](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.diagnostics.healthchecks.healthcheckservice 'Microsoft\.Extensions\.Diagnostics\.HealthChecks\.HealthCheckService')

The health check service\.

<a name='Innago.Shared.HealthChecks.TcpHealthProbe.TcpHealthProbeService.TcpHealthProbeService(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService,Microsoft.Extensions.Logging.ILogger_Innago.Shared.HealthChecks.TcpHealthProbe.TcpHealthProbeService_,Microsoft.Extensions.Configuration.IConfiguration).logger'></a>

`logger` [Microsoft\.Extensions\.Logging\.ILogger&lt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1 'Microsoft\.Extensions\.Logging\.ILogger\`1')[TcpHealthProbeService](index.md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe\.TcpHealthProbeService')[&gt;](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger-1 'Microsoft\.Extensions\.Logging\.ILogger\`1')

The logger\.

<a name='Innago.Shared.HealthChecks.TcpHealthProbe.TcpHealthProbeService.TcpHealthProbeService(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService,Microsoft.Extensions.Logging.ILogger_Innago.Shared.HealthChecks.TcpHealthProbe.TcpHealthProbeService_,Microsoft.Extensions.Configuration.IConfiguration).configuration'></a>

`configuration` [Microsoft\.Extensions\.Configuration\.IConfiguration](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration.iconfiguration 'Microsoft\.Extensions\.Configuration\.IConfiguration')

The configuration\.