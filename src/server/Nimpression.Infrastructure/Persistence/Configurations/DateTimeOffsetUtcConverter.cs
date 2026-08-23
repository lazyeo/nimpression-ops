using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class DateTimeOffsetUtcConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public DateTimeOffsetUtcConverter()
        : base(
            v => v.ToUniversalTime(),
            v => v)
    {
    }
}

public class NullableDateTimeOffsetUtcConverter : ValueConverter<DateTimeOffset?, DateTimeOffset?>
{
    public NullableDateTimeOffsetUtcConverter()
        : base(
            v => v.HasValue ? v.Value.ToUniversalTime() : v,
            v => v)
    {
    }
}
