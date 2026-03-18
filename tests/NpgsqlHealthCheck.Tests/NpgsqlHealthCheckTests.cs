namespace NpgsqlHealthCheck.Tests;

using System.Text.Json;

using AwesomeAssertions;

using Innago.Shared.HealthChecks.Npgsql;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.OpenCategories;

[Category("NpgsqlHealthCheck")]
public sealed class NpgsqlHealthCheckTests : IDisposable
{
    private readonly Mock<ILogger<NpgsqlHealthCheck>> loggerMock = new(MockBehavior.Loose);
    private readonly List<string> tempFiles = [];

    public void Dispose()
    {
        foreach (string f in this.tempFiles)
        {
            try
            {
                File.Delete(f);
            }
            catch
            {
                /* best effort */
            }
        }
    }

    private string WriteTempVaultFile(Dictionary<string, string> secrets)
    {
        string path = Path.GetTempFileName();
        this.tempFiles.Add(path);
        File.WriteAllText(path, JsonSerializer.Serialize(secrets));
        return path;
    }

    private string WriteTempVaultFile(string rawContent)
    {
        string path = Path.GetTempFileName();
        this.tempFiles.Add(path);
        File.WriteAllText(path, rawContent);
        return path;
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Func<string, CancellationToken, Task<HealthCheckResult>> HealthyProber() =>
        (_, _) => Task.FromResult(HealthCheckResult.Healthy("ok"));

    private static Func<string, CancellationToken, Task<HealthCheckResult>> CaptureProber(out Func<string?> getCaptured)
    {
        string? captured = null;
        getCaptured = () => captured;

        return (cs, _) =>
        {
            captured = cs;
            return Task.FromResult(HealthCheckResult.Healthy("ok"));
        };
    }

    // ── Options defaults ──────────────────────────────────────────────

    [Fact]
    public void Options_Defaults_AreCorrect()
    {
        var options = new NpgsqlHealthCheckOptions();

        options.VaultSecretsPath.Should().Be("/vault/secrets/appsettings.json");
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.Tags.Should().BeEquivalentTo("ready");
        options.Name.Should().Be("npgsql");
        options.CommandText.Should().Be("SELECT 1");
        options.ConnectionStringName.Should().BeNull();
    }

    // ── Registration ──────────────────────────────────────────────────

    [Fact]
    public void Registration_AddsNamedHealthCheck_WithDefaultName()
    {
        var services = new ServiceCollection();
        services.AddSingleton(BuildConfig(new Dictionary<string, string?>()));
        services.AddLogging();
        services.AddHealthChecks().AddInnagoNpgsql();

        using ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<HealthCheckRegistration> registrations = provider.GetServices<HealthCheckRegistration>();

        registrations.Should().ContainSingle(r => r.Name == "npgsql");
    }

    [Fact]
    public void Registration_WithTags_UsesProvidedTags()
    {
        var services = new ServiceCollection();
        services.AddSingleton(BuildConfig(new Dictionary<string, string?>()));
        services.AddLogging();
        services.AddHealthChecks().AddInnagoNpgsql(opts => opts.Tags = ["live", "ready"]);

        using ServiceProvider provider = services.BuildServiceProvider();

        HealthCheckRegistration registration = provider.GetServices<HealthCheckRegistration>()
            .Single(r => r.Name == "npgsql");

        registration.Tags.Should().BeEquivalentTo("live", "ready");
    }

    [Fact]
    public void Registration_WithOptions_UsesOptionsName()
    {
        var services = new ServiceCollection();
        services.AddSingleton(BuildConfig(new Dictionary<string, string?>()));
        services.AddLogging();
        services.AddHealthChecks().AddInnagoNpgsql(o => o.Name = "pg-custom");

        using ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<HealthCheckRegistration> registrations = provider.GetServices<HealthCheckRegistration>();

        registrations.Should().ContainSingle(r => r.Name == "pg-custom");
    }

    // ── Strategy 1: Connection string ─────────────────────────────────

    [Fact]
    public async Task Strategy1_DefaultConnectionConfigured_CallsProber()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/path" };
        Func<string, CancellationToken, Task<HealthCheckResult>> prober = CaptureProber(out Func<string?> getCaptured);

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, prober);
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
        getCaptured().Should().Be("Host=localhost;Database=test");
    }

    [Fact]
    public async Task Strategy1_DatabaseConnectionConfigured_CallsProber()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=db;Database=mydb",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/path" };
        Func<string, CancellationToken, Task<HealthCheckResult>> prober = CaptureProber(out Func<string?> getCaptured);

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, prober);
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
        getCaptured().Should().Be("Host=db;Database=mydb");
    }

    [Fact]
    public async Task Strategy1_ConnectionStringName_UsesNamedKey()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MyCustomDb"] = "Host=custom;Database=customdb",
        });

        var options = new NpgsqlHealthCheckOptions
        {
            ConnectionStringName = "MyCustomDb",
            VaultSecretsPath = "/nonexistent/path",
        };

        Func<string, CancellationToken, Task<HealthCheckResult>> prober = CaptureProber(out Func<string?> getCaptured);

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, prober);
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
        getCaptured().Should().Be("Host=custom;Database=customdb");
    }

    [Fact]
    public async Task Strategy1_ConnectionStringName_Null_TriesDefaultConnectionThenDatabase()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=fallback;Database=fallbackdb",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/path" };
        Func<string, CancellationToken, Task<HealthCheckResult>> prober = CaptureProber(out Func<string?> getCaptured);

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, prober);
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
        getCaptured().Should().Be("Host=fallback;Database=fallbackdb");
    }

    [Fact]
    public async Task Strategy1_NoConnectionString_FallsThrough()
    {
        string vaultPath = this.WriteTempVaultFile(new Dictionary<string, string>
        {
            ["db_host"] = "vaulthost",
            ["db_username"] = "user",
            ["db_password"] = "pass",
            ["db_name"] = "vaultdb",
        });

        IConfiguration config = BuildConfig(new Dictionary<string, string?>());
        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    // ── Strategy 2: Vault file ────────────────────────────────────────

    [Fact]
    public async Task Strategy2_VaultFileAbsent_FallsThroughToStrategy3()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["db_host"] = "confighost",
            ["db_username"] = "user",
            ["db_password"] = "pass",
            ["db_name"] = "configdb",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/vault/path.json" };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentWithAllKeys_CallsProber()
    {
        string vaultPath = this.WriteTempVaultFile(new Dictionary<string, string>
        {
            ["db_host"] = "pghost",
            ["db_username"] = "pguser",
            ["db_password"] = "pgpass",
            ["db_name"] = "pgdb",
            ["db_port"] = "5433",
        });

        IConfiguration config = BuildConfig(new Dictionary<string, string?>());
        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };
        Func<string, CancellationToken, Task<HealthCheckResult>> prober = CaptureProber(out Func<string?> getCaptured);

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, prober);
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
        string cs = getCaptured()!;
        cs.Should().Contain("Host=pghost");
        cs.Should().Contain("Username=pguser");
        cs.Should().Contain("Database=pgdb");
        cs.Should().Contain("Port=5433");
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentHostAbsent_ReturnsUnhealthy()
    {
        string vaultPath = this.WriteTempVaultFile(new Dictionary<string, string>
        {
            ["db_username"] = "user",
            ["db_password"] = "pass",
            ["db_name"] = "db",
        });

        IConfiguration config = BuildConfig(new Dictionary<string, string?>());
        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("db_host");
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentUsernameAbsent_ReturnsUnhealthy()
    {
        string vaultPath = this.WriteTempVaultFile(new Dictionary<string, string>
        {
            ["db_host"] = "host",
            ["db_password"] = "pass",
            ["db_name"] = "db",
        });

        IConfiguration config = BuildConfig(new Dictionary<string, string?>());
        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("db_username");
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentPasswordAbsent_ReturnsUnhealthy()
    {
        string vaultPath = this.WriteTempVaultFile(new Dictionary<string, string>
        {
            ["db_host"] = "host",
            ["db_username"] = "user",
            ["db_name"] = "db",
        });

        IConfiguration config = BuildConfig(new Dictionary<string, string?>());
        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("db_password");
    }

    [Fact]
    public async Task Strategy2_VaultFilePresentDbNameAbsent_ReturnsUnhealthy()
    {
        string vaultPath = this.WriteTempVaultFile(new Dictionary<string, string>
        {
            ["db_host"] = "host",
            ["db_username"] = "user",
            ["db_password"] = "pass",
        });

        IConfiguration config = BuildConfig(new Dictionary<string, string?>());
        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("db_name");
    }

    [Fact]
    public async Task Strategy2_VaultFileMalformed_ReturnsUnhealthy_DoesNotFallThrough()
    {
        string vaultPath = this.WriteTempVaultFile("not valid json {{{");

        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["db_host"] = "shouldnotbeused",
            ["db_username"] = "user",
            ["db_password"] = "pass",
            ["db_name"] = "db",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("could not be read or parsed");
    }

    // ── Strategy 3: IConfiguration keys ───────────────────────────────

    [Fact]
    public async Task Strategy3_DbHostConfigured_CallsProber()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["db_host"] = "s3host",
            ["db_username"] = "s3user",
            ["db_password"] = "s3pass",
            ["db_name"] = "s3db",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/path" };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Strategy3_DB_HOST_Configured_CallsProber()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["DB_HOST"] = "s3host",
            ["DB_USERNAME"] = "s3user",
            ["DB_PASSWORD"] = "s3pass",
            ["DB_NAME"] = "s3db",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/path" };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Strategy3_NoHostConfigured_ReturnsUnhealthy()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>());
        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/path" };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("no connection string, vault secrets, or configuration keys found");
    }

    // ── Strategy ordering ─────────────────────────────────────────────

    [Fact]
    public async Task StrategyOrder_VaultFilePresentBlocksStrategy3()
    {
        // Vault file present with missing keys → unhealthy, NOT fallthrough to Strategy 3
        string vaultPath = this.WriteTempVaultFile(new Dictionary<string, string>
        {
            ["db_host"] = "vaulthost",
            // Missing other required keys
        });

        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["db_host"] = "confighost",
            ["db_username"] = "user",
            ["db_password"] = "pass",
            ["db_name"] = "db",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("db_username");
    }

    [Fact]
    public async Task StrategyOrder_ConnectionStringTakesPrecedenceOverVault()
    {
        string vaultPath = this.WriteTempVaultFile(new Dictionary<string, string>
        {
            ["db_host"] = "vaulthost",
            ["db_username"] = "user",
            ["db_password"] = "pass",
            ["db_name"] = "vaultdb",
        });

        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=connstring;Database=conndb",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = vaultPath };
        Func<string, CancellationToken, Task<HealthCheckResult>> prober = CaptureProber(out Func<string?> getCaptured);

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, prober);
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Healthy);
        getCaptured().Should().Be("Host=connstring;Database=conndb");
    }

    // ── No strategy / error handling ──────────────────────────────────

    [Fact]
    public async Task NoStrategy_ReturnsUnhealthyWithDescriptiveMessage()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>());
        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/vault/path.json" };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, HealthyProber());
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("no connection string, vault secrets, or configuration keys found");
    }

    [Fact]
    public async Task Prober_ExceptionIsCaught_ReturnsUnhealthySanitizedMessage()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        });

        var options = new NpgsqlHealthCheckOptions { VaultSecretsPath = "/nonexistent/path" };

        var hc = new NpgsqlHealthCheck(config, options, this.loggerMock.Object, ThrowingProber);
        HealthCheckResult result = await hc.CheckHealthAsync(null);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("InvalidOperationException");
        result.Description.Should().NotContain("secret connection details here");
        
        // ReSharper disable once SeparateLocalFunctionsWithJumpStatement
        static Task<HealthCheckResult> ThrowingProber(string s, CancellationToken cancellationToken) => throw new InvalidOperationException("secret connection details here");
    }
}