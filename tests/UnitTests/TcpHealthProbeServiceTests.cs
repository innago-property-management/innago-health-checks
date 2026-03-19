namespace UnitTests;

using System.Net;

using AutoFixture;

using FluentAssertions;

using Innago.Shared.HealthChecks.TcpHealthProbe;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.OpenCategories;

[Category($"{nameof(TcpHealthProbeService)}")]
public class TcpHealthProbeServiceTests
{
    private static Fixture Faker { get; } = new();
    private readonly int expectedDefaultRefreshSeconds = new HealthProbeConfiguration().RefreshSeconds;
    private readonly int expectedDefaultPort = new HealthProbeConfiguration().Port;

    [Fact]
    public void ConfigurationShouldNotBeNeeded()
    {
        Func<TcpHealthProbeService> act = () => MakeTarget();

        act.Should().NotThrow();

        TcpHealthProbeService target = act();
        target.RefreshSeconds.Should().Be(this.expectedDefaultRefreshSeconds);
        (target.Listener.LocalEndpoint as IPEndPoint)!.Port.Should().Be(this.expectedDefaultPort);
    }

    [Fact]
    public void ConfigurationShouldNotBeUsed()
    {
        var port = Faker.Create<int>();
        var refresh = Faker.Create<int>();

        TcpHealthProbeService target = MakeTarget(configValues: new Dictionary<string, string?>
        {
            { $"{nameof(HealthProbeConfiguration)}:{nameof(HealthProbeConfiguration.Port)}", port.ToString() },
            { $"{nameof(HealthProbeConfiguration)}:{nameof(HealthProbeConfiguration.RefreshSeconds)}", refresh.ToString() },
        });

        target.RefreshSeconds.Should().Be(refresh);
        (target.Listener.LocalEndpoint as IPEndPoint)!.Port.Should().Be(port);
    }

    [Fact]
    public async Task StopListenerAndLogHealthIssueShouldStopListener()
    {
        TcpHealthProbeService target = MakeTarget();
        target.Listener.Start();
        await target.StopListenerAndLogHealthIssue();

        target.Listener.Server.IsBound.Should().BeFalse();
        
        target.Listener.Stop();
    }

    [Fact]
    public async Task StopListenerAndLogHealthIssueShouldLog()
    {
        using TcpHealthProbeService target = MakeTarget();
        await target.StopListenerAndLogHealthIssue();

        Mock.Get(target.Logger)
            .Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((obj, type) => obj.ToString() == "Service is unhealthy. Listener stopped."),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()));
    }

    [Theory]
    [InlineData(HealthStatus.Unhealthy)]
    [InlineData(HealthStatus.Degraded)]
    [InlineData(HealthStatus.Healthy)]
    public async Task CheckHealthAndDetermineStatusShouldReturnTrueIfHealthy(HealthStatus healthStatus)
    {
        TcpHealthProbeService target = MakeTarget(healthStatus);

        (await target.CheckHealthAndDetermineStatus(CancellationToken.None)).Should().Be(healthStatus == HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(HealthStatus.Unhealthy)]
    [InlineData(HealthStatus.Degraded)]
    [InlineData(HealthStatus.Healthy)]
    public async Task UpdateHeartbeatAsyncShouldStopListenerIfUnhealthy(HealthStatus healthStatus)
    {
        TcpHealthProbeService target = MakeTarget(healthStatus);

        await target.UpdateHeartbeatAsync(CancellationToken.None);

        target.Listener.Server.IsBound.Should().Be(healthStatus == HealthStatus.Healthy);
        target.Listener.Stop();
    }

    private static TcpHealthProbeService MakeTarget(
        HealthStatus healthStatus = HealthStatus.Healthy,
        Dictionary<string, string?>? configValues = null)
    {
        return new Mock<TcpHealthProbeService>(
            MockBehavior.Loose,
            MakeHealthCheckService(healthStatus),
            MakeLogger(),
            MakeConfiguration(configValues))
        {
            CallBase = true,
        }.Object;
    }

    private static IConfiguration MakeConfiguration(Dictionary<string, string?>? configValues = null)
    {
        configValues ??= [];

        IConfigurationBuilder builder = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues);

        return builder.Build();
    }

    private static HealthCheckService MakeHealthCheckService(HealthStatus healthStatus = HealthStatus.Healthy)
    {
        var mock = new Mock<HealthCheckService>(MockBehavior.Strict);

        mock.Setup(service => service.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<HealthCheckRegistration, bool>? _, CancellationToken _) =>
            {
                Dictionary<string, HealthReportEntry> entries = [];

                return new HealthReport(entries.AsReadOnly(), healthStatus, TimeSpan.Zero);
            });

        return mock.Object;
    }

    private static ILogger<TcpHealthProbeService> MakeLogger()
    {
        var mock = new Mock<ILogger<TcpHealthProbeService>>(MockBehavior.Strict);

        mock.Setup(logger => logger.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

        mock.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        return mock.Object;
    }
}