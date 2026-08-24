using Nimpression.Application.Features.Vehicles.Commands.AssignVehicle;
using Nimpression.Application.Features.Vehicles.Commands.CreateVehicle;
using Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;
using Nimpression.Application.Features.Vehicles.Commands.RecordVehicleService;
using Nimpression.Application.Features.Vehicles.Commands.ReleaseVehicleAssignment;
using Nimpression.Application.Features.Vehicles.Commands.UpdateVehicle;
using Nimpression.Application.Features.Vehicles.Commands.UpdateVehicleStatus;
using Nimpression.Application.Features.Vehicles.DTOs;
using Nimpression.Application.Features.Vehicles.Queries.GetActiveVehicleAssignment;
using Nimpression.Application.Features.Vehicles.Queries.GetOdometerReadings;
using Nimpression.Application.Features.Vehicles.Queries.GetVehicleAssignments;
using Nimpression.Application.Features.Vehicles.Queries.GetVehicleById;
using Nimpression.Application.Features.Vehicles.Queries.GetVehiclesList;
using Nimpression.Domain.Enums;
using Xunit;

namespace Nimpression.Application.Tests.Vehicles.Validators;

public class VehicleValidatorTests
{
    [Fact]
    public void CreateVehicleCommandValidator_ValidCommand_PassesValidation()
    {
        var validator = new CreateVehicleCommandValidator();
        var command = new CreateVehicleCommand(
            "ABC123",
            "Toyota",
            "Hilux",
            2023,
            "ENC_VIN",
            10000m,
            15000m);

        var result = validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateVehicleCommandValidator_InvalidCommand_FailsValidation()
    {
        var validator = new CreateVehicleCommandValidator();
        var command = new CreateVehicleCommand(
            "", // Empty rego
            "", // Empty make
            "", // Empty model
            1800, // Invalid year
            "ENC_VIN",
            -10m, // Negative km
            0m); // Zero service interval

        var result = validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5);
    }

    [Fact]
    public void UpdateVehicleCommandValidator_ValidCommand_PassesValidation()
    {
        var validator = new UpdateVehicleCommandValidator();
        var command = new UpdateVehicleCommand(Guid.NewGuid(), null, null, null, VehicleStatus.Active);
        var result = validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateVehicleStatusCommandValidator_ValidCommand_PassesValidation()
    {
        var validator = new UpdateVehicleStatusCommandValidator();
        var command = new UpdateVehicleStatusCommand(Guid.NewGuid(), VehicleStatus.Maintenance);
        var result = validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void AssignVehicleCommandValidator_EmptyIds_FailsValidation()
    {
        var validator = new AssignVehicleCommandValidator();
        var command = new AssignVehicleCommand(Guid.Empty, Guid.Empty);
        var result = validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ReleaseVehicleAssignmentCommandValidator_EmptyId_FailsValidation()
    {
        var validator = new ReleaseVehicleAssignmentCommandValidator();
        var command = new ReleaseVehicleAssignmentCommand(Guid.Empty);
        var result = validator.Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RecordOdometerReadingCommandValidator_NegativeKm_FailsValidation()
    {
        var validator = new RecordOdometerReadingCommandValidator();
        var command = new RecordOdometerReadingCommand(Guid.NewGuid(), Guid.NewGuid(), -5m);
        var result = validator.Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RecordVehicleServiceCommandValidator_NegativeKm_FailsValidation()
    {
        var validator = new RecordVehicleServiceCommandValidator();
        var command = new RecordVehicleServiceCommand(Guid.NewGuid(), -10m);
        var result = validator.Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void QueryValidators_ValidateInputs()
    {
        Assert.False(new GetVehicleByIdQueryValidator().Validate(new GetVehicleByIdQuery(Guid.Empty)).IsValid);
        Assert.True(new GetVehicleByIdQueryValidator().Validate(new GetVehicleByIdQuery(Guid.NewGuid())).IsValid);

        Assert.False(new GetActiveVehicleAssignmentQueryValidator().Validate(new GetActiveVehicleAssignmentQuery(Guid.Empty)).IsValid);
        Assert.True(new GetActiveVehicleAssignmentQueryValidator().Validate(new GetActiveVehicleAssignmentQuery(Guid.NewGuid())).IsValid);

        Assert.False(new GetVehicleAssignmentsQueryValidator().Validate(new GetVehicleAssignmentsQuery(Guid.Empty)).IsValid);
        Assert.True(new GetVehicleAssignmentsQueryValidator().Validate(new GetVehicleAssignmentsQuery(Guid.NewGuid())).IsValid);

        Assert.False(new GetOdometerReadingsQueryValidator().Validate(new GetOdometerReadingsQuery(Guid.Empty, 10)).IsValid);
        Assert.False(new GetOdometerReadingsQueryValidator().Validate(new GetOdometerReadingsQuery(Guid.NewGuid(), 0)).IsValid);
        Assert.True(new GetOdometerReadingsQueryValidator().Validate(new GetOdometerReadingsQuery(Guid.NewGuid(), 50)).IsValid);

        Assert.False(new GetVehiclesListQueryValidator().Validate(new GetVehiclesListQuery(Page: 0)).IsValid);
        Assert.False(new GetVehiclesListQueryValidator().Validate(new GetVehiclesListQuery(PageSize: 101)).IsValid);
        Assert.True(new GetVehiclesListQueryValidator().Validate(new GetVehiclesListQuery(Page: 1, PageSize: 20)).IsValid);
    }
}
