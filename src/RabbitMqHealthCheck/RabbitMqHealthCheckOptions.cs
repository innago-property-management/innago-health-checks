namespace Innago.Shared.HealthChecks.RabbitMq;

using System;

using JetBrains.Annotations;

/// <summary>Configuration options for the Innago RabbitMQ health check.</summary>
[PublicAPI]
public sealed class RabbitMqHealthCheckOptions
{
    /// <summary>Path to the Vault-injected secrets file. Default: <c>/vault/secrets/appsettings.json</c>.</summary>
    public string VaultSecretsPath { get; set; } = "/vault/secrets/appsettings.json";

    /// <summary>Timeout for opening a test connection. Default: 5 seconds.</summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Health check tags. Default: <c>["ready"]</c>.</summary>
    public string[] Tags { get; set; } = ["ready"];

    /// <summary>Health check registration name. Default: <c>"rabbitmq"</c>.</summary>
    public string Name { get; set; } = "rabbitmq";

    /// <summary>When true, Strategy 1 (DI IConnection) is skipped. Default: false.</summary>
    public bool IgnoreDiConnection { get; set; }
}
