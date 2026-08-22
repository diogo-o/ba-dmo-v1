using System.Data;
using Dapper;

namespace BA.Dmo.Infrastructure.Persistence;

/// <summary>
/// Bridges PostgreSQL timestamptz (returned by Npgsql as <see cref="DateTime"/>)
/// to the <see cref="DateTimeOffset"/> used by domain records, both when
/// materializing rows and when binding parameters.
/// </summary>
public sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
        parameter.Value = value.UtcDateTime;

    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime dt when dt.Kind == DateTimeKind.Unspecified =>
            new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
        DateTime dt => new DateTimeOffset(dt).ToUniversalTime(),
        _ => throw new InvalidOperationException(
            $"Cannot convert {value.GetType().Name} to DateTimeOffset.")
    };
}
