using BA.Dmo.Domain.Modules.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// U-13 verification occurrence generation tests (modules/05 §7, GLM-JOB-07):
/// one occurrence per rule, pendente with manual_job_on source, frequency
/// semantics, and no occurrences for empty/invalid rules.
/// </summary>
public class JobOnVerificationGeneratorTests
{
    private static readonly Guid ComponentId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 17, 18, 0, 0, DateTimeKind.Utc);

    private static VerificationRule Rule(string text, VerificationFrequency frequency) =>
        new(Guid.NewGuid(), text, frequency);

    [Fact]
    public void Generate_OneRule_YieldsOnePendenteOccurrence()
    {
        var rules = new[] { Rule("Verificar aperto", VerificationFrequency.OncePerLot) };

        var occurrences = JobOnVerificationGenerator.Generate(ComponentId, rules, Now);

        var occurrence = Assert.Single(occurrences);
        Assert.Equal(ComponentId, occurrence.JobOnComponentId);
        Assert.Equal("pendente", occurrence.Status);
        Assert.Equal("manual_job_on", occurrence.CompletionSource);
        Assert.Equal("Verificar aperto", occurrence.RuleTextSnapshot);
        Assert.Null(occurrence.CompletedBy);
        Assert.Null(occurrence.CompletedAtUtc);
    }

    [Fact]
    public void Generate_MultipleRules_YieldsOnePerRule()
    {
        var rules = new[]
        {
            Rule("Regra A", VerificationFrequency.OncePerLot),
            Rule("Regra B", VerificationFrequency.PerProduction),
            Rule("Regra C", VerificationFrequency.OncePerLot)
        };

        var occurrences = JobOnVerificationGenerator.Generate(ComponentId, rules, Now);

        Assert.Equal(3, occurrences.Count);
        Assert.All(occurrences, o => Assert.Equal(ComponentId, o.JobOnComponentId));
    }

    [Fact]
    public void Generate_EmptyRules_YieldsNone()
    {
        var occurrences = JobOnVerificationGenerator.Generate(
            ComponentId, Array.Empty<VerificationRule>(), Now);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void Generate_NullRules_YieldsNone()
    {
        var occurrences = JobOnVerificationGenerator.Generate(ComponentId, null!, Now);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void Generate_EmptyRuleId_IsSkipped()
    {
        var rules = new[] { new VerificationRule(Guid.Empty, "Inválida", VerificationFrequency.OncePerLot) };

        var occurrences = JobOnVerificationGenerator.Generate(ComponentId, rules, Now);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void Generate_RecordsCreationTimestamp()
    {
        var rules = new[] { Rule("Regra", VerificationFrequency.PerProduction) };

        var occurrences = JobOnVerificationGenerator.Generate(ComponentId, rules, Now);

        Assert.Equal(Now, Assert.Single(occurrences).CreatedAtUtc);
        Assert.Equal(Now, Assert.Single(occurrences).UpdatedAtUtc);
    }
}
