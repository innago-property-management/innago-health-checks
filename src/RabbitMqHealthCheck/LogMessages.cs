namespace Innago.Shared.HealthChecks.RabbitMq;

using System;

using Microsoft.Extensions.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(LogLevel.Debug, Message = "RabbitMQ health check: trying DI IConnection strategy.")]
    public static partial void StrategyDiConnection(this ILogger<RabbitMqHealthCheck> logger);

    [LoggerMessage(LogLevel.Debug, Message = "RabbitMQ health check: DI IConnection found (type: {TypeName}).")]
    public static partial void DiConnectionFound(this ILogger<RabbitMqHealthCheck> logger, string typeName);

    [LoggerMessage(LogLevel.Debug, Message = "RabbitMQ health check: trying vault secrets file at {Path}.")]
    public static partial void StrategyVaultFile(this ILogger<RabbitMqHealthCheck> logger, string path);

    [LoggerMessage(LogLevel.Debug, Message = "RabbitMQ health check: trying IConfiguration fallback.")]
    public static partial void StrategyIConfiguration(this ILogger<RabbitMqHealthCheck> logger);

    [LoggerMessage(LogLevel.Warning, Message = "RabbitMQ health check: vault file at {Path} could not be read or parsed.")]
    public static partial void VaultFileReadError(this ILogger logger, string path, Exception exception);

    [LoggerMessage(LogLevel.Warning, Message = "RabbitMQ health check: connection probe failed.")]
    public static partial void ProbeConnectionFailed(this ILogger<RabbitMqHealthCheck> logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, Message = "RabbitMQ health check: no strategy succeeded — no connection or configuration found.")]
    public static partial void NoStrategyFound(this ILogger<RabbitMqHealthCheck> logger);
}
