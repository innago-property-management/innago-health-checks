namespace Innago.Shared.HealthChecks.TcpHealthProbe;

using System;

using Microsoft.Extensions.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(LogLevel.Information, Message = "{Message}")]
    public static partial void TcpHealthProbeServiceInformation(this ILogger<TcpHealthProbeService> logger, string message);
    
    [LoggerMessage(LogLevel.Debug, Message = "{Message}")]
    public static partial void TcpHealthProbeServiceDebug(this ILogger<TcpHealthProbeService> logger, string message);

    public static void TcpHealthProbeServiceCritical(this ILogger<TcpHealthProbeService> logger, Exception exception)
    {
        TcpHealthProbeServiceCriticalDefinition(logger, exception.Message, exception);
    }
    
    private static Action<ILogger<TcpHealthProbeService>, string, Exception> TcpHealthProbeServiceCriticalDefinition => MakeErrorDefinition("{Message}", nameof(TcpHealthProbeService));
    
    private static Action<ILogger, string, Exception> MakeErrorDefinition(string formatString, string? eventIdName = null)
    {
        return LoggerMessage.Define<string>(LogLevel.Error, new EventId(0, eventIdName), formatString);
    }
}