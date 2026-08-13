using System.Collections.Immutable;
using Vector.Data.Export;
using Vector.Domain;
using Vector.Engine;

namespace Vector.Data.Tests;

public class MarkdownExporterTests
{
    private const string FixedDate = "2026-08-13";

    private static (RuleSet Rules, Scenario Scenario, DecisionOutcome Outcome) BuildFixture(string scenarioId = "scn.policy-assistant")
    {
        var (rules, scenarios) = KnowledgeLoader.Parse(ContentFile.ReadAllText());
        var scenario = scenarios.Single(s => s.Id == scenarioId);
        var engine = new DecisionEngine();
        var outcome = engine.Evaluate(scenario, rules);
        return (rules, scenario, outcome);
    }

    [Fact]
    public void Export_WithEmptyRationale_ContainsAllBracketPromptsUnfilled()
    {
        var (rules, scenario, outcome) = BuildFixture();
        var input = new ExportInput(scenario, scenario.Profile, rules, outcome, RationaleMarkdown: "", FixedDate);

        var markdown = MarkdownExporter.Export(input);

        Assert.Contains("[[ Name the architecture pattern you are recommending. ]]", markdown, StringComparison.Ordinal);
        Assert.Contains("[[ Explain why this pattern fits the scenario's constraints. ]]", markdown, StringComparison.Ordinal);
        Assert.Contains("[[ Explain why the other patterns were not chosen. ]]", markdown, StringComparison.Ordinal);
        Assert.Contains("[[ Describe what would need to change for this decision to be revisited. ]]", markdown, StringComparison.Ordinal);
        Assert.Contains("[[ Describe the broader consequences of adopting this pattern. ]]", markdown, StringComparison.Ordinal);
        Assert.Contains("[[ List any unresolved questions or follow-ups before this decision is finalized. ]]", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_WithNonEmptyRationale_AppendsItWithoutReplacingBracketPrompts()
    {
        var (rules, scenario, outcome) = BuildFixture();
        const string rationale = "We chose retrieval-augmented generation because policy answers must cite the current document.";
        var input = new ExportInput(scenario, scenario.Profile, rules, outcome, rationale, FixedDate);

        var markdown = MarkdownExporter.Export(input);

        Assert.Contains(rationale, markdown, StringComparison.Ordinal);
        // The engine-authored rationale text must NOT have silently replaced the literal prompts.
        Assert.Contains("[[ Name the architecture pattern you are recommending. ]]", markdown, StringComparison.Ordinal);
        Assert.Contains("[[ Explain why this pattern fits the scenario's constraints. ]]", markdown, StringComparison.Ordinal);
        Assert.Contains("[[ Explain why the other patterns were not chosen. ]]", markdown, StringComparison.Ordinal);
        Assert.Contains("[[ Describe what would need to change for this decision to be revisited. ]]", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ContainsVersionsAndDigestAndDate()
    {
        var (rules, scenario, outcome) = BuildFixture();
        var input = new ExportInput(scenario, scenario.Profile, rules, outcome, "", FixedDate);

        var markdown = MarkdownExporter.Export(input);

        Assert.Contains(FixedDate, markdown, StringComparison.Ordinal);
        Assert.Contains(EngineConstants.EngineVersion, markdown, StringComparison.Ordinal);
        Assert.Contains(rules.RulesVersion, markdown, StringComparison.Ordinal);

        var shortDigest = outcome.ConfigDigest["Sha256:".Length..][..12];
        Assert.Contains(shortDigest, markdown, StringComparison.Ordinal);

        Assert.Contains("Status: Draft — decision support only", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_NeverClaimsCorrectnessOrBestness()
    {
        var (rules, scenario, outcome) = BuildFixture();
        var input = new ExportInput(scenario, scenario.Profile, rules, outcome, "", FixedDate);

        var markdown = MarkdownExporter.Export(input);

        Assert.DoesNotContain("best architecture", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correct architecture", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Leading under these constraints", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_HasStableStructuralSectionOrder()
    {
        var (rules, scenario, outcome) = BuildFixture();
        var input = new ExportInput(scenario, scenario.Profile, rules, outcome, "", FixedDate);

        var markdown = MarkdownExporter.Export(input);

        string[] expectedHeadingsInOrder =
        [
            "## Context & Scenario",
            "## Constraints & Weights",
            "## Patterns Considered",
            "## Hard Conflicts",
            "## Scoring Trace",
            "## Shortlist / Leading Option",
            "## Rationale (Author-Written)",
            "## Sensitivity Notes",
            "## Risks & Mitigations",
            "## Consequences",
            "## Unresolved Questions",
        ];

        var lastIndex = -1;
        foreach (var heading in expectedHeadingsInOrder)
        {
            var index = markdown.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(index > lastIndex, $"Expected heading '{heading}' to appear after index {lastIndex}, but found it at {index}.");
            lastIndex = index;
        }
    }

    [Fact]
    public void Export_ForNearTieScenario_NotesCoLeadingPatterns()
    {
        var (rules, scenario, outcome) = BuildFixture("scn.supervised-research");
        Assert.True(outcome.HasNearTie);

        var input = new ExportInput(scenario, scenario.Profile, rules, outcome, "", FixedDate);
        var markdown = MarkdownExporter.Export(input);

        Assert.Contains("effectively co-leading", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_TwiceWithIdenticalInput_IsByteIdentical()
    {
        var (rules, scenario, outcome) = BuildFixture();
        var input = new ExportInput(scenario, scenario.Profile, rules, outcome, "", FixedDate);

        var first = MarkdownExporter.Export(input);
        var second = MarkdownExporter.Export(input);

        Assert.Equal(first, second, StringComparer.Ordinal);
    }

    [Fact]
    public void Export_ChangingOneWeightTier_ChangesEmbeddedDigest()
    {
        var (rules, scenario, outcome) = BuildFixture();
        var input = new ExportInput(scenario, scenario.Profile, rules, outcome, "", FixedDate);
        var original = MarkdownExporter.Export(input);

        var mutatedSettings = scenario.Profile.Settings
            .Select(s => s.Dimension == ConstraintDimension.DataSensitivity ? s with { WeightTier = s.WeightTier == 3 ? 1 : 3 } : s)
            .ToImmutableArray();

        var mutatedProfile = new ConstraintProfile(mutatedSettings);
        var mutatedScenario = scenario with { Profile = mutatedProfile };
        var engine = new DecisionEngine();
        var mutatedOutcome = engine.Evaluate(mutatedScenario, rules);
        var mutatedInput = new ExportInput(mutatedScenario, mutatedProfile, rules, mutatedOutcome, "", FixedDate);
        var mutated = MarkdownExporter.Export(mutatedInput);

        Assert.NotEqual(outcome.ConfigDigest, mutatedOutcome.ConfigDigest);
        Assert.NotEqual(original, mutated);
    }

    [Fact]
    public void Export_ForCustomProfile_WithNoScenario_UsesGenericTitle()
    {
        var (rules, scenario, _) = BuildFixture();
        var engine = new DecisionEngine();
        var outcome = engine.Evaluate(scenario, rules);
        var input = new ExportInput(null, scenario.Profile, rules, outcome, "", FixedDate);

        var markdown = MarkdownExporter.Export(input);

        Assert.Contains("# Architecture Decision Record: Custom constraint profile", markdown, StringComparison.Ordinal);
        Assert.Contains("Custom constraint profile (no predefined scenario).", markdown, StringComparison.Ordinal);
    }
}
