namespace RabbitMqHealthCheck.Tests;

using AwesomeAssertions;

using Innago.Shared.HealthChecks.RabbitMq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using RabbitMQ.Client;

using Xunit.OpenCategories;

[Category("RabbitMqHealthCheck")]
public class RabbitMqHealthCheckTests
{
    #region Options defaults

    [Fact]
    public void Options_Defaults_AreCorrect()
    {
        var options = new RabbitMqHealthCheckOptions();

        options.VaultSecretsPath.Should().Be("/vault/secrets/appsettings.json");
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.Tags.Should().BeEquivalentTo(new[] { "ready" });
        options.Name.Should().Be("rabbitmq");
        options.IgnoreDiConnection.Should().BeFalse();
    }

    #endregion

    #region Registration

    [Fact]
    public void Registration_AddsNamedHealthCheck_WithDefaultName()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHealthChecks().AddInnagoRabbitMq();

        using var sp = services.BuildServiceProvider();
        var hcOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        hcOptions.Registrations.Should().ContainSingle(r => r.Name == "rabbitmq");
    }

    [Fact]
    public void Registration_WithTags_UsesProvidedTags()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHealthChecks().AddInnagoRabbitMq(opts => opts.Tags = ["live", "custom"]);

        using var sp = services.BuildServiceProvider();
        var hcOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var reg = hcOptions.Registrations.Single(r => r.Name == "rabbitmq");

        reg.Tags.Should().BeEquivalentTo(new[] { "live", "custom" });
    }

    [Fact]
    public void Registration_WithOptions_UsesOptionsName()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHealthChecks().AddInnagoRabbitMq(opts => opts.Name = "my-rabbit");

        using var sp = services.BuildServiceProvider();
        var hcOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        hcOptions.Registrations.Should().ContainSingle(r => r.Name == "my-rabbit");
    }

    #endregion

    #region Strategy 1 — DI IConnection

    [Fact]
    public async Task Strategy1_ConnectionIsOpen_ReturnsHealthy()
    {
        var mockConnection = new Mock<IConnection>();
        mockConnection.Setup(c => c.IsOpen).Returns(true);

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(s => s.GetService(typeof(IConnection))).Returns(mockConnection.Object);

        var check = MakeHealthCheck(sp.Object);

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Strategy1_ConnectionIsNotOpen_ReturnsUnhealthy()
    {
        var mockConnection = new Mock<IConnection>();
        mockConnection.Setup(c => c.IsOpen).Returns(false);

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(s => s.GetService(typeof(IConnection))).Returns(mockConnection.Object);

        var check = MakeHealthCheck(sp.Object);

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not open");
    }

    [Fact]
    public async Task Strategy1_NoConnectionRegistered_FallsThrough()
    {
        // No IConnection in DI, no vault file, no config → should reach "no strategy" message
        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(s => s.GetService(typeof(IConnection))).Returns((object?)null);

        var check = MakeHealthCheck(sp.Object);

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("No RabbitMQ connection or configuration found");
    }

    [Fact]
    public async Task Strategy1_IgnoreDiConnection_SkipsStrategy1()
    {
        // Even with IConnection registered, should skip Strategy 1 when IgnoreDiConnection = true
        var mockConnection = new Mock<IConnection>(MockBehavior.Strict);
        mockConnection.Setup(c => c.IsOpen).Returns(true);

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        // Should NOT be called, so strict mock will throw if it is
        // But we need to allow it not to be called, so don't set it up

        var options = new RabbitMqHealthCheckOptions { IgnoreDiConnection = true };
        var check = MakeHealthCheck(sp.Object, options: options);

        var result = await check.CheckHealthAsync(MakeContext());

        // No DI, no vault, no config → unhealthy with "no strategy" message
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("No RabbitMQ connection or configuration found");
    }

    #endregion

    #region Strategy 2 — Vault File

    [Fact]
    public async Task Strategy2_VaultFileAbsent_FallsThroughToStrategy3()
    {
        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = "fallback-host",
        });

        bool proberCalled = false;
        var options = new RabbitMqHealthCheckOptions
        {
            VaultSecretsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.json"),
        };

        var check = MakeHealthCheck(sp, config, options, prober: (factory, _) =>
        {
            proberCalled = true;
            factory.HostName.Should().Be("fallback-host");
            return Task.FromResult(HealthCheckResult.Healthy());
        });

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        proberCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentWithAllKeys_CallsProber()
    {
        var vaultPath = WriteTempVaultFile(new Dictionary<string, string>
        {
            ["rabbit_host"] = "vault-host",
            ["rabbit_username"] = "vault-user",
            ["rabbit_password"] = "vault-pass",
            ["rabbit_virtualhost"] = "/vhost",
            ["rabbit_port"] = "5672",
        });

        var sp = MakeNoConnectionServiceProvider();
        var options = new RabbitMqHealthCheckOptions { VaultSecretsPath = vaultPath };
        ConnectionFactory? capturedFactory = null;

        var check = MakeHealthCheck(sp, options: options, prober: (factory, _) =>
        {
            capturedFactory = factory;
            return Task.FromResult(HealthCheckResult.Healthy());
        });

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        capturedFactory.Should().NotBeNull();
        capturedFactory!.HostName.Should().Be("vault-host");
        capturedFactory.UserName.Should().Be("vault-user");
        capturedFactory.Password.Should().Be("vault-pass");
        capturedFactory.VirtualHost.Should().Be("/vhost");
        capturedFactory.Port.Should().Be(5672);
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentHostAbsent_ReturnsUnhealthy()
    {
        var vaultPath = WriteTempVaultFile(new Dictionary<string, string>
        {
            ["rabbit_username"] = "user",
            ["rabbit_password"] = "pass",
        });

        var sp = MakeNoConnectionServiceProvider();
        var options = new RabbitMqHealthCheckOptions { VaultSecretsPath = vaultPath };
        var check = MakeHealthCheck(sp, options: options);

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("rabbit_host");
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentUsernameAbsent_ReturnsUnhealthy()
    {
        var vaultPath = WriteTempVaultFile(new Dictionary<string, string>
        {
            ["rabbit_host"] = "host",
            ["rabbit_password"] = "pass",
        });

        var sp = MakeNoConnectionServiceProvider();
        var options = new RabbitMqHealthCheckOptions { VaultSecretsPath = vaultPath };
        var check = MakeHealthCheck(sp, options: options);

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("rabbit_username");
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentPasswordAbsent_ReturnsUnhealthy()
    {
        var vaultPath = WriteTempVaultFile(new Dictionary<string, string>
        {
            ["rabbit_host"] = "host",
            ["rabbit_username"] = "user",
        });

        var sp = MakeNoConnectionServiceProvider();
        var options = new RabbitMqHealthCheckOptions { VaultSecretsPath = vaultPath };
        var check = MakeHealthCheck(sp, options: options);

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("rabbit_password");
    }

    [Fact]
    public async Task Strategy2_VaultFileMalformed_ReturnsUnhealthy_DoesNotFallThrough()
    {
        var vaultPath = Path.GetTempFileName();
        File.WriteAllText(vaultPath, "not valid json {{{");

        var sp = MakeNoConnectionServiceProvider();
        // Add config that would succeed in Strategy 3 — should NOT be reached
        var config = MakeConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = "should-not-reach",
        });

        bool proberCalled = false;
        var options = new RabbitMqHealthCheckOptions { VaultSecretsPath = vaultPath };
        var check = MakeHealthCheck(sp, config, options, prober: (_, _) =>
        {
            proberCalled = true;
            return Task.FromResult(HealthCheckResult.Healthy());
        });

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Vault file could not be parsed");
        proberCalled.Should().BeFalse();
    }

    #endregion

    #region Strategy 3 — IConfiguration

    [Fact]
    public async Task Strategy3_RabbitMQHostConfigured_CallsProber()
    {
        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = "config-host",
            ["RabbitMQ:Username"] = "config-user",
            ["RabbitMQ:Password"] = "config-pass",
        });

        ConnectionFactory? capturedFactory = null;
        var check = MakeHealthCheck(sp, config, prober: (factory, _) =>
        {
            capturedFactory = factory;
            return Task.FromResult(HealthCheckResult.Healthy());
        });

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        capturedFactory!.HostName.Should().Be("config-host");
        capturedFactory.UserName.Should().Be("config-user");
        capturedFactory.Password.Should().Be("config-pass");
    }

    [Fact]
    public async Task Strategy3_MassTransitHostConfigured_CallsProber()
    {
        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>
        {
            ["MassTransit:RabbitMq:Host"] = "mt-host",
        });

        ConnectionFactory? capturedFactory = null;
        var check = MakeHealthCheck(sp, config, prober: (factory, _) =>
        {
            capturedFactory = factory;
            return Task.FromResult(HealthCheckResult.Healthy());
        });

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        capturedFactory!.HostName.Should().Be("mt-host");
    }

    [Fact]
    public async Task Strategy3_RabbitHostConfigured_CallsProber()
    {
        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>
        {
            ["rabbit_host"] = "flat-host",
        });

        ConnectionFactory? capturedFactory = null;
        var check = MakeHealthCheck(sp, config, prober: (factory, _) =>
        {
            capturedFactory = factory;
            return Task.FromResult(HealthCheckResult.Healthy());
        });

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        capturedFactory!.HostName.Should().Be("flat-host");
    }

    [Fact]
    public async Task Strategy3_NoHostConfigured_ReturnsUnhealthy()
    {
        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>());

        var check = MakeHealthCheck(sp, config);

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("No RabbitMQ connection or configuration found");
    }

    [Fact]
    public async Task Strategy3_UsesGuestDefaults_WhenCredentialsAbsent()
    {
        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = "some-host",
        });

        ConnectionFactory? capturedFactory = null;
        var check = MakeHealthCheck(sp, config, prober: (factory, _) =>
        {
            capturedFactory = factory;
            return Task.FromResult(HealthCheckResult.Healthy());
        });

        await check.CheckHealthAsync(MakeContext());

        capturedFactory!.UserName.Should().Be("guest");
        capturedFactory.Password.Should().Be("guest");
    }

    #endregion

    #region Strategy ordering

    [Fact]
    public async Task StrategyOrder_VaultFilePresentBlocksStrategy3()
    {
        // Vault file present with all keys → prober fails → should NOT fall through to Strategy 3
        var vaultPath = WriteTempVaultFile(new Dictionary<string, string>
        {
            ["rabbit_host"] = "vault-host",
            ["rabbit_username"] = "vault-user",
            ["rabbit_password"] = "vault-pass",
        });

        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = "config-host",
        });

        var options = new RabbitMqHealthCheckOptions { VaultSecretsPath = vaultPath };
        var check = MakeHealthCheck(sp, config, options, prober: (factory, _) =>
        {
            // Vault prober returns unhealthy
            factory.HostName.Should().Be("vault-host");
            return Task.FromResult(HealthCheckResult.Unhealthy("vault probe failed"));
        });

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("vault probe failed");
    }

    [Fact]
    public async Task StrategyOrder_DITakesPrecedenceOverVault()
    {
        // DI connection is open AND vault file exists → should return healthy from DI, not touch vault
        var vaultPath = WriteTempVaultFile(new Dictionary<string, string>
        {
            ["rabbit_host"] = "vault-host",
            ["rabbit_username"] = "vault-user",
            ["rabbit_password"] = "vault-pass",
        });

        var mockConnection = new Mock<IConnection>();
        mockConnection.Setup(c => c.IsOpen).Returns(true);

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(s => s.GetService(typeof(IConnection))).Returns(mockConnection.Object);

        bool proberCalled = false;
        var options = new RabbitMqHealthCheckOptions { VaultSecretsPath = vaultPath };
        var check = MakeHealthCheck(sp.Object, options: options, prober: (_, _) =>
        {
            proberCalled = true;
            return Task.FromResult(HealthCheckResult.Healthy());
        });

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        proberCalled.Should().BeFalse();
    }

    #endregion

    #region No strategy / prober exception

    [Fact]
    public async Task NoStrategy_ReturnsUnhealthyWithDescriptiveMessage()
    {
        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>());

        var check = MakeHealthCheck(sp, config);

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("No RabbitMQ connection or configuration found.");
    }

    [Fact]
    public async Task Prober_ExceptionIsCaught_ReturnsUnhealthySanitizedMessage()
    {
        var sp = MakeNoConnectionServiceProvider();
        var config = MakeConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = "some-host",
        });

        var check = MakeHealthCheck(sp, config, prober: (_, _) =>
            throw new InvalidOperationException("secret connection string info"));

        var result = await check.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        // Should NOT contain the raw exception message
        result.Description.Should().NotContain("secret connection string info");
        result.Description.Should().Contain("See application logs");
        // Should NOT have the exception attached
        result.Exception.Should().BeNull();
    }

    #endregion

    #region Helpers

    private static RabbitMqHealthCheck MakeHealthCheck(
        IServiceProvider serviceProvider,
        IConfiguration? configuration = null,
        RabbitMqHealthCheckOptions? options = null,
        Func<ConnectionFactory, CancellationToken, Task<HealthCheckResult>>? prober = null)
    {
        configuration ??= MakeConfiguration(new Dictionary<string, string?>());
        options ??= new RabbitMqHealthCheckOptions
        {
            // Default to a non-existent path so vault strategy falls through
            VaultSecretsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.json"),
        };

        return new RabbitMqHealthCheck(
            serviceProvider,
            configuration,
            options,
            Mock.Of<ILogger<RabbitMqHealthCheck>>(),
            prober);
    }

    private static IServiceProvider MakeNoConnectionServiceProvider()
    {
        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(s => s.GetService(typeof(IConnection))).Returns((object?)null);
        return sp.Object;
    }

    private static IConfiguration MakeConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static HealthCheckContext MakeContext()
    {
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "rabbitmq",
                Mock.Of<IHealthCheck>(),
                failureStatus: null,
                tags: null),
        };
    }

    private static string WriteTempVaultFile(Dictionary<string, string> secrets)
    {
        var path = Path.GetTempFileName();
        var json = System.Text.Json.JsonSerializer.Serialize(secrets);
        File.WriteAllText(path, json);
        return path;
    }

    #endregion
}
