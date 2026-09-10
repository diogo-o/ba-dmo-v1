using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL round-trip proofs for the Tampões Opções write paths
/// (<see cref="DapperTampaoRepository.CreateFieldDefAsync"/> /
/// <see cref="DapperTampaoRepository.CreateFieldValueAsync"/>).
/// BA_DMO_TEST_DATABASE must identify an isolated test database. Field names are
/// unique per run (uq_tampao_field_defs.field_name); no existing row is selected
/// or mutated.
/// </summary>
public sealed class TampaoFieldDefPostgresTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task CreateFieldDefAndValue_RoundTrip_ReadbackMatches()
    {
        if (SkipIfNoDatabase()) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repository = new DapperTampaoRepository(CreateFactory("tampoes-" + suffix));
        var now = DateTimeOffset.UtcNow;

        var field = new TampaoFieldDef
        {
            TampaoFieldDefId = Guid.NewGuid(),
            FieldName = "SMOKE " + suffix,
            Unit = "mm",
            PrecisionDigits = 1,
            DisplayOrder = 10,
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var fieldId = await repository.CreateFieldDefAsync(field);
        Assert.Equal(field.TampaoFieldDefId, fieldId);

        var value = new TampaoFieldValue
        {
            TampaoFieldValueId = Guid.NewGuid(),
            TampaoFieldDefId = fieldId,
            ValueNumeric = 40m,
            ValueLabel = "40 mm",
            DisplayOrder = 10,
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var valueId = await repository.CreateFieldValueAsync(value);
        Assert.Equal(value.TampaoFieldValueId, valueId);

        // Readback through the same repository surface the UI uses.
        var defs = await repository.ListFieldDefsAsync(onlyActive: true);
        var readDef = Assert.Single(defs, f => f.TampaoFieldDefId == fieldId);
        Assert.Equal("SMOKE " + suffix, readDef.FieldName);
        Assert.Equal("mm", readDef.Unit);
        Assert.Equal(1, readDef.PrecisionDigits);
        Assert.Equal(10, readDef.DisplayOrder);
        Assert.True(readDef.Active);

        var values = await repository.ListFieldValuesAsync(fieldId, onlyActive: true);
        var readValue = Assert.Single(values, v => v.TampaoFieldValueId == valueId);
        Assert.Equal(40m, readValue.ValueNumeric);
        Assert.Equal("40 mm", readValue.ValueLabel);
        Assert.Equal(10, readValue.DisplayOrder);
        Assert.True(readValue.Active);
    }

    [Fact]
    public async Task CreateFieldDef_DuplicateName_SurfacesUniqueViolation()
    {
        if (SkipIfNoDatabase()) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repository = new DapperTampaoRepository(CreateFactory("tampoes-" + suffix));
        var now = DateTimeOffset.UtcNow;
        var name = "SMOKE-DUP " + suffix;

        await repository.CreateFieldDefAsync(new TampaoFieldDef
        {
            TampaoFieldDefId = Guid.NewGuid(),
            FieldName = name,
            Unit = "mm",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        // uq_tampao_field_defs.field_name is the canonical duplicate guard; it must
        // surface as TampaoFieldDuplicateException (never a raw 23505) and never
        // silently overwrite the first row.
        var duplicate = await Record.ExceptionAsync(() => repository.CreateFieldDefAsync(new TampaoFieldDef
        {
            TampaoFieldDefId = Guid.NewGuid(),
            FieldName = name,
            Unit = "mm",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }));

        var dup = Assert.IsType<TampaoFieldDuplicateException>(duplicate);
        Assert.Contains(name, dup.Message);
        Assert.Single(await repository.ListFieldDefsAsync(onlyActive: false),
            f => f.FieldName == name);
    }

    [Fact]
    public async Task CreateFieldValue_DuplicateNormalizedValue_SurfacesFieldDuplicate()
    {
        if (SkipIfNoDatabase()) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var repository = new DapperTampaoRepository(CreateFactory("tampoes-" + suffix));
        var now = DateTimeOffset.UtcNow;
        var fieldId = await repository.CreateFieldDefAsync(new TampaoFieldDef
        {
            TampaoFieldDefId = Guid.NewGuid(),
            FieldName = "SMOKE-VAL " + suffix,
            Unit = "mm",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await repository.CreateFieldValueAsync(new TampaoFieldValue
        {
            TampaoFieldValueId = Guid.NewGuid(),
            TampaoFieldDefId = fieldId,
            ValueNumeric = 40m,
            ValueLabel = "40 mm",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        // uq_tampao_field_values (tampao_field_def_id, value_numeric): the same
        // normalized value for the same field must surface as the domain
        // duplicate exception, never a raw 23505.
        var duplicate = await Record.ExceptionAsync(() => repository.CreateFieldValueAsync(new TampaoFieldValue
        {
            TampaoFieldValueId = Guid.NewGuid(),
            TampaoFieldDefId = fieldId,
            ValueNumeric = 40m,
            ValueLabel = "40 mm again",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }));

        Assert.IsType<TampaoFieldDuplicateException>(duplicate);
        Assert.Single(await repository.ListFieldValuesAsync(fieldId, onlyActive: false));
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] TampaoFieldDefPostgresTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL field-def/value round-trip assertions were not executed.");
        return true;
    }

    private static DbConnectionFactory CreateFactory(string applicationName)
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString!)
        {
            ApplicationName = applicationName
        };
        return new DbConnectionFactory(builder.ConnectionString);
    }
}