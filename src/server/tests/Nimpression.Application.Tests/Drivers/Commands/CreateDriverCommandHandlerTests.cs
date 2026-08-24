using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.Commands.CreateDriver;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Commands;

public sealed class CreateDriverCommandHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private CreateDriverCommandHandler CreateSut()
    {
        _dateTimeProvider.NzToday.Returns(new DateOnly(2026, 8, 24));
        _dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero));
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed_pwd");

        return new CreateDriverCommandHandler(_driverRepository, _unitOfWork, _passwordHasher, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_with_valid_command_creates_driver_and_user_and_returns_id()
    {
        var sut = CreateSut();
        var command = new CreateDriverCommand
        {
            DisplayName = "John Doe",
            Email = "john.doe@nimpression.co.nz",
            EmployeeNo = "DRV-100",
            LicenceClass = "Class 4",
            LicenceExpiry = new DateOnly(2027, 8, 24),
            HourlyRateAmount = 35.00m,
            PerTripRateAmount = 45.00m,
            PerKmRateAmount = 0.85m,
            Phone = "+6421000001",
            Address = "10 Queen St",
            EmergencyContact = "+6421999999",
            HiredOn = new DateOnly(2026, 8, 24)
        };

        _driverRepository.ExistsByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _driverRepository.ExistsByEmployeeNoAsync("DRV-100", Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        await _driverRepository.Received(1).AddDriverAsync(
            Arg.Is<Driver>(d =>
                d.EmployeeNo == "DRV-100" &&
                d.LicenceClass == "Class 4" &&
                d.HourlyRate.Amount == 35.00m &&
                d.Status == DriverStatus.Active),
            Arg.Is<User>(u =>
                u.Email.Value == "john.doe@nimpression.co.nz" &&
                u.DisplayName == "John Doe" &&
                u.Role == UserRole.Driver),
            Arg.Any<IEnumerable<AreaAssignment>?>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_with_duplicate_email_returns_conflict_error()
    {
        var sut = CreateSut();
        var command = new CreateDriverCommand
        {
            DisplayName = "John Doe",
            Email = "existing@nimpression.co.nz",
            EmployeeNo = "DRV-101",
            LicenceClass = "Class 4",
            LicenceExpiry = new DateOnly(2027, 8, 24),
            Phone = "123",
            Address = "Street",
            EmergencyContact = "456"
        };

        _driverRepository.ExistsByEmailAsync(Arg.Is<EmailAddress>(e => e.Value == "existing@nimpression.co.nz"), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be("email_conflict");
    }

    [Fact]
    public async Task Handle_with_duplicate_employee_no_returns_conflict_error()
    {
        var sut = CreateSut();
        var command = new CreateDriverCommand
        {
            DisplayName = "John Doe",
            Email = "john.unique@nimpression.co.nz",
            EmployeeNo = "DRV-001",
            LicenceClass = "Class 4",
            LicenceExpiry = new DateOnly(2027, 8, 24),
            Phone = "123",
            Address = "Street",
            EmergencyContact = "456"
        };

        _driverRepository.ExistsByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _driverRepository.ExistsByEmployeeNoAsync("DRV-001", Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be("employee_no_conflict");
    }

    [Fact]
    public async Task Handle_with_area_ids_creates_initial_area_assignments()
    {
        var sut = CreateSut();
        var areaId1 = Guid.NewGuid();
        var areaId2 = Guid.NewGuid();

        var command = new CreateDriverCommand
        {
            DisplayName = "John Doe",
            Email = "john.areas@nimpression.co.nz",
            EmployeeNo = "DRV-102",
            LicenceClass = "Class 4",
            LicenceExpiry = new DateOnly(2027, 8, 24),
            Phone = "123",
            Address = "Street",
            EmergencyContact = "456",
            AreaIds = [areaId1, areaId2]
        };

        _driverRepository.ExistsByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>()).Returns(false);
        _driverRepository.ExistsByEmployeeNoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _driverRepository.Received(1).AddDriverAsync(
            Arg.Any<Driver>(),
            Arg.Any<User>(),
            Arg.Is<IEnumerable<AreaAssignment>>(assignments => assignments.Count() == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CreateDriverCommand_sets_correct_auditable_properties()
    {
        var command = new CreateDriverCommand
        {
            DisplayName = "John Doe",
            Email = "john@example.com",
            EmployeeNo = "DRV-999"
        };

        command.AuditEntityType.Should().Be("Driver");
        command.AuditAction.Should().Be("CreateDriver");
    }
}
