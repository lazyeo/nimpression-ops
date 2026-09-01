using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Tests.Privacy.TestDoubles;

public sealed class FakePrivacyRepository : IPrivacyRepository
{
    public DriverPersonalDataExportDto? MockExportData { get; set; }
    public RetentionCleanupReportDto? MockCleanupReport { get; set; }
    public AnonymizationResultDto? MockAnonymizationResult { get; set; }
    public PrivacyConsentDto? MockConsentStatus { get; set; }
    public List<DataSubjectRequest> StoredRequests { get; } = [];
    public List<(Guid UserId, string PolicyVersion, DateTimeOffset ConsentedAt, string? IpAddress, string? UserAgent)> RecordedConsents { get; } = [];

    public Task<DriverPersonalDataExportDto?> GetDriverPersonalDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MockExportData);
    }

    public Task<DataSubjectRequest?> GetLatestRequestAsync(Guid userId, DataSubjectRequestKind kind, CancellationToken cancellationToken = default)
    {
        var item = StoredRequests.LastOrDefault(r => r.SubjectUserId == userId && r.Kind == kind);
        return Task.FromResult(item);
    }

    public Task AddDataSubjectRequestAsync(DataSubjectRequest request, CancellationToken cancellationToken = default)
    {
        StoredRequests.Add(request);
        return Task.CompletedTask;
    }

    public Task<RetentionCleanupReportDto> ExecuteRetentionCleanupAsync(DateTimeOffset referenceDate, bool execute, CancellationToken cancellationToken = default)
    {
        if (MockCleanupReport is not null)
        {
            return Task.FromResult(MockCleanupReport);
        }

        var report = new RetentionCleanupReportDto(
            referenceDate,
            !execute,
            execute ? 5 : 5,
            execute ? 2 : 2,
            execute ? 3 : 3,
            referenceDate,
            [execute ? "[LIVE] Purged" : "[DRY-RUN] Eligible"]);

        return Task.FromResult(report);
    }

    public Task<AnonymizationResultDto> AnonymizeDriverAsync(Guid driverId, DateTimeOffset referenceDate, CancellationToken cancellationToken = default)
    {
        if (MockAnonymizationResult is not null)
        {
            return Task.FromResult(MockAnonymizationResult);
        }

        var result = new AnonymizationResultDto(
            driverId,
            Guid.NewGuid(),
            referenceDate,
            $"Driver #{driverId.ToString("N")[..6]}",
            1250.50m,
            1250.50m,
            3,
            3,
            1,
            1,
            10,
            10);

        return Task.FromResult(result);
    }

    public Task<PrivacyConsentDto> GetPrivacyConsentStatusAsync(Guid userId, string policyVersion, CancellationToken cancellationToken = default)
    {
        if (MockConsentStatus is not null)
        {
            return Task.FromResult(MockConsentStatus);
        }

        var recorded = RecordedConsents.LastOrDefault(c => c.UserId == userId && c.PolicyVersion == policyVersion);
        var hasConsented = recorded != default;

        return Task.FromResult(new PrivacyConsentDto(
            userId,
            policyVersion,
            hasConsented,
            hasConsented ? recorded.ConsentedAt : null,
            hasConsented ? recorded.IpAddress : null,
            "Notice Title",
            "Notice Summary",
            "Nimpression Ops Privacy Statement (NZ Privacy Act 2020) Full Notice"));
    }

    public Task RecordPrivacyConsentAsync(Guid userId, string policyVersion, DateTimeOffset consentedAt, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        RecordedConsents.Add((userId, policyVersion, consentedAt, ipAddress, userAgent));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DataSubjectRequestDto>> GetDataSubjectRequestsAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        var query = StoredRequests.AsEnumerable();
        if (userId.HasValue)
        {
            query = query.Where(r => r.SubjectUserId == userId.Value);
        }

        var list = query.Select(r => new DataSubjectRequestDto(
            r.Id,
            r.SubjectUserId,
            r.Kind,
            r.Status,
            r.RequestedAt,
            r.CompletedAt,
            r.ExportKey,
            r.RejectionReason)).ToList();

        return Task.FromResult<IReadOnlyList<DataSubjectRequestDto>>(list);
    }
}
