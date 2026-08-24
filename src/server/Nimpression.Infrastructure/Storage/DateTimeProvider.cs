using Nimpression.Application.Common.Abstractions;
using Nimpression.Domain.Common;

namespace Nimpression.Infrastructure.Storage;

/// <summary>
/// 新西兰时区可替换时钟实现。
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset NzNow => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, NzTimeZone.Info);

    public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
}
