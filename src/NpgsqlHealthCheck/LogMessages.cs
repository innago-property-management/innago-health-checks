namespace Innago.Shared.HealthChecks.Npgsql;

using System;

using Microsoft.Extensions.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(LogLevel.Debug, Message = "Npgsql health check: trying connection string strategy.")]
    public static partial void StrategyConnectionString(this ILogger<NpgsqlHealthCheck> logger);

    [LoggerMessage(LogLevel.Debug, Message = "Npgsql health check: trying vault secrets file at {Path}.")]
    public static partial void StrategyVaultFile(this ILogger<NpgsqlHealthCheck> logger, string path);

    [LoggerMessage(LogLevel.Debug, Message = "Npgsql health check: trying IConfiguration fallback.")]
    public static partial void StrategyIConfiguration(this ILogger<NpgsqlHealthCheck> logger);

    [LoggerMessage(LogLevel.Warning, Message = "Npgsql health check: vault file at {Path} could not be read or parsed.")]
    public static partial void VaultFileReadError(this ILogger logger, string path, Exception exception);

    [LoggerMessage(LogLevel.Warning, Message = "Npgsql health check: connection probe failed.")]
    public static partial void ProbeConnectionFailed(this ILogger<NpgsqlHealthCheck> logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, Message = "Npgsql health check: no strategy succeeded — no connection string or configuration found.")]
    public static partial void NoStrategyFound(this ILogger<NpgsqlHealthCheck> logger);
}
