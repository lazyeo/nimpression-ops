using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Infrastructure.Persistence.Configurations;

namespace Nimpression.Infrastructure.Persistence;

/// <summary>
/// 核心数据库上下文，统一管理领域实体映射、事务与领域事件发件箱（Outbox）拦截落库。
/// </summary>
public class AppDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleAssignment> VehicleAssignments => Set<VehicleAssignment>();
    public DbSet<OdometerReading> OdometerReadings => Set<OdometerReading>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<AreaAssignment> AreaAssignments => Set<AreaAssignment>();
    public DbSet<JobTask> JobTasks => Set<JobTask>();
    public DbSet<Fine> Fines => Set<Fine>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<ShiftEntry> ShiftEntries => Set<ShiftEntry>();
    public DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<PayslipLine> PayslipLines => Set<PayslipLine>();
    public DbSet<NewsPost> NewsPosts => Set<NewsPost>();
    public DbSet<NewsReadReceipt> NewsReadReceipts => Set<NewsReadReceipt>();
    public DbSet<PartnerContact> PartnerContacts => Set<PartnerContact>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<DataSubjectRequest> DataSubjectRequests => Set<DataSubjectRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Nimpression.Infrastructure.Idempotency.IdempotencyRecord> IdempotencyRecords => Set<Nimpression.Infrastructure.Idempotency.IdempotencyRecord>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetUtcConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableDateTimeOffsetUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessDomainEventsIntoOutbox();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ProcessDomainEventsIntoOutbox();
        return base.SaveChanges();
    }

    private void ProcessDomainEventsIntoOutbox()
    {
        var aggregateRoots = ChangeTracker.Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        if (aggregateRoots.Count == 0)
        {
            return;
        }

        var domainEvents = aggregateRoots
            .SelectMany(root => root.DomainEvents)
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType();
            var payload = JsonSerializer.Serialize(domainEvent, eventType, JsonOptions);
            var outboxMessage = new OutboxMessage(
                Guid.NewGuid(),
                eventType.Name,
                payload,
                domainEvent.OccurredAt);

            OutboxMessages.Add(outboxMessage);
        }

        foreach (var root in aggregateRoots)
        {
            root.ClearDomainEvents();
        }
    }
}
