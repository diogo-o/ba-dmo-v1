using System.Security.Cryptography;

namespace BA.Dmo.Infrastructure.Persistence.Migrations;

/// <summary>
/// SHA-256 computation for migration files (Plan-V3 PV-04, 06_DATA §12).
/// The checksum is computed over the RAW file bytes — the exact content that
/// is sent whole to PostgreSQL — so any change to an applied script is
/// detected as a mismatch.
/// </summary>
public static class MigrationChecksum
{
    public static string ComputeSha256(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    public static string ComputeSha256File(string fullPath) =>
        ComputeSha256(File.ReadAllBytes(fullPath));
}
