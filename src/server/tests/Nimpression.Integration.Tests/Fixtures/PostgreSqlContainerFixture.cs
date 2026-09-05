using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Integration.Tests.Notifications.Fixtures;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nimpression.Integration.Tests.Fixtures;

public class PostgreSqlContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("nimpression_test")
        .WithUsername("nimpression")
        .WithPassword("dev-only-insecure-test-db-password")
        .WithCleanUp(true)
        .Build();

    public IContainer MailpitContainer { get; } = new ContainerBuilder("axllent/mailpit:latest")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithEnvironment("MP_MAX_MESSAGES", "5000")
        .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
        .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "1")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025)))
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public string MailpitSmtpHost => MailpitContainer.Hostname;
    public int MailpitSmtpPort => MailpitContainer.GetMappedPublicPort(1025);
    public string MailpitApiUrl => $"http://{MailpitContainer.Hostname}:{MailpitContainer.GetMappedPublicPort(8025)}";

    public MailpitTestClient CreateMailpitClient() => new(MailpitApiUrl);

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENCRYPTION_KEY")))
        {
            Environment.SetEnvironmentVariable("ENCRYPTION_KEY", "ZGV2LW9ubHktaW5zZWN1cmUtYWVzLWtleS0zMmJ5dGU=");
        }
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Jwt__Secret")) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Jwt:Secret")))
        {
            Environment.SetEnvironmentVariable("Jwt__Secret", "dev-only-insecure-jwt-secret-never-use-in-production-0000");
        }
        await Task.WhenAll(Container.StartAsync(), MailpitContainer.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);
        Environment.SetEnvironmentVariable("Email__Host", MailpitSmtpHost);
        Environment.SetEnvironmentVariable("Email__Port", MailpitSmtpPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(Container.DisposeAsync().AsTask(), MailpitContainer.DisposeAsync().AsTask());
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("Email__Host", null);
        Environment.SetEnvironmentVariable("Email__Port", null);
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}

#pragma warning disable CA1711
[CollectionDefinition("PostgreSqlCollection")]
public class PostgreSqlTestCollectionDefinition : ICollectionFixture<PostgreSqlContainerFixture>
{
}
#pragma warning restore CA1711
