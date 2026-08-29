using System.Text.Json;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Timesheets.Commands.AdminCorrectShift;

/// <summary>
/// 管理员更正打卡记录命令处理器（F6.4）。
/// 严格校验管理员权限、强制更正理由（422），原值与新值全量录入审计。
/// </summary>
public sealed class AdminCorrectShiftCommandHandler(
    IShiftEntryRepository shiftEntryRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditSink auditSink,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<AdminCorrectShiftCommand, Result>
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<Result> Handle(AdminCorrectShiftCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            return Error.Forbidden("forbidden", "Only system administrators can perform shift corrections.");
        }

        // F6.4 验收标准：必须填理由（缺理由 422）
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Unprocessable("correction_reason_required", "Admin correction reason is mandatory.");
        }

        if (request.NewBreakMinutes < 0)
        {
            return Error.Unprocessable("invalid_break_minutes", "Break minutes cannot be negative.");
        }

        if (request.NewClockOutAt.HasValue && request.NewClockOutAt.Value < request.NewClockInAt)
        {
            return Error.Unprocessable(
                "invalid_clock_times",
                $"Clock out time ({request.NewClockOutAt.Value}) cannot be earlier than clock in time ({request.NewClockInAt}).");
        }

        var shift = await shiftEntryRepository.GetByIdAsync(request.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Error.NotFound("shift_not_found", $"Shift entry with ID '{request.ShiftId}' was not found.");
        }

        // F6.4 验收标准：原值全量记录
        var beforeState = new
        {
            shift.Id,
            shift.DriverId,
            shift.ClockInAt,
            shift.ClockInLat,
            shift.ClockInLng,
            shift.ClockOutAt,
            shift.ClockOutLat,
            shift.ClockOutLng,
            shift.VehicleId,
            shift.BreakMinutes,
            shift.Note,
            Status = shift.Status.ToString(),
            shift.AdminCorrectionReason,
            shift.CorrectedByUserId,
            shift.CorrectedAt
        };
        var beforeJson = JsonSerializer.Serialize(beforeState, AuditJsonOptions);

        var adminUserId = currentUser.UserId ?? Guid.Empty;
        var correctedAt = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        shift.AdminCorrect(
            request.NewClockInAt,
            request.NewClockOutAt,
            request.NewBreakMinutes,
            request.Reason,
            adminUserId,
            correctedAt);

        // F6.4 验收标准：新值全量记录
        var afterState = new
        {
            shift.Id,
            shift.DriverId,
            shift.ClockInAt,
            shift.ClockInLat,
            shift.ClockInLng,
            shift.ClockOutAt,
            shift.ClockOutLat,
            shift.ClockOutLng,
            shift.VehicleId,
            shift.BreakMinutes,
            shift.Note,
            Status = shift.Status.ToString(),
            shift.AdminCorrectionReason,
            shift.CorrectedByUserId,
            shift.CorrectedAt
        };
        var afterJson = JsonSerializer.Serialize(afterState, AuditJsonOptions);

        // 录入审计表
        await auditSink.RecordAsync(
            "ShiftEntry",
            shift.Id,
            "AdminCorrectShift",
            beforeJson,
            afterJson,
            cancellationToken);

        shiftEntryRepository.Update(shift);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
