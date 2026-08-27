using System.Data;
using System.Reflection;
using System.Xml.Linq;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// U-03 architecture guards (Plan-V3 constraints): no EF Core, no ORM
/// framework, no global/static connection state, no ambient TransactionScope,
/// no direct Npgsql access from the web layer, approved dependency graph
/// (03_ARCH §1/§4), and browser code never touching the database
/// (GLM-DATA-01).
/// </summary>
public class PersistenceArchitectureGuardTests
{
    private static Assembly InfrastructureAssembly =>
        typeof(BA.Dmo.Infrastructure.Persistence.DbConnectionFactory).Assembly;

    private static Assembly DomainAssembly =>
        typeof(BA.Dmo.Domain.Shared.Kernel.DomainError).Assembly;

    private static Assembly ApplicationAssembly =>
        typeof(BA.Dmo.Application.Shared.Persistence.IDbConnectionFactory).Assembly;

    private static Assembly WebAssembly => typeof(Program).Assembly;

    [Fact]
    public void NoEfCoreOrOrmFramework_IsReferenced()
    {
        var assemblies = new[]
        {
            DomainAssembly, ApplicationAssembly, InfrastructureAssembly, WebAssembly
        };

        var offenders = assemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Where(name =>
                name.Name!.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)
                || name.Name.Contains("DbUp", StringComparison.OrdinalIgnoreCase)
                || name.Name.Contains("NHibernate", StringComparison.OrdinalIgnoreCase))
            .Select(name => name.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Persistence must stay Npgsql + Dapper (GLM-DATA-01). Offenders: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void NoGlobalOrStaticConnectionState_ExistsInProductionCode()
    {
        var assemblies = new[] { InfrastructureAssembly, WebAssembly };

        var offenders = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(field =>
                typeof(IDbConnection).IsAssignableFrom(field.FieldType)
                || field.FieldType == typeof(NpgsqlConnection))
            .Select(field => $"{field.DeclaringType?.FullName}.{field.Name}")
            .ToList();

        Assert.True(offenders.Count == 0,
            $"No static/global DB connections allowed. Offenders: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void NoAmbientTransactionScope_DependencyExists()
    {
        var assemblies = new[] { ApplicationAssembly, InfrastructureAssembly, WebAssembly };

        var offenders = assemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Where(name => name.Name!.StartsWith("System.Transactions", StringComparison.Ordinal))
            .Select(name => name.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Transactions are explicit units of work only (GLM-DATA-05). Offenders: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void WebLayer_DoesNotReferenceNpgsqlDirectly()
    {
        // The web layer accesses persistence only through Application ports /
        // Infrastructure (DI); direct driver access stays in Infrastructure.
        Assert.DoesNotContain(
            WebAssembly.GetReferencedAssemblies(),
            name => name.Name!.Equals("Npgsql", StringComparison.Ordinal));
    }

    [Fact]
    public void DependencyGraph_MatchesPlanV3()
    {
        // Read from the csproj files (03_ARCH §1/§4): reflection cannot prove
        // the graph because compilers prune unused references from IL.
        Assert.Empty(ProjectReferences("src/BA.Dmo.Domain"));
        Assert.Equal(["BA.Dmo.Domain"], ProjectReferences("src/BA.Dmo.Application"));
        Assert.Equal(
            ["BA.Dmo.Application", "BA.Dmo.Domain"],
            ProjectReferences("src/BA.Dmo.Infrastructure"));
        Assert.Equal(
            ["BA.Dmo.Application", "BA.Dmo.Infrastructure"],
            ProjectReferences("src/BA.Dmo.Web"));
        Assert.Equal(
            ["BA.Dmo.Application", "BA.Dmo.Domain"],
            ProjectReferences("AI-CONTEXT/docs/tests/BA.Dmo.UnitTests"));
        Assert.Equal(
            ["BA.Dmo.Infrastructure", "BA.Dmo.Web"],
            ProjectReferences("AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests"));
    }

    [Fact]
    public void Infrastructure_DoesNotLeakIntoDomain()
    {
        // Domain must stay free of persistence/infrastructure knowledge,
        // whether referenced (csproj) or actually used (IL).
        Assert.DoesNotContain(ProjectReferences("src/BA.Dmo.Domain"),
            name => name is "BA.Dmo.Infrastructure" or "BA.Dmo.Application");
        Assert.DoesNotContain(
            DomainAssembly.GetReferencedAssemblies(),
            name => name.Name!.Equals("Npgsql", StringComparison.Ordinal)
                || name.Name.Equals("Dapper", StringComparison.Ordinal));
    }

    private static string[] ProjectReferences(string projectRelativePath)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var csprojPath = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)), "*.csproj")
            .Single();

        var document = XDocument.Load(csprojPath);
        return document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(
                element.Attribute("Include")!.Value))
            .Select(name => name.Replace('\\', '/').Split('/').Last())
            .Order()
            .ToArray();
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BA-DMO.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("BA-DMO.sln not found above the test base directory.");
    }
}
