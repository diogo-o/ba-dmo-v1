using System.Reflection;
using System.Text.RegularExpressions;

namespace BA.Dmo.IntegrationTests.Security;

/// <summary>
/// U-01 technical contract guard: no production debug bypass (Plan-V3 09_TEST §10.4,
/// GLM-ARCH-18). Production assemblies must contain no debug auth bypass, anonymous admin,
/// debug claims, insecure fallback identity or impersonation types. Test doubles are
/// confined to the tests/* projects.
/// LO-4 strengthening: the second test now verifies the real composition root
/// (entry point) instead of a tautological type-name check, and a source-level
/// guard pins the single sign-in call site and the absence of #if DEBUG auth code.
/// </summary>
public class NoDebugBypassGuardTests
{
    private static readonly string[] ForbiddenMarkers =
    [
        "debuguser",
        "debugauth",
        "debugclaim",
        "authbypass",
        "bypassauth",
        "anonymousadmin",
        "fallbackidentity",
        "impersonat"
    ];

    [Fact]
    public void ProductionAssemblies_ContainNoDebugAuthBypassTypes()
    {
        var productionAssemblies = new[]
        {
            typeof(BA.Dmo.Domain.Shared.Kernel.DomainError).Assembly,
            Assembly.Load("BA.Dmo.Application"),
            Assembly.Load("BA.Dmo.Infrastructure"),
            typeof(Program).Assembly
        };

        var offenders = productionAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => ForbiddenMarkers.Any(marker =>
                type.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(type => $"{type.Assembly.GetName().Name}: {type.FullName}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Production code must not contain debug authentication bypass types (GLM-ARCH-18). " +
            $"Offenders: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void WebStartup_EntryPoint_IsTheRealProgram_CompositionRoot()
    {
        // The web assembly must have a real entry point in the BA.Dmo.Web
        // namespace (the genuine Program composition root) — a debug/replacement
        // entry point would indicate a bypassable startup.
        var entryPoint = typeof(Program).Assembly.EntryPoint;
        Assert.NotNull(entryPoint);
        Assert.Equal("BA.Dmo.Web", entryPoint!.DeclaringType!.Assembly.GetName().Name);
        Assert.Equal("Program", entryPoint.DeclaringType.Name);

        // The composition root type itself must be free of bypass markers.
        Assert.DoesNotContain(
            ForbiddenMarkers,
            marker => typeof(Program).Name.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AuthPath_Sources_HaveNoDebugBlocks_AndExactlyOneSignInCallSite()
    {
        // Source-level guard (LO-4): the authentication surface must stay a
        // single, non-debug path. Skipped when the source tree is not present
        // next to the build output (e.g. CI without sources).
        var webSourceDir = FindSourceDirectory("src/BA.Dmo.Web/Program.cs");
        if (webSourceDir is null)
            return; // source tree not present next to build output (e.g. CI); guard is a no-op there

        var authDir = Path.Combine(webSourceDir, "Pages", "Auth");
        var cliDir = Path.Combine(webSourceDir, "Cli");
        var programPath = Path.Combine(webSourceDir, "Program.cs");
        var scannedFiles = new List<string> { programPath };
        scannedFiles.AddRange(Directory.EnumerateFiles(authDir, "*.cs", SearchOption.AllDirectories));
        if (Directory.Exists(cliDir))
            scannedFiles.AddRange(Directory.EnumerateFiles(cliDir, "*.cs", SearchOption.AllDirectories));

        var debugBlocks = scannedFiles
            .SelectMany(file => File.ReadAllLines(file))
            .Where(line => line.TrimStart().StartsWith("#if DEBUG", StringComparison.Ordinal))
            .ToList();
        Assert.True(
            debugBlocks.Count == 0,
            "The auth composition path must contain no #if DEBUG blocks. " +
            $"Offenders: {string.Join("; ", debugBlocks)}");

        var signInCallSites = scannedFiles
            .Select(file => (file, Matches: Regex.Matches(File.ReadAllText(file), @"\.SignInAsync\(")))
            .Where(x => x.Matches.Count > 0)
            .ToList();
        var totalSignInCalls = signInCallSites.Sum(x => x.Matches.Count);
        Assert.True(
            totalSignInCalls == 1,
            "Exactly one sign-in call site is allowed (Pages/Auth/Login.cshtml.cs). " +
            $"Found in: {string.Join("; ", signInCallSites.Select(x => x.file))}");
        var signInPaths = signInCallSites
            .Select(x => Path.GetFullPath(x.file))
            .ToList();
        Assert.Contains(
            Path.GetFullPath(Path.Combine(authDir, "Login.cshtml.cs")),
            signInPaths);
    }

    /// <summary>Walks up from the build output until the repo file is found.</summary>
    private static string? FindSourceDirectory(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return Path.Combine(dir.FullName, "src", "BA.Dmo.Web");
        }
        return null;
    }
}
