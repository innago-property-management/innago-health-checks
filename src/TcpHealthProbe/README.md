# Innago.Shared.HealthChecks.TcpHealthProbe

A background service that exposes ASP.NET Core `HealthCheckService` results as a TCP port probe -- enabling Kubernetes-style liveness checks for console applications that don't use HTTP.

When healthy, a `TcpListener` accepts connections on the configured port. When unhealthy or degraded, the listener is stopped and the port becomes unreachable, causing the probe to fail.

## Installation

```xml
<PackageReference Include="Innago.Shared.HealthChecks.TcpHealthProbe" Version="2.*" />
```

## Usage

```csharp
// Register health checks as usual
builder.Services.AddHealthChecks();

// Add the TCP health probe as a hosted service
builder.Services.AddHostedService<TcpHealthProbeService>();
```

### Configuration

Add to `appsettings.json` (both values are optional):

```json
{
  "HealthProbeConfiguration": {
    "Port": 5555,
    "RefreshSeconds": 1
  }
}
```

### Kubernetes probe

```yaml
livenessProbe:
  tcpSocket:
    port: 5555
  initialDelaySeconds: 5
  periodSeconds: 10
```

## API Documentation

[View full API docs](https://github.com/innago-property-management/innago-health-checks/tree/main/src/TcpHealthProbe/docs)
