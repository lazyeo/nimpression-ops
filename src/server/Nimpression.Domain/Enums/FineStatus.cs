namespace Nimpression.Domain.Enums;

/// <summary>
/// 交通罚单审核状态。
/// </summary>
public enum FineStatus
{
    Submitted = 1,
    UnderReview = 2,
    Accepted = 3,
    Disputed = 4,
    Waived = 5,
}
