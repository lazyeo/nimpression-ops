using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Privacy.Commands.AnonymizeDriver;

/// <summary>
/// 离职司机数据不可逆匿名化命令（AC N2.5）。
/// 关键纪律：离职司机数据匿名化而非物理删除（工资单与事故记录有法定保存期）。
/// 替换可识别 PII 为不可逆占位符，保留全部数值与关联关系。
/// </summary>
public sealed record AnonymizeDriverCommand(
    Guid DriverId,
    string? Reason = null) : IRequest<Result<AnonymizationResultDto>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Driver";
    public Guid? AuditEntityId => DriverId;
    public string AuditAction => "AnonymizeDriverData";
}

public sealed class AnonymizeDriverCommandValidator : AbstractValidator<AnonymizeDriverCommand>
{
    public AnonymizeDriverCommandValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEmpty()
            .WithMessage("DriverId cannot be empty.");
    }
}

public sealed class AnonymizeDriverCommandHandler(
    IPrivacyRepository privacyRepository,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<AnonymizeDriverCommand, Result<AnonymizationResultDto>>
{
    public async Task<Result<AnonymizationResultDto>> Handle(
        AnonymizeDriverCommand request,
        CancellationToken cancellationToken)
    {
        // 仅管理员有权发起司机数据匿名化处理（N1.3 权限防护）
        if (currentUser.Role != UserRole.Admin)
        {
            return Error.Forbidden(
                "forbidden_anonymization",
                "Only administrators are authorized to execute driver anonymization.");
        }

        var now = dateTimeProvider.UtcNow;
        var result = await privacyRepository.AnonymizeDriverAsync(
            request.DriverId,
            now,
            cancellationToken);

        // 强断言：匿名化前后聚合数字不变
        if (result.GrossPaySumBefore != result.GrossPaySumAfter ||
            result.PayslipsCountBefore != result.PayslipsCountAfter ||
            result.IncidentReportsCountBefore != result.IncidentReportsCountAfter)
        {
            return Error.Unprocessable(
                "anonymization_integrity_violation",
                "Anonymization corrupted aggregated financial or statutory records integrity.");
        }

        return Result<AnonymizationResultDto>.Success(result);
    }
}
