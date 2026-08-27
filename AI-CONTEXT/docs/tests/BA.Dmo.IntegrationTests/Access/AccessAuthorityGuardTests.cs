namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// SCHEMA-RAT-03A/03B access-authority source guards (D-1/D-2 + mirror
/// quiescence).
///
/// These are SOURCE guards, not executed PostgreSQL tests: they read the
/// repository's C# source to pin the authority contract of the identity and
/// admin persistence SQL (which lives in private consts not reachable at
/// runtime). Executed PostgreSQL behaviour is covered by the env-guarded
/// <c>RemediationGuardTests.N32_*</c>/<c>N33_*</c> probes
/// (BA_DMO_TEST_DATABASE) and by the ADO.NET-double projections
/// (<c>DapperAdminRepositoryProjectionTests</c>).
/// </summary>
public sealed class AccessAuthorityGuardTests
{
    [Fact]
    public void IdentityResolutionSql_ResolvesThroughDirectFk_AndDoesNotConsultJunction()
    {
        var sql = Read("DapperInternalUserRepository.cs");

        // D-2: identity joins access_templates through internal_users.template_id
        // (the canonical direct FK) and the template-owned profile.
        Assert.Contains("JOIN access_templates t ON t.template_id = u.template_id", sql, StringComparison.Ordinal);
        Assert.Contains(
            "LEFT JOIN access_template_profiles p ON p.template_id = t.template_id",
            sql, StringComparison.Ordinal);

        // The N27 junction must NOT participate in identity resolution SQL.
        // SCHEMA-RAT-03B: the junction is fully retired — no statement in the
        // identity repository references it at all (the bootstrap one-way
        // mirror insert was removed too).
        Assert.DoesNotContain("internal_user_access_templates", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdentityResolutionSql_ReadsFunctionalProfileFromTemplateProfileTable()
    {
        var sql = Read("DapperInternalUserRepository.cs");

        Assert.Contains("p.functional_profile   AS FunctionalProfile", sql, StringComparison.Ordinal);
        // SCHEMA-RAT-03B: the retired user-level profile mirror column is not
        // read at all — the record's ProfileTitle slot is always NULL.
        Assert.Contains("NULL::text             AS ProfileTitle", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("u.profile_title", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminExists_SelfLockoutAndIdentityUseTemplateProfile_NotProfileTitle()
    {
        var identitySql = Read("DapperInternalUserRepository.cs");
        var adminSql = Read("DapperAdminRepository.cs");

        // Bootstrap idempotency check: admin-ness is template-owned.
        Assert.Contains("p.functional_profile = 'Admin'", identitySql, StringComparison.Ordinal);
        Assert.DoesNotContain("u.profile_title = 'Admin'", identitySql, StringComparison.Ordinal);

        // Self-lockout count: template-owned profile, junction-free, FK-based.
        Assert.Contains("JOIN access_templates t ON t.template_id = u.template_id", adminSql, StringComparison.Ordinal);
        Assert.Contains("p.functional_profile = 'Admin'", adminSql, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN internal_user_access_templates", adminSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("u.profile_title = 'Admin'", adminSql, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminUserProjection_DerivesTemplateIds_FromDirectFk_NotJunction()
    {
        var sql = Read("DapperAdminRepository.cs");

        Assert.Contains("ARRAY[u.template_id] AS TemplateIds", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("internal_user_access_templates", sql, StringComparison.OrdinalIgnoreCase);
        // SCHEMA-RAT-03B: the admin projection reads the profile from the
        // template-owned authority table through a join — never from a
        // user-level column.
        Assert.Contains(
            "LEFT JOIN access_template_profiles pt ON pt.template_id = u.template_id",
            sql, StringComparison.Ordinal);
        Assert.Contains("pt.functional_profile AS ProfileTitle", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("u.profile_title", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void UserWrites_AreSingleTemplate_AndUserEditsNeverWriteProfileTitle()
    {
        var adminSql = Read("DapperAdminRepository.cs");

        // No plural template-replacement write remains.
        Assert.DoesNotContain("ReplaceUserAccessTemplatesAsync", adminSql, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM unnest(@TemplateIds", adminSql, StringComparison.Ordinal);

        // The user UPDATE touches display fields only — never profile_title.
        Assert.Contains(
            "UPDATE internal_users\n                SET display_name = @DisplayName",
            adminSql, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateProfileWrites_AreTemplateOwned_AndNeverTouchUsers()
    {
        var adminSql = Read("DapperAdminRepository.cs");

        // SCHEMA-RAT-03B: the profile authority is written on
        // access_template_profiles only, in the template write transaction.
        // The one-way user profile mirror UPDATE is REMOVED — no runtime
        // statement in the admin repository writes the retired mirror column.
        Assert.Contains("INSERT INTO access_template_profiles", adminSql, StringComparison.Ordinal);
        Assert.DoesNotContain("profile_title", adminSql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE internal_users\n                        SET profile_title", adminSql, StringComparison.Ordinal);
    }

    [Fact]
    public void BothRepositories_HaveNoLegacyMirrorReferences()
    {
        // SCHEMA-RAT-03B: neither repository reads or writes the legacy
        // mirror structures (the N27 junction table and the user-level
        // profile mirror column) — writes AND reads are both gone.
        var adminSql = Read("DapperAdminRepository.cs");
        var identitySql = Read("DapperInternalUserRepository.cs");

        Assert.DoesNotContain("profile_title", adminSql, StringComparison.Ordinal);
        Assert.DoesNotContain("profile_title", identitySql, StringComparison.Ordinal);
        Assert.DoesNotContain("internal_user_access_templates", adminSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal_user_access_templates", identitySql, StringComparison.OrdinalIgnoreCase);
        // No INSERT/UPDATE/DELETE targets the junction table anywhere in the
        // runtime repositories.
        Assert.DoesNotContain("INSERT INTO internal_user_access_templates", adminSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO internal_user_access_templates", identitySql, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string fileName)
    {
        var root = ResolveRepositoryRoot();
        var path = Path.Combine(root, "src", "BA.Dmo.Infrastructure", "Access", fileName);
        if (!File.Exists(path))
            path = Path.Combine(root, "src", "BA.Dmo.Infrastructure", "Identity", fileName);
        return File.ReadAllText(path);
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