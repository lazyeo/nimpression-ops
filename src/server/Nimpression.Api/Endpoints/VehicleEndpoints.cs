using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Vehicles.Commands.AssignVehicle;
using Nimpression.Application.Features.Vehicles.Commands.CreateVehicle;
using Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;
using Nimpression.Application.Features.Vehicles.Commands.RecordVehicleService;
using Nimpression.Application.Features.Vehicles.Commands.ReleaseVehicleAssignment;
using Nimpression.Application.Features.Vehicles.Commands.UpdateVehicle;
using Nimpression.Application.Features.Vehicles.Commands.UpdateVehicleStatus;
using Nimpression.Application.Features.Vehicles.Queries.GetActiveVehicleAssignment;
using Nimpression.Application.Features.Vehicles.Queries.GetOdometerReadings;
using Nimpression.Application.Features.Vehicles.Queries.GetVehicleAssignments;
using Nimpression.Application.Features.Vehicles.Queries.GetVehicleById;
using Nimpression.Application.Features.Vehicles.Queries.GetVehiclesList;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 车辆管理模块 Minimal API 端点。
/// 遵循 IEndpointModule 约定由 EndpointModuleExtensions 自动发现与挂载，无需在 Program.cs 手动注册。
/// </summary>
public sealed class VehicleEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/vehicles")
            .WithTags("Vehicles");

        // F3.1 车辆 CRUD 与状态管理
        group.MapPost("/", CreateVehicle)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithName("CreateVehicle")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", GetVehicles)
            .RequireAuthorization(AuthorizationPolicies.Dispatcher)
            .WithName("GetVehicles")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetVehicleById)
            .RequireAuthorization(AuthorizationPolicies.Dispatcher)
            .WithName("GetVehicleById")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateVehicle)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithName("UpdateVehicle")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/status", UpdateVehicleStatus)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithName("UpdateVehicleStatus")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/service", RecordVehicleService)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithName("RecordVehicleService")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // F3.2 车辆分派管理
        group.MapPost("/{id:guid}/assignments", AssignVehicle)
            .RequireAuthorization(AuthorizationPolicies.Dispatcher)
            .WithName("AssignVehicle")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/assignments/{assignmentId:guid}/release", ReleaseAssignment)
            .RequireAuthorization(AuthorizationPolicies.Dispatcher)
            .WithName("ReleaseVehicleAssignment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}/assignments/active", GetActiveAssignment)
            .RequireAuthorization(AuthorizationPolicies.Dispatcher)
            .WithName("GetActiveVehicleAssignment")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}/assignments", GetVehicleAssignments)
            .RequireAuthorization(AuthorizationPolicies.Dispatcher)
            .WithName("GetVehicleAssignments")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // F3.3 里程上报与历史
        group.MapPost("/{id:guid}/odometer", RecordOdometerReading)
            .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
            .WithName("RecordOdometerReading")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}/odometer", GetOdometerReadings)
            .RequireAuthorization(AuthorizationPolicies.Dispatcher)
            .WithName("GetOdometerReadings")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> CreateVehicle(
        [FromBody] CreateVehicleRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreateVehicleCommand(
            request.Rego,
            request.Make,
            request.Model,
            request.Year,
            request.VinEnc,
            request.OdometerKm,
            request.ServiceIntervalKm,
            request.LastServiceOdometerKm,
            request.WofExpiry,
            request.CofExpiry,
            request.InsuranceExpiry,
            request.Status);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetVehicles(
        [FromQuery] string? search,
        [FromQuery] VehicleStatus? status,
        [FromQuery] bool? serviceDueOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromServices] ISender sender = default!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVehiclesListQuery(search, status, serviceDueOnly, page, pageSize);
        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetVehicleById(
        [FromRoute] Guid id,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetVehicleByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateVehicle(
        [FromRoute] Guid id,
        [FromBody] UpdateVehicleRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateVehicleCommand(
            id,
            request.WofExpiry,
            request.CofExpiry,
            request.InsuranceExpiry,
            request.Status);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateVehicleStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateVehicleStatusRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateVehicleStatusCommand(id, request.Status);
        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RecordVehicleService(
        [FromRoute] Guid id,
        [FromBody] RecordVehicleServiceRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RecordVehicleServiceCommand(id, request.ServiceOdometerKm);
        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> AssignVehicle(
        [FromRoute] Guid id,
        [FromBody] AssignVehicleRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new AssignVehicleCommand(id, request.DriverId, request.AssignedAt);
        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReleaseAssignment(
        [FromRoute] Guid assignmentId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new ReleaseVehicleAssignmentCommand(assignmentId);
        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetActiveAssignment(
        [FromRoute] Guid id,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetActiveVehicleAssignmentQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetVehicleAssignments(
        [FromRoute] Guid id,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetVehicleAssignmentsQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RecordOdometerReading(
        [FromRoute] Guid id,
        [FromBody] RecordOdometerReadingRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RecordOdometerReadingCommand(
            id,
            request.DriverId,
            request.ReadingKm,
            request.PhotoKey,
            request.RecordedAt,
            request.Source);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetOdometerReadings(
        [FromRoute] Guid id,
        [FromQuery] int limit = 50,
        [FromServices] ISender sender = default!,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOdometerReadingsQuery(id, limit);
        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult();
    }
}

public sealed record CreateVehicleRequest(
    string Rego,
    string Make,
    string Model,
    int Year,
    string VinEnc,
    decimal OdometerKm,
    decimal ServiceIntervalKm,
    decimal? LastServiceOdometerKm = null,
    DateOnly? WofExpiry = null,
    DateOnly? CofExpiry = null,
    DateOnly? InsuranceExpiry = null,
    VehicleStatus Status = VehicleStatus.Active);

public sealed record UpdateVehicleRequest(
    DateOnly? WofExpiry,
    DateOnly? CofExpiry,
    DateOnly? InsuranceExpiry,
    VehicleStatus Status);

public sealed record UpdateVehicleStatusRequest(VehicleStatus Status);

public sealed record RecordVehicleServiceRequest(decimal ServiceOdometerKm);

public sealed record AssignVehicleRequest(Guid DriverId, DateTimeOffset? AssignedAt = null);

public sealed record RecordOdometerReadingRequest(
    Guid DriverId,
    decimal ReadingKm,
    string? PhotoKey = null,
    DateTimeOffset? RecordedAt = null,
    string Source = "DriverApp");
