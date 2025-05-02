namespace Innago.Shared.HealthChecks.TcpHealthProbe;

public record HealthProbe(int Port = 5555, int RefreshSeconds = 1);