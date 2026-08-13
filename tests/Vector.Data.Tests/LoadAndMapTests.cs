using System.Text.Json.Nodes;
using Vector.Domain;

namespace Vector.Data.Tests;

/// <summary>Loads and maps the real content JSON, and verifies malformed fixtures fail loudly with <see cref="DataMappingException"/>.</summary>
public class LoadAndMapTests
{
    [Fact]
    public void Parse_RealContentFile_ProducesExpectedShape()
    {
        var (rules, scenarios) = KnowledgeLoader.Parse(ContentFile.ReadAllText());

        Assert.Equal(8, rules.Constraints.Length);
        Assert.Equal(4, rules.Patterns.Length);
        Assert.Equal(3, scenarios.Count);

        foreach (var constraint in rules.Constraints)
        {
            Assert.Equal(constraint.Levels.Length, constraint.DemandCurve.Length);
            foreach (var demand in constraint.DemandCurve)
            {
                Assert.InRange(demand, 0, 4);
            }
        }

        foreach (var pattern in rules.Patterns)
        {
            Assert.Equal(8, pattern.Capabilities.Length);
            Assert.Equal(8, pattern.Rationales.Length);
            foreach (var capability in pattern.Capabilities)
            {
                Assert.InRange(capability, 0, 4);
            }
        }

        // Every dimension 1..8 must be represented exactly once.
        var dimensions = rules.Constraints.Select(c => c.Dimension).OrderBy(d => (int)d).ToArray();
        Assert.Equal(Enum.GetValues<ConstraintDimension>().OrderBy(d => (int)d), dimensions);

        // Every PatternId must be represented exactly once.
        var patternIds = rules.Patterns.Select(p => p.Id).OrderBy(id => (int)id).ToArray();
        Assert.Equal(Enum.GetValues<PatternId>().OrderBy(id => (int)id), patternIds);

        // All risk and advisory mitigation references resolved (ToRuleSet would have thrown otherwise);
        // sanity-check that at least some risks/advisories actually carry mitigations, so this isn't vacuous.
        var allRiskMitigations = rules.Patterns.SelectMany(p => p.Risks).SelectMany(r => r.Mitigations).ToList();
        Assert.NotEmpty(allRiskMitigations);

        var allAdvisoryMitigations = rules.Advisories.SelectMany(a => a.Mitigations).ToList();
        Assert.NotEmpty(allAdvisoryMitigations);

        // Every scenario's profile must cover all 8 dimensions exactly once.
        foreach (var scenario in scenarios)
        {
            Assert.Equal(8, scenario.Profile.Settings.Length);
        }
    }

    [Fact]
    public void Parse_RealContentFile_RulesContentHashIsStable()
    {
        var (rules1, _) = KnowledgeLoader.Parse(ContentFile.ReadAllText());
        var (rules2, _) = KnowledgeLoader.Parse(ContentFile.ReadAllText());

        Assert.Equal(rules1.RulesContentHash, rules2.RulesContentHash);
        Assert.StartsWith("Sha256:", rules1.RulesContentHash, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnknownDimensionEnumValue_ThrowsDataMappingExceptionNamingTheValue()
    {
        var json = Mutate(root =>
        {
            var constraint = (JsonObject)root["constraints"]![0]!;
            constraint["dimension"] = "NotADimension";
        });

        var ex = Assert.Throws<DataMappingException>(() => KnowledgeLoader.Parse(json));
        Assert.Contains("NotADimension", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnknownMitigationIdReference_ThrowsDataMappingExceptionNamingTheId()
    {
        var json = Mutate(root =>
        {
            var pattern = (JsonObject)root["patterns"]![0]!;
            var risk = (JsonObject)pattern["risks"]![0]!;
            var mitigationIds = (JsonArray)risk["mitigationIds"]!;
            mitigationIds[0] = "totally-bogus-mitigation-id";
        });

        var ex = Assert.Throws<DataMappingException>(() => KnowledgeLoader.Parse(json));
        Assert.Contains("totally-bogus-mitigation-id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WrongCapabilitiesArrayLength_ThrowsDataMappingException()
    {
        var json = Mutate(root =>
        {
            var pattern = (JsonObject)root["patterns"]![0]!;
            var capabilities = (JsonArray)pattern["capabilities"]!;
            capabilities.RemoveAt(capabilities.Count - 1);
        });

        var ex = Assert.Throws<DataMappingException>(() => KnowledgeLoader.Parse(json));
        Assert.Contains("capabilities", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("7", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DemandCurveLevelsLengthMismatch_ThrowsDataMappingException()
    {
        var json = Mutate(root =>
        {
            var constraint = (JsonObject)root["constraints"]![0]!;
            var demandCurve = (JsonArray)constraint["demandCurve"]!;
            demandCurve.RemoveAt(demandCurve.Count - 1);
        });

        var ex = Assert.Throws<DataMappingException>(() => KnowledgeLoader.Parse(json));
        Assert.Contains("demandCurve", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Parses the real content file into a mutable JSON tree, applies one deliberate corruption, and re-serializes.</summary>
    private static string Mutate(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(ContentFile.ReadAllText())!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }
}
