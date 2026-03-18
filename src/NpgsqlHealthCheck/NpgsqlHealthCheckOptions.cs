namespace Innago.Shared.HealthChecks.Npgsql;

using System;

using JetBrains.Annotations;

/// <summary>Configuration options for the Innago Npgsql health check.</summary>
[PublicAPI]
public sealed class NpgsqlHealthCheckOptions
{
    /// <summary>Path to the Vault-injected secrets file. Default: <c>/vault/secrets/appsettings.json</c>.</summary>
    public string VaultSecretsPath { get; set; } = "/vault/secrets/appsettings.json";

    /// <summary>Timeout for opening a test connection. Default: 5 seconds.</summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Health check tags. Default: <c>["ready"]</c>.</summary>
    public string[] Tags { get; set; } = ["ready"];

    /// <summary>Health check registration name. Default: <c>"npgsql"</c>.</summary>
    public string Name { get; set; } = "npgsql";

    /// <summary>SQL command to verify database connectivity. Default: <c>"SELECT 1"</c>.</summary>
    public string CommandText { get; set; } = "SELECT 1";

    /// <summary>Explicit connection string name. Default: null (tries DefaultConnection then Database).</summary>
    public string? ConnectionStringName { get; set; }
}
