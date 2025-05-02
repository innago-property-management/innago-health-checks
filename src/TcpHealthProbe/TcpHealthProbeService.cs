namespace Innago.Shared.HealthChecks.TcpHealthProbe;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
///     Represents a background service that listens on a specific TCP port and uses a health check service
///     to provide health status updates.
/// </summary>
[PublicAPI]
public class TcpHealthProbeService : BackgroundService
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TcpHealthProbeService" /> class.
    /// </summary>
    /// <param name="healthCheckService">The health check service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public TcpHealthProbeService(
        HealthCheckService healthCheckService,
        ILogger<TcpHealthProbeService> logger,
        IConfiguration configuration)
    {
        this.CheckService = healthCheckService;
        this.Logger = logger;

        HealthProbeConfiguration healthProbeConfiguration =
            configuration.GetSection(nameof(HealthProbeConfiguration)).Get<HealthProbeConfiguration>() ?? new HealthProbeConfiguration();

        this.RefreshSeconds = healthProbeConfiguration.RefreshSeconds;

        this.Listener = new TcpListener(IPAddress.Any, healthProbeConfiguration.Port);
    }

    internal HealthCheckService CheckService { get; }
    internal TcpListener Listener { get; }
    internal ILogger<TcpHealthProbeService> Logger { get; }
    internal int RefreshSeconds { get; }

    /// <summary>
    ///     Executes the background service.
    /// </summary>
    /// <param name="stoppingToken">The stopping token.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.Logger.TcpHealthProbeServiceInformation("Started.");
        this.Listener.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            await this.UpdateHeartbeatAsync(stoppingToken).ConfigureAwait(false);
            Thread.Sleep(TimeSpan.FromSeconds(this.RefreshSeconds));
        }
    }

    internal async Task<bool> CheckHealthAndDetermineStatus(CancellationToken token)
    {
        HealthReport result = await this.CheckService.CheckHealthAsync(token).ConfigureAwait(false);
        return result.Status == HealthStatus.Healthy;
    }

    internal async Task HandleTcpClientConnection(CancellationToken token)
    {
        TcpClient client = await this.Listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
        client.Close();
        this.Logger.TcpHealthProbeServiceInformation("Successfully process health check request.");
    }

    internal Task StopListenerAndLogHealthIssue()
    {
        this.Listener.Stop();
        this.Logger.TcpHealthProbeServiceInformation("Service is unhealthy. Listener stopped.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Updates the heartbeat.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    internal async Task UpdateHeartbeatAsync(CancellationToken token)
    {
        try
        {
            bool isHealthy = await this.CheckHealthAndDetermineStatus(token);

            await isHealthy.MatchAsync(this.ProcessPendingTcpConnections(token),
                this.StopListenerAndLogHealthIssue).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this.Logger.TcpHealthProbeServiceCritical(e);
        }
    }

    private Func<Task> ProcessPendingTcpConnections(CancellationToken token)
    {
        return ProcessPendingTcpConnectionsImplementation;

        async Task ProcessPendingTcpConnectionsImplementation()
        {
            this.Listener.Start();

            while (this.Listener.Server.IsBound && this.Listener.Pending())
            {
                await this.HandleTcpClientConnection(token);
            }

            this.Logger.TcpHealthProbeServiceDebug("Heartbeat executed.");
        }
    }
}