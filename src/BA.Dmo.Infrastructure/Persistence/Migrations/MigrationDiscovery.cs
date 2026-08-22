using System.Text.RegularExpressions;

namespace BA.Dmo.Infrastructure.Persistence.Migrations;

/// <summary>
/// Deterministic discovery of the fresh-build migration family
/// (Plan-V3 BT-08, 06_DATA §2). Rules:
/// - only files matching the family pattern (N##_name.sql) are migrations;
/// - canonical order = ordinal file-name order (N01 … N12 … forward-only);
/// - every version prefix must be unique — duplicates fail explicitly.
/// There is no recursion, no configurable comparator and no environment
/// dependence: the same directory always yields the same ordered family.
/// </summary>
public static partial class MigrationDiscovery
{
    [GeneratedRegex(@"^(N\d{2})_[A-Za-z0-9_]+\.sql$")]
    private static partial Regex MigrationFileNamePattern();

    public static IReadOnlyList<MigrationFile> Discover(string migrationsDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(migrationsDirectory);
        if (!Directory.Exists(migrationsDirectory))
            throw new MigrationDiscoveryException(
                $"Migrations directory not found: '{migrationsDirectory}'.");

        var discovered = new List<MigrationFile>();
        foreach (var fileName in Directory.EnumerateFiles(migrationsDirectory, "*.sql")
                     .Select(Path.GetFileName)
                     .Where(name => !string.IsNullOrEmpty(name))
                     .Cast<string>()
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            var match = MigrationFileNamePattern().Match(fileName);
            if (!match.Success)
                throw new MigrationDiscoveryException(
                    $"File '{fileName}' in '{migrationsDirectory}' does not match the " +
                    "fresh-build migration family pattern 'N##_<name>.sql'.");

            discovered.Add(new MigrationFile(
                match.Groups[1].Value,
                fileName,
                Path.Combine(migrationsDirectory, fileName)));
        }

        var duplicate = discovered
            .GroupBy(m => m.Version)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new MigrationDiscoveryException(
                $"Duplicate migration version '{duplicate.Key}' in '{migrationsDirectory}': " +
                string.Join(", ", duplicate.Select(m => m.FileName)));

        return discovered;
    }
}
