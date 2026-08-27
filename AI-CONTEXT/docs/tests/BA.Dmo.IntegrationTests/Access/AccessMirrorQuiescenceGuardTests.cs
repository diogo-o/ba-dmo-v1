namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// SCHEMA-RAT-03B architecture guard: the legacy access mirrors are RETIRED
/// as runtime objects — zero source references are allowed to either
/// structure anywhere under <c>src/</c>.
///
///   * <c>internal_user_access_templates</c> — the N27 junction table;
///   * <c>profile_title</c> — the user-level profile mirror column of
///     internal_users (snake_case; the PascalCase <c>ProfileTitle</c> property
///     and record slot remain — they are presentation/compatibility shape).
///
/// Allow-list for the identifiers: the historical and quiescence migration
/// files under <c>database/migrations/</c> (N27…N33 — they must still mention
/// the objects they created/revoked) and the documentation tree
/// (<c>AI-CONTEXT/</c>, <c>reports/</c> — not scanned here). Executed
/// PostgreSQL behaviour of the N33 revoke is covered by the env-guarded
/// <c>RemediationGuardTests.N33_*</c> probes (BA_DMO_TEST_DATABASE).
/// </summary>
public sealed class AccessMirrorQuiescenceGuardTests
{
    private static readonly string[] Needles =
    [
        "internal_user_access_templates",
        "profile_title"
    ];

    [Fact]
    public void Src_HasZeroReferences_ToLegacyAccessMirrors()
    {
        var root = ResolveRepositoryRoot();
        var srcDir = Path.Combine(root, "src");
        Assert.True(Directory.Exists(srcDir), $"src/ directory missing under {root}");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
        {
            // Generated/build artifacts are not sources of truth.
            var parts = file.Split(Path.DirectorySeparatorChar);
            if (parts.Contains("bin", StringComparer.Ordinal)
                || parts.Contains("obj", StringComparer.Ordinal))
                continue;

            var isSource = file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase);
            if (!isSource)
                continue;

            var text = File.ReadAllText(file);
            foreach (var needle in Needles)
            {
                if (text.Contains(needle, StringComparison.Ordinal))
                {
                    offenders.Add(
                        $"{Path.GetRelativePath(root, file)}: contains '{needle}'");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "SCHEMA-RAT-03B: the legacy access mirrors are retired. No src/ file "
            + "may reference either structure (junction table or user-level "
            + "profile mirror column). Offenders:\n"
            + string.Join("\n", offenders));
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BA-DMO.sln"))
                && Directory.Exists(Path.Combine(directory.FullName, "database", "migrations"))
                && Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the BA-DMO repository root.");
    }
}