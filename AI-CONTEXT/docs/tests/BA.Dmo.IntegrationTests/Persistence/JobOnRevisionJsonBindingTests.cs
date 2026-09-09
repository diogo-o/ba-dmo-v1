using System.Text.RegularExpressions;

namespace BA.Dmo.IntegrationTests.Persistence;

public sealed class JobOnRevisionJsonBindingTests
{
    /// <summary>
    /// Method-scoped regression guard for the shared graph persistence path:
    /// CreateAtomicallyAsync / SaveRevisionGraphAsync / AlterDatesAtomicallyAsync /
    /// DuplicateAtomicallyAsync all insert revisions through
    /// InsertRevisionGraphCoreAsync. A whole-file scan passes by matching the casts
    /// in the sibling InsertRevisionAsync / InsertImageMutationAsync methods, which
    /// is exactly how the missing casts escaped into PROD (SQLSTATE 42804). Only the
    /// InsertRevisionGraphCoreAsync method body is inspected here.
    /// </summary>
    [Fact]
    public void RevisionInsert_BindsEveryJsonSnapshotWithExplicitJsonbCast()
    {
        var source = File.ReadAllText(FindSource(
            "src", "BA.Dmo.Infrastructure", "Access", "DapperJobOnRepository.cs"));

        var start = source.IndexOf(
            "private static async Task InsertRevisionGraphCoreAsync(", StringComparison.Ordinal);
        Assert.True(start >= 0,
            "InsertRevisionGraphCoreAsync not found in DapperJobOnRepository.cs.");

        var end = source.IndexOf(
            "private static async Task InsertComponentCoreAsync(", start, StringComparison.Ordinal);
        Assert.True(end > start,
            "InsertComponentCoreAsync not found after InsertRevisionGraphCoreAsync.");

        var method = source[start..end];

        // The revision INSERT must bind every JSON snapshot parameter explicitly as
        // jsonb. On the broken variant the method contained "@ProductionSnapshot,"
        // etc. and PostgreSQL rejected the text -> jsonb assignment (42804).
        foreach (var parameter in new[]
        {
            "ProductionSnapshot", "ReferenceSnapshot", "MachineSnapshot", "DatesSnapshot",
            "Sections", "TypeSnapshot", "StopSnapshot", "WeightSnapshot", "ProcessSnapshot"
        })
        {
            Assert.Matches(new Regex("@(?:" + parameter + @")::jsonb\b"), method);
        }

        // drop_count is numeric: it must keep its plain parameter and never take a
        // jsonb cast.
        Assert.Contains("@DropCount,", method);
        Assert.DoesNotMatch(new Regex("@DropCount::jsonb\b"), method);
    }

    private static string FindSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository source.");
    }
}
