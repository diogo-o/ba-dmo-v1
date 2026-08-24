using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// U-03 base mappings tests (roadmap "mappings base"; unit-testable part).
/// Row-level mapping against a real database belongs to the integration
/// smoke phase when a test DB is available (roadmap U-03 "testes").
/// </summary>
public class PersistenceMappingsTests
{
    private sealed class SampleRow
    {
        public Guid InternalUserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public int TotalQty { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
    }

    [Fact]
    public void Configure_EnablesUnderscoreMatching_AndIsIdempotent()
    {
        PersistenceMappings.Configure();
        PersistenceMappings.Configure();

        Assert.True(PersistenceMappings.IsConfigured);
        Assert.True(DefaultTypeMap.MatchNamesWithUnderscores);
    }

    [Fact]
    public void SnakeCaseColumns_MapToPascalCaseMembers()
    {
        PersistenceMappings.Configure();

        var map = new DefaultTypeMap(typeof(SampleRow));

        Assert.NotNull(map.GetMember("internal_user_id"));
        Assert.NotNull(map.GetMember("display_name"));
        Assert.NotNull(map.GetMember("total_qty"));
        Assert.NotNull(map.GetMember("created_at_utc"));
        Assert.Equal(
            nameof(SampleRow.TotalQty),
            map.GetMember("total_qty")!.Property!.Name);
    }

    [Fact]
    public void UnknownColumns_DoNotMap()
    {
        PersistenceMappings.Configure();

        var map = new DefaultTypeMap(typeof(SampleRow));

        Assert.Null(map.GetMember("column_that_does_not_exist"));
    }
}
