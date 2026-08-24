using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-01 kernel unit tests: capability and module definition invariants
/// (Plan-V3 GLM-CAT-01/GLM-CAT-02 format rules).
/// </summary>
public class CapabilityAndModuleDefinitionTests
{
    [Theory]
    [InlineData("jobon.view", "jobon")]
    [InlineData("peso.aprovar", "peso")]
    [InlineData("reparacao_interna.corrigir", "reparacao_interna")]
    public void Capability_ParsesModuleSegment(string id, string expectedSegment)
    {
        var capability = new Capability(id);

        Assert.Equal(id, capability.Id);
        Assert.Equal(expectedSegment, capability.ModuleSegment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("noseparator")]
    [InlineData(".ação")]
    [InlineData("moduleId.")]
    [InlineData("with space.action")]
    public void Capability_InvalidFormat_IsRejected(string id)
    {
        Assert.Throws<ArgumentException>(() => new Capability(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    public void ModuleDefinition_InvalidModuleId_IsRejected(string moduleId)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModuleDefinition(moduleId, "Nome", ModuleKind.Module, 1, "/rota"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ModuleDefinition_EmptyDisplayName_IsRejected(string displayName)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModuleDefinition("peso", displayName, ModuleKind.Module, 1, "/peso"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("peso")]
    [InlineData("peso/")]
    public void ModuleDefinition_InitialRouteMustBeAbsolute(string route)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModuleDefinition("peso", "Peso", ModuleKind.Module, 1, route));
    }

    [Fact]
    public void ModuleDefinition_TrimsAndFreezesCapabilities()
    {
        var definition = new ModuleDefinition(
            " peso ", " Peso ", ModuleKind.Module, 21, " /peso ",
            [new Capability("peso.aprovar")]);

        Assert.Equal("peso", definition.ModuleId);
        Assert.Equal("Peso", definition.DisplayName);
        Assert.Equal("/peso", definition.InitialRoute);
        Assert.Single(definition.Capabilities);
    }
}
