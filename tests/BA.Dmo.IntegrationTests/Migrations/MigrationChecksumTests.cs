using System.Text;
using BA.Dmo.Infrastructure.Persistence.Migrations;

namespace BA.Dmo.IntegrationTests.Migrations;

/// <summary>
/// U-02 test area 2: SHA-256 calculation over the raw migration content
/// (Plan-V3 PV-04, 06_DATA §12).
/// </summary>
public sealed class MigrationChecksumTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ba_dmo_checksum_").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void ComputeSha256_MatchesKnownFipsVector()
    {
        // SHA-256("abc") — canonical test vector.
        var hash = MigrationChecksum.ComputeSha256(Encoding.UTF8.GetBytes("abc"));

        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            hash);
    }

    [Fact]
    public void ComputeSha256File_HashesExactFileBytes()
    {
        var path = Path.Combine(_directory, "N01_a.sql");
        File.WriteAllText(path, "CREATE TABLE t (id int);");

        var fromFile = MigrationChecksum.ComputeSha256File(path);
        var fromBytes = MigrationChecksum.ComputeSha256(File.ReadAllBytes(path));

        Assert.Equal(fromBytes, fromFile);
        Assert.Matches("^[0-9a-f]{64}$", fromFile);
    }

    [Fact]
    public void ComputeSha256_DetectsAnyContentChange()
    {
        var original = MigrationChecksum.ComputeSha256(Encoding.UTF8.GetBytes("SELECT 1;"));
        var altered = MigrationChecksum.ComputeSha256(Encoding.UTF8.GetBytes("SELECT 1; "));

        Assert.NotEqual(original, altered);
    }
}
