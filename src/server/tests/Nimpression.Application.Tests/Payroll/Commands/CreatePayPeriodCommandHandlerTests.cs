using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.Commands.CreatePayPeriod;
using Nimpression.Application.Tests.Payroll.TestDoubles;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Xunit;

namespace Nimpression.Application.Tests.Payroll.Commands;

public sealed class CreatePayPeriodCommandHandlerTests
{
    private readonly FakePayrollRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditSink _auditSink = new();
    private readonly FakeCurrentUser _currentUser = new(role: UserRole.Admin);

    [Fact]
    public async Task CreatePayPeriod_Success_CreatesFortnightlyPeriod()
    {
        var handler = new CreatePayPeriodCommandHandler(_repository, _unitOfWork, _currentUser, _auditSink);
        var startsOn = new DateOnly(2026, 9, 7); // Monday

        var command = new CreatePayPeriodCommand(startsOn);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(startsOn, result.Value.StartsOn);
        Assert.Equal(startsOn.AddDays(13), result.Value.EndsOn);
        Assert.Equal(PayPeriodStatus.Open, result.Value.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Single(_auditSink.RecordedAudits);
        Assert.Equal("CreatePayPeriod", _auditSink.RecordedAudits[0].Action);
    }

    [Fact]
    public async Task F7_7_CreatePayPeriod_OverlappingPeriod_ReturnsConflict()
    {
        var existingStart = new DateOnly(2026, 9, 7);
        var existingEnd = new DateOnly(2026, 9, 20);
        var existingPeriod = new PayPeriod(Guid.NewGuid(), existingStart, existingEnd);
        _repository.PayPeriods[existingPeriod.Id] = existingPeriod;

        var handler = new CreatePayPeriodCommandHandler(_repository, _unitOfWork, _currentUser, _auditSink);

        // Overlapping candidate period: 2026-09-14 to 2026-09-27
        var overlappingCommand = new CreatePayPeriodCommand(new DateOnly(2026, 9, 14));
        var result = await handler.Handle(overlappingCommand, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal("pay_period_overlap", result.Error.Code);
    }

    [Fact]
    public async Task CreatePayPeriod_ForbiddenForDrivers()
    {
        _currentUser.Role = UserRole.Driver;
        var handler = new CreatePayPeriodCommandHandler(_repository, _unitOfWork, _currentUser, _auditSink);

        var command = new CreatePayPeriodCommand(new DateOnly(2026, 9, 7));
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
    }
}
