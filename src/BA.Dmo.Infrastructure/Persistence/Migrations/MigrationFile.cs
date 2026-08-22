namespace BA.Dmo.Infrastructure.Persistence.Migrations;

/// <summary>
/// One discovered migration script of the fresh-build family
/// (Plan-V3 BT-08, 06_DATA §2: database/migrations/N01_identity.sql … N12_rls.sql).
/// </summary>
public sealed record MigrationFile(string Version, string FileName, string FullPath)
{
    public override string ToString() => FileName;
}

/// <summary>
/// One applied-migration record as tracked in <c>schema_migrations</c>
/// (Plan-V3 06_DATA §12 minimum contract: version/id, filename, sha256, applied_at).
/// </summary>
public sealed record AppliedMigration(
    string Version,
    string FileName,
    string Sha256,
    DateTimeOffset AppliedAtUtc);
