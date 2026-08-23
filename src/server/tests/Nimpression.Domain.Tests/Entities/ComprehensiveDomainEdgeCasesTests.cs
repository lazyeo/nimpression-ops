using System.Reflection;
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
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class ComprehensiveDomainEdgeCasesTests
{
    private static T CreatePrivateInstance<T>() where T : class
    {
        var ctor = typeof(T).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        Assert.NotNull(ctor);
        return (T)ctor.Invoke(null);
    }

    [Fact]
    public void Private_parameterless_constructors_instantiate_for_ef_core()
    {
        Assert.NotNull(CreatePrivateInstance<User>());
        Assert.NotNull(CreatePrivateInstance<RefreshToken>());
        Assert.NotNull(CreatePrivateInstance<Driver>());
        Assert.NotNull(CreatePrivateInstance<Vehicle>());
        Assert.NotNull(CreatePrivateInstance<VehicleAssignment>());
        Assert.NotNull(CreatePrivateInstance<OdometerReading>());
        Assert.NotNull(CreatePrivateInstance<Area>());
        Assert.NotNull(CreatePrivateInstance<AreaAssignment>());
        Assert.NotNull(CreatePrivateInstance<JobTask>());
        Assert.NotNull(CreatePrivateInstance<ShiftEntry>());
        Assert.NotNull(CreatePrivateInstance<PayPeriod>());
        Assert.NotNull(CreatePrivateInstance<Payslip>());
        Assert.NotNull(CreatePrivateInstance<PayslipLine>());
        Assert.NotNull(CreatePrivateInstance<Fine>());
        Assert.NotNull(CreatePrivateInstance<IncidentReport>());
        Assert.NotNull(CreatePrivateInstance<NewsPost>());
        Assert.NotNull(CreatePrivateInstance<NewsReadReceipt>());
        Assert.NotNull(CreatePrivateInstance<PartnerContact>());
        Assert.NotNull(CreatePrivateInstance<EmailTemplate>());
        Assert.NotNull(CreatePrivateInstance<EmailLog>());
        Assert.NotNull(CreatePrivateInstance<AuditEvent>());
        Assert.NotNull(CreatePrivateInstance<DataSubjectRequest>());
        Assert.NotNull(CreatePrivateInstance<OutboxMessage>());
        Assert.NotNull(CreatePrivateInstance<DomainAssemblyMarker>());
    }

    [Fact]
    public void EmailTemplate_and_PartnerContact_comprehensive_guards_and_updates()
    {
        var template = new EmailTemplate(
            Guid.NewGuid(),
            "TEST_KEY",
            "Subject En",
            "Subject Zh",
            "Body En",
            "Body Zh");

        Assert.Equal("TEST_KEY", template.Key);
        Assert.Equal("Subject En", template.SubjectEn);
        Assert.Equal("Subject Zh", template.SubjectZh);
        Assert.Equal("Body En", template.BodyEn);
        Assert.Equal("Body Zh", template.BodyZh);

        template.UpdateContent("New Subj En", "New Subj Zh", "New Body En", "New Body Zh");
        Assert.Equal("New Subj En", template.SubjectEn);
        Assert.Equal("New Subj Zh", template.SubjectZh);

        template.Deactivate();
        Assert.False(template.Active);
        template.Activate();
        Assert.True(template.Active);

        // Validation guards
        Assert.Throws<DomainValidationException>(() => new EmailTemplate(Guid.NewGuid(), "", "Sub", "Sub", "B", "B"));
        Assert.Throws<DomainValidationException>(() => new EmailTemplate(Guid.NewGuid(), "K", "", "", "B", "B"));
        Assert.Throws<DomainValidationException>(() => new EmailTemplate(Guid.NewGuid(), "K", "S", "S", "", ""));
        Assert.Throws<DomainValidationException>(() => template.UpdateContent("", "", "B", "B"));
        Assert.Throws<DomainValidationException>(() => template.UpdateContent("S", "S", "", ""));

        var partner = new PartnerContact(Guid.NewGuid(), PartnerKind.Maintenance, "Mechanic Ltd", new EmailAddress("m@mech.co.nz"));
        Assert.Equal(PartnerKind.Maintenance, partner.Kind);
        Assert.Equal("Mechanic Ltd", partner.CompanyName);
        Assert.Equal("m@mech.co.nz", partner.Email.Value);

        partner.UpdateDetails(PartnerKind.Inspection, "VTNZ", new EmailAddress("info@vtnz.co.nz"));
        Assert.Equal(PartnerKind.Inspection, partner.Kind);
        Assert.Equal("VTNZ", partner.CompanyName);
        Assert.Equal("info@vtnz.co.nz", partner.Email.Value);

        Assert.Throws<DomainValidationException>(() => new PartnerContact(Guid.NewGuid(), PartnerKind.Insurer, "  ", new EmailAddress("a@a.co.nz")));
        Assert.Throws<DomainValidationException>(() => partner.UpdateDetails(PartnerKind.Insurer, "", new EmailAddress("a@a.co.nz")));
    }

    [Fact]
    public void NewsPost_and_NewsReadReceipt_comprehensive_guards()
    {
        var post = new NewsPost(Guid.NewGuid(), Guid.NewGuid(), "Title", "Body En", "", NewsAudience.All, DateTimeOffset.UtcNow);
        Assert.Equal("Title", post.Title);
        Assert.Equal("Body En", post.BodyEn);
        Assert.Equal("", post.BodyZh);

        Assert.Throws<DomainValidationException>(() => new NewsPost(Guid.NewGuid(), Guid.NewGuid(), "Title", "", "", NewsAudience.All, DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => post.UpdateContent("", "En", "Zh", NewsAudience.All));
        Assert.Throws<DomainValidationException>(() => post.UpdateContent("T", "", "", NewsAudience.All));

        Assert.Throws<DomainValidationException>(() => new NewsReadReceipt(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => new NewsReadReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AuditEvent_DSR_and_Outbox_comprehensive_guards()
    {
        var audit = new AuditEvent(
            Guid.NewGuid(),
            "Create",
            "User",
            "123",
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            UserRole.Admin,
            "before",
            "after",
            "192.168.1.1",
            "Agent");

        Assert.Equal("Create", audit.Action);
        Assert.Equal("User", audit.EntityType);
        Assert.Equal("123", audit.EntityId);
        Assert.NotNull(audit.ActorUserId);
        Assert.Equal(UserRole.Admin, audit.ActorRole);
        Assert.Equal("before", audit.BeforeJson);
        Assert.Equal("after", audit.AfterJson);
        Assert.Equal("192.168.1.1", audit.IpAddress);
        Assert.Equal("Agent", audit.UserAgent);

        Assert.Throws<DomainValidationException>(() => new AuditEvent(Guid.NewGuid(), "", "User", "1", DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => new AuditEvent(Guid.NewGuid(), "Act", "", "1", DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => new AuditEvent(Guid.NewGuid(), "Act", "User", "", DateTimeOffset.UtcNow));

        var dsr = new DataSubjectRequest(Guid.NewGuid(), Guid.NewGuid(), DataSubjectRequestKind.Rectification, DateTimeOffset.UtcNow);
        Assert.Equal(DataSubjectRequestKind.Rectification, dsr.Kind);
        Assert.Null(dsr.CompletedAt);
        Assert.Null(dsr.RejectionReason);

        Assert.Throws<DomainValidationException>(() => new DataSubjectRequest(Guid.NewGuid(), Guid.Empty, DataSubjectRequestKind.Export, DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => dsr.Reject("", DateTimeOffset.UtcNow));

        dsr.Complete("export.zip", DateTimeOffset.UtcNow);
        Assert.Throws<DomainValidationException>(() => dsr.Complete("again.zip", DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => dsr.Reject("reason", DateTimeOffset.UtcNow));

        var outbox = new OutboxMessage(Guid.NewGuid(), "TypeA", "{}", DateTimeOffset.UtcNow);
        Assert.Equal("TypeA", outbox.Type);
        Assert.Equal("{}", outbox.PayloadJson);
        Assert.Null(outbox.ProcessedAt);

        Assert.Throws<DomainValidationException>(() => new OutboxMessage(Guid.NewGuid(), "", "{}", DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => new OutboxMessage(Guid.NewGuid(), "T", "", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Vehicle_compliance_and_assignment_properties()
    {
        var wof = new DateOnly(2027, 1, 1);
        var cof = new DateOnly(2027, 2, 1);
        var ins = new DateOnly(2027, 3, 1);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("NZ999"),
            "Hino",
            "300",
            2023,
            "ENC_VIN",
            new Kilometres(50000m),
            new Kilometres(10000m),
            new Kilometres(45000m),
            wof,
            cof,
            ins,
            VehicleStatus.Active);

        Assert.Equal(wof, vehicle.WofExpiry);
        Assert.Equal(cof, vehicle.CofExpiry);
        Assert.Equal(ins, vehicle.InsuranceExpiry);
        Assert.Equal("ENC_VIN", vehicle.VinEnc);

        vehicle.UpdateComplianceDates(null, null, null);
        Assert.Null(vehicle.WofExpiry);
        Assert.Null(vehicle.CofExpiry);
        Assert.Null(vehicle.InsuranceExpiry);

        vehicle.SetStatus(VehicleStatus.Maintenance);
        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status);

        Assert.Throws<DomainValidationException>(() => new VehicleAssignment(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => new VehicleAssignment(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow, Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => new VehicleAssignment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.Empty));

        Assert.Throws<DomainValidationException>(() => new OdometerReading(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), new Kilometres(100m), null, DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => new OdometerReading(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, new Kilometres(100m), null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Fine_guards_and_properties()
    {
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        Assert.Throws<DomainValidationException>(() => new Fine(Guid.NewGuid(), Guid.Empty, vehicleId, new DateOnly(2026, 1, 1), "Auth", "Ref", new Money(50m), "Reason"));
        Assert.Throws<DomainValidationException>(() => new Fine(Guid.NewGuid(), driverId, Guid.Empty, new DateOnly(2026, 1, 1), "Auth", "Ref", new Money(50m), "Reason"));
        Assert.Throws<DomainValidationException>(() => new Fine(Guid.NewGuid(), driverId, vehicleId, new DateOnly(2026, 1, 1), "", "Ref", new Money(50m), "Reason"));
        Assert.Throws<DomainValidationException>(() => new Fine(Guid.NewGuid(), driverId, vehicleId, new DateOnly(2026, 1, 1), "Auth", "", new Money(50m), "Reason"));
        Assert.Throws<DomainValidationException>(() => new Fine(Guid.NewGuid(), driverId, vehicleId, new DateOnly(2026, 1, 1), "Auth", "Ref", new Money(50m), ""));

        var fine = new Fine(Guid.NewGuid(), driverId, vehicleId, new DateOnly(2026, 1, 1), "Auth", "Ref", new Money(50m), "Reason");
        Assert.Throws<DomainValidationException>(() => fine.StartReview(Guid.Empty, DateTimeOffset.UtcNow));

        fine.StartReview(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Throws<DomainValidationException>(() => fine.Accept(Guid.Empty, DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => fine.Dispute(Guid.Empty, DateTimeOffset.UtcNow, "Note"));
        Assert.Throws<DomainValidationException>(() => fine.Dispute(Guid.NewGuid(), DateTimeOffset.UtcNow, ""));
        Assert.Throws<DomainValidationException>(() => fine.Waive(Guid.Empty, DateTimeOffset.UtcNow, "Note"));
        Assert.Throws<DomainValidationException>(() => fine.Waive(Guid.NewGuid(), DateTimeOffset.UtcNow, ""));
    }

    [Fact]
    public void JobTask_guards_and_properties()
    {
        Assert.Throws<DomainValidationException>(() => new JobTask(Guid.NewGuid(), "", "Title", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => new JobTask(Guid.NewGuid(), "REF", "", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => new JobTask(Guid.NewGuid(), "REF", "Title", Guid.Empty, DateTimeOffset.UtcNow, Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => new JobTask(Guid.NewGuid(), "REF", "Title", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.Empty));

        var task = new JobTask(Guid.NewGuid(), "REF", "Title", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), description: "Desc", priority: TaskPriority.High);
        Assert.Equal("Desc", task.Description);
        Assert.Equal(TaskPriority.High, task.Priority);

        Assert.Throws<DomainValidationException>(() => task.Assign(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => task.Assign(Guid.NewGuid(), Guid.Empty));
        Assert.Throws<DomainValidationException>(() => task.Cancel("", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Exception_constructors_and_NzTimeZone_helper()
    {
        var exDefault = new DomainValidationException();
        Assert.NotNull(exDefault);

        var inner = new InvalidOperationException("inner");
        var exInner = new DomainValidationException("msg", inner);
        Assert.Same(inner, exInner.InnerException);

        var nzTime = NzTimeZone.Info;
        Assert.NotNull(nzTime);
        var now = DateTimeOffset.UtcNow;
        var nzDto = NzTimeZone.ToNzDateTimeOffset(now);
        var nzDate = NzTimeZone.ToNzDateOnly(now);
        Assert.Equal(nzDto.Year, nzDate.Year);
        Assert.Equal(nzDto.Month, nzDate.Month);
        Assert.Equal(nzDto.Day, nzDate.Day);
    }
}
