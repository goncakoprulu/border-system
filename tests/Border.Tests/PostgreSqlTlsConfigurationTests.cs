using System.Net.Security;
using System.Security.Authentication;
using Border.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Border.Tests;

public sealed class PostgreSqlTlsConfigurationTests
{
    [Fact]
    public void ForceTls12_IsDisabledByDefault()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.False(PostgreSqlTlsConfiguration.IsEnabled(configuration));
    }

    [Fact]
    public void ForceTls12_ReadsEnvironmentStyleConfigurationKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ForceTls12"] = "true"
            })
            .Build();

        Assert.True(PostgreSqlTlsConfiguration.IsEnabled(configuration));
    }

    [Fact]
    public void Apply_RestrictsSslProtocolsToTls12()
    {
        var options = new SslClientAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls13
        };

        PostgreSqlTlsConfiguration.Apply(options);

        Assert.Equal(SslProtocols.Tls12, options.EnabledSslProtocols);
        Assert.Null(options.RemoteCertificateValidationCallback);
    }

    [Fact]
    public async Task DataSource_IsOwnedAsOneSingletonByTheServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=test;Database=test;Username=test;Password=test"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        var registration = Assert.Single(services, x => x.ServiceType == typeof(NpgsqlDataSource));
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);

        await using var serviceProvider = services.BuildServiceProvider();
        Assert.Same(
            serviceProvider.GetRequiredService<NpgsqlDataSource>(),
            serviceProvider.GetRequiredService<NpgsqlDataSource>());
    }
}
