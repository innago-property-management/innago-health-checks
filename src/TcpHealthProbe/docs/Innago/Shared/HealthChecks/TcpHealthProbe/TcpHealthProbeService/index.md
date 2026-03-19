### [Innago\.Shared\.HealthChecks\.TcpHealthProbe](../index.md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe')

## TcpHealthProbeService Class

Represents a background service that listens on a specific TCP port and uses a health check service
to provide health status updates\.

```csharp
public class TcpHealthProbeService : Microsoft.Extensions.Hosting.BackgroundService
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') &#129106; [Microsoft\.Extensions\.Hosting\.BackgroundService](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice 'Microsoft\.Extensions\.Hosting\.BackgroundService') &#129106; TcpHealthProbeService

| Constructors | |
| :--- | :--- |
| [TcpHealthProbeService\(HealthCheckService, ILogger&lt;TcpHealthProbeService&gt;, IConfiguration\)](TcpHealthProbeService(HealthCheckService,ILogger_TcpHealthProbeService_,IConfiguration).md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe\.TcpHealthProbeService\.TcpHealthProbeService\(Microsoft\.Extensions\.Diagnostics\.HealthChecks\.HealthCheckService, Microsoft\.Extensions\.Logging\.ILogger\<Innago\.Shared\.HealthChecks\.TcpHealthProbe\.TcpHealthProbeService\>, Microsoft\.Extensions\.Configuration\.IConfiguration\)') | Initializes a new instance of the [TcpHealthProbeService](index.md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe\.TcpHealthProbeService') class\. |

| Methods | |
| :--- | :--- |
| [ExecuteAsync\(CancellationToken\)](ExecuteAsync(CancellationToken).md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe\.TcpHealthProbeService\.ExecuteAsync\(System\.Threading\.CancellationToken\)') | Executes the background service\. |
| [UpdateHeartbeatAsync\(CancellationToken\)](UpdateHeartbeatAsync(CancellationToken).md 'Innago\.Shared\.HealthChecks\.TcpHealthProbe\.TcpHealthProbeService\.UpdateHeartbeatAsync\(System\.Threading\.CancellationToken\)') | Updates the heartbeat\. |
