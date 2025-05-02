namespace Innago.Shared.HealthChecks.TcpHealthProbe;

/// <summary>
/// Configuration for the TCP health probe.
/// </summary>
public record HealthProbeConfiguration(int Port = 5555, int RefreshSeconds = 1);