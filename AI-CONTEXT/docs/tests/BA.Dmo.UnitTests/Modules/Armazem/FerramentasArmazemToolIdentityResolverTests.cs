using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.UnitTests.Modules.Armazem;

/// <summary>
/// U-14 — FerramentasArmazemToolIdentityResolver adapter tests. Maps Ferramentas
/// identity → Armazém-owned WarehouseToolIdentity; accepts CM/MF/BQ; rejects
/// PU/CS; exposes canonical reference/lot/type/name; and
/// never mutates Ferramentas (read-only port consumed).
/// </summary>
public class FerramentasArmazemToolIdentityResolverTests
{
    private readonly FakeFerramentasIdentityLookup _lookup = new();
    private readonly FerramentasArmazemToolIdentityResolver _resolver;

    public FerramentasArmazemToolIdentityResolverTests()
    {
        _resolver = new FerramentasArmazemToolIdentityResolver(_lookup);
    }

    private FerramentasIdentityHit Hit(FerramentasToolType type, string refCode, string lot) =>
        new(Guid.NewGuid(), Guid.NewGuid(), type, refCode, lot, "Contra-molde");

    [Theory]
    [InlineData(FerramentasToolType.CM)]
    [InlineData(FerramentasToolType.MF)]
    [InlineData(FerramentasToolType.BQ)]
    public async Task Search_WarehouseTypes_AreAccepted(FerramentasToolType type)
    {
        _lookup.Hits.Add(Hit(type, "REF", "1"));
        var result = await _resolver.SearchAsync(type.ToString(), "REF", null);
        var hit = Assert.Single(result);
        Assert.Equal(WarehouseToolDomain.Ferramentas, hit.Domain);
        Assert.Equal("REF", hit.Reference);
        Assert.Equal("1", hit.Lot);
        Assert.Equal("Contra-molde", hit.TechnicalName);
        Assert.Equal(type.ToString(), hit.Type);
    }

    [Theory]
    [InlineData("PU")]
    [InlineData("CS")]
    public async Task Search_UnsupportedTypes_ReturnEmpty(string type)
    {
        _lookup.Hits.Add(Hit(FerramentasToolType.BQ, "BQ1", "1"));
        var result = await _resolver.SearchAsync(type, "BQ1", null);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Resolve_MapsToWarehouseOwnedIdentity()
    {
        var hit = Hit(FerramentasToolType.MF, "MF-x", "3");
        _lookup.Hits.Add(hit);
        var result = await _resolver.ResolveAsync(hit.ToolLoteId);
        Assert.NotNull(result);
        Assert.Equal(hit.ToolLoteId, result!.ToolId);
        Assert.Equal(WarehouseToolDomain.Ferramentas, result.Domain);
        Assert.Equal("3", result.Lot);
    }

    [Fact]
    public async Task Resolve_Missing_ReturnsNull()
    {
        var result = await _resolver.ResolveAsync(Guid.NewGuid());
        Assert.Null(result);
    }
}
