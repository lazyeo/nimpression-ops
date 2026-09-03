using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Common.Security;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Contracts;

/// <summary>
/// Minimal API 全局枚举序列化契约测试套件（W11 / R2）。
/// 严格断言返回 JSON 报文中的枚举字段是字符串类型（JsonValueKind.String）且精确等于枚举成员名称，
/// 杜绝数字枚举或静默降级。
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class EnumSerializationContractIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private ContractTestAppFactory _factory = null!;
    private HttpClient _client = null!;
    private IJwtTokenGenerator _tokenGenerator = null!;

    public EnumSerializationContractIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new ContractTestAppFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        _tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetVehicles_MustSerializeStatusEnumAsString_NotNumber()
    {
        // Arrange: Seed an admin user and vehicle with specific enum status
        await using var context = _fixture.CreateDbContext();
        var adminEmail = TestDataFactory.CreateEmailAddress("contract-admin");
        var adminUser = new User(
            Guid.NewGuid(),
            adminEmail,
            "PasswordHash",
            UserRole.Admin,
            "Admin User",
            "en-NZ",
            new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));
        await context.Users.AddAsync(adminUser);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("CV"),
            "Toyota",
            "Hilux",
            2023,
            "VIN-CONTRACT-001",
            new Kilometres(50000),
            new Kilometres(10000));
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        var (token, _) = _tokenGenerator.GenerateAccessToken(
            adminUser.Id,
            adminUser.Email.Value,
            UserRole.Admin.ToString(),
            adminUser.DisplayName);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/vehicles/{vehicle.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(rawJson);
        var root = jsonDoc.RootElement;

        root.TryGetProperty("status", out var statusProp).Should().BeTrue("JSON response must contain 'status' field");
        
        // Hard requirement: must be string, not number
        statusProp.ValueKind.Should().Be(JsonValueKind.String, "Enum must serialize as a string, not integer");
        statusProp.GetString().Should().Be("Active", "Expected enum member name 'Active'");
    }

    [Fact]
    public async Task GetVehiclesList_AllItemsMustSerializeStatusEnumAsString()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var adminEmail = TestDataFactory.CreateEmailAddress("contract-admin-list");
        var adminUser = new User(
            Guid.NewGuid(),
            adminEmail,
            "PasswordHash",
            UserRole.Admin,
            "Admin User",
            "en-NZ",
            new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));
        await context.Users.AddAsync(adminUser);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("LV"),
            "Isuzu",
            "NPR",
            2022,
            "VIN-CONTRACT-002",
            new Kilometres(80000),
            new Kilometres(10000));
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        var (token, _) = _tokenGenerator.GenerateAccessToken(
            adminUser.Id,
            adminUser.Email.Value,
            UserRole.Admin.ToString(),
            adminUser.DisplayName);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/vehicles?page=1&pageSize=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(rawJson);
        var root = jsonDoc.RootElement;

        root.TryGetProperty("items", out var itemsProp).Should().BeTrue();
        itemsProp.ValueKind.Should().Be(JsonValueKind.Array);

        var items = itemsProp.EnumerateArray().ToList();
        items.Should().NotBeEmpty();

        foreach (var item in items)
        {
            item.TryGetProperty("status", out var statusProp).Should().BeTrue();
            statusProp.ValueKind.Should().Be(JsonValueKind.String, "Every vehicle item's status must be a string");
            statusProp.GetString().Should().BeOneOf(
                nameof(VehicleStatus.Active),
                nameof(VehicleStatus.Maintenance),
                nameof(VehicleStatus.Inactive),
                nameof(VehicleStatus.Decommissioned));
        }
    }
}

internal sealed class ContractTestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ContractTestAppFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.ConfigureServices(services =>
        {
            var hostedServices = services.Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList();
            foreach (var hs in hostedServices)
            {
                services.Remove(hs);
            }
        });
    }
}
