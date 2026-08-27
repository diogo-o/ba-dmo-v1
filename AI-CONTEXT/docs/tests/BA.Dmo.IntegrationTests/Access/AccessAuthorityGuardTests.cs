namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// SCHEMA-RAT-03A access-authority source guards (D-1/D-2).
///
/// These are SOURCE guards, not executed PostgreSQL tests: they read the
/// repository's C# source to pin the authority contract of the identity and
/// admin persistence SQL (which lives in private consts not reachable at
/// runtime). Executed PostgreSQL behaviour is covered by the env-guarded
/// <c>RemediationGuardTests.N32_*</c> probes (BA_DMO_TEST_DATABASE) and by the
/// ADO.NET-double projections (<c>DapperAdminRepositoryProjectionTests</c>).
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
        // (The only junction SQL left in the file is the bootstrap one-way
        // mirror insert, which lives in InsertUserTemplateSql — so we assert
        // the absence of any JOIN on the junction.)
        Assert.DoesNotContain("JOIN internal_user_access_templates", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LEFT JOIN internal_user_access_templates", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdentityResolutionSql_ReadsFunctionalProfileFromTemplateProfileTable()
    {
        var sql = Read("DapperInternalUserRepository.cs");

        Assert.Contains("p.functional_profile   AS FunctionalProfile", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT u.profile_title AS FunctionalProfile", sql, StringComparison.Ordinal);
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
        Assert.DoesNotContain("FROM internal_user_access_templates ut", sql, StringComparison.Ordinal);
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
    public void TemplateProfileWrites_AreTemplateOwned_AndMirrorIsOneWay()
    {
        var adminSql = Read("DapperAdminRepository.cs");

        // Profile authority is written on access_template_profiles only, in the
        // template write transaction; the mirror is a one-way UPDATE of
        // internal_users.profile_title (which never feeds authorization).
        Assert.Contains("INSERT INTO access_template_profiles", adminSql, StringComparison.Ordinal);
        Assert.Contains("UPDATE internal_users\n                        SET profile_title = @FunctionalProfile", adminSql, StringComparison.Ordinal);
        // The mirror update must not rewrite the user concurrency version.
        Assert.DoesNotContain(
            "SET profile_title = @FunctionalProfile,\n                            updated_at_utc",
            adminSql, StringComparison.Ordinal);
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