using Vector.Domain;
using Vector.Engine;

namespace Vector.Data.Tests;

/// <summary>
/// Loads the real content JSON, runs the real <see cref="DecisionEngine"/> against each authored
/// scenario, and asserts the pre-verified expected rankings. These numbers were hand-derived from the
/// capability matrix and demand curves (see the arithmetic notes below and in the task's authoring
/// spec) before the JSON was written; if any assertion here fails, the JSON's numbers are wrong -
/// fix the JSON, not this test.
/// </summary>
public class CalibrationTests
{
    private static (RuleSet Rules, IReadOnlyList<Scenario> Scenarios) LoadContent() =>
        KnowledgeLoader.Parse(ContentFile.ReadAllText());

    private static Scenario FindScenario(IReadOnlyList<Scenario> scenarios, string id) =>
        scenarios.Single(s => s.Id == id);

    /// <summary>
    /// scn.policy-assistant: hand-derived tier-weighted shortfall (weightTier * shortfall, summed across
    /// dimensions) is RAG=4, Workflow=9, Direct=Agent=11. The real weighted-basis-point score (verified
    /// independently by script) is RAG=928,575, Workflow=839,275, Direct=Agent=803,575 (tied for last).
    /// RAG's margin over the runner-up (Workflow) is 89,300 scaled units, far above the near-tie
    /// threshold of 300 bp of 1,000,000 = 30,000, so RAG leads decisively.
    /// </summary>
    [Fact]
    public void PolicyAssistant_RagLeadsDecisively()
    {
        var (rules, scenarios) = LoadContent();
        var scenario = FindScenario(scenarios, "scn.policy-assistant");

        var engine = new DecisionEngine();
        var outcome = engine.Evaluate(scenario, rules);

        var top = outcome.Rankings.Single(r => r.Rank == 1);
        Assert.Equal(PatternId.RetrievalAugmentedGeneration, top.Pattern);
        Assert.True(top.IsEligible);
        Assert.Equal(928_575, top.ScoreScaled);
        Assert.False(outcome.HasNearTie);
    }

    /// <summary>
    /// scn.structured-extraction: DirectStructuredCall's capabilities exactly meet or exceed every
    /// demand on this profile (zero shortfall on all 8 dimensions), so it scores the maximum possible
    /// 1,000,000 regardless of weight distribution. ToolUsingAgent has the largest shortfalls
    /// (DataSensitivity, LatencyTarget, CostPressure, DeterminismReproducibility all unmet) and is
    /// verified last at rank 4 with score 673,050.
    /// </summary>
    [Fact]
    public void StructuredExtraction_DirectIsPerfectFit_AgentIsLast()
    {
        var (rules, scenarios) = LoadContent();
        var scenario = FindScenario(scenarios, "scn.structured-extraction");

        var engine = new DecisionEngine();
        var outcome = engine.Evaluate(scenario, rules);

        var top = outcome.Rankings.Single(r => r.Rank == 1);
        Assert.Equal(PatternId.DirectStructuredCall, top.Pattern);
        Assert.True(top.IsEligible);
        Assert.Equal(1_000_000, top.ScoreScaled);

        var last = outcome.Rankings.Single(r => r.Rank == 4);
        Assert.Equal(PatternId.ToolUsingAgent, last.Pattern);
    }

    /// <summary>
    /// scn.supervised-research: hand-derived tier-weighted shortfall gives RAG=10 and Agent=10 (an exact
    /// tie) with Workflow=13 and Direct=24 clearly behind. The REAL weighted-basis-point score does not
    /// tie exactly, because WeightSet.FromTiers' Hamilton/largest-remainder apportionment distributes
    /// leftover basis points one at a time and happens to give the Tools dimension 2143 bp instead of an
    /// exact 2142.86: RAG scores 821,425 and ToolUsingAgent scores 821,450 - a 25-scaled-unit difference
    /// (0.0025% of the 1,000,000 scale), independently verified by script against the capability matrix
    /// and demand curves. That is a legitimate rounding artifact of integer basis-point apportionment,
    /// not a calibration bug: it is two orders of magnitude below the 30,000-unit (300 bp) near-tie
    /// threshold, so HasNearTie still fires, and it does not affect which two patterns lead. No JSON
    /// content was changed to accommodate this; the set {RAG, Agent} as the top two, both eligible, is
    /// exactly as specified, and Workflow (767,850) is nowhere near close enough to join them.
    /// </summary>
    [Fact]
    public void SupervisedResearch_RagAndAgentAreCoLeadingNearTie_WorkflowNotInTopTwo_DirectIsLast()
    {
        var (rules, scenarios) = LoadContent();
        var scenario = FindScenario(scenarios, "scn.supervised-research");

        var engine = new DecisionEngine();
        var outcome = engine.Evaluate(scenario, rules);

        var topTwo = outcome.Rankings.Where(r => r.Rank is 1 or 2).Select(r => r.Pattern).ToHashSet();
        Assert.Equal(
            new HashSet<PatternId> { PatternId.RetrievalAugmentedGeneration, PatternId.ToolUsingAgent },
            topTwo);

        foreach (var rank in new[] { 1, 2 })
        {
            var result = outcome.Rankings.Single(r => r.Rank == rank);
            Assert.True(result.IsEligible);
        }

        Assert.True(outcome.HasNearTie);

        // Deterministic, independently-verified exact order and scores (see class doc comment): the
        // real bp-apportionment score gives ToolUsingAgent a razor-thin edge over RAG.
        var rank1 = outcome.Rankings.Single(r => r.Rank == 1);
        var rank2 = outcome.Rankings.Single(r => r.Rank == 2);
        Assert.Equal(PatternId.ToolUsingAgent, rank1.Pattern);
        Assert.Equal(821_450, rank1.ScoreScaled);
        Assert.Equal(PatternId.RetrievalAugmentedGeneration, rank2.Pattern);
        Assert.Equal(821_425, rank2.ScoreScaled);
        Assert.Equal(25, rank1.ScoreScaled - rank2.ScoreScaled);

        var last = outcome.Rankings.Single(r => r.Rank == 4);
        Assert.Equal(PatternId.DirectStructuredCall, last.Pattern);

        var workflow = outcome.Rankings.Single(r => r.Pattern == PatternId.DeterministicWorkflow);
        Assert.Equal(3, workflow.Rank);
        Assert.Equal(767_850, workflow.ScoreScaled);
    }

    [Theory]
    [InlineData("scn.policy-assistant")]
    [InlineData("scn.structured-extraction")]
    [InlineData("scn.supervised-research")]
    public void Evaluate_SameScenarioTwice_YieldsIdenticalConfigDigest(string scenarioId)
    {
        var (rules, scenarios) = LoadContent();
        var scenario = FindScenario(scenarios, scenarioId);
        var engine = new DecisionEngine();

        var first = engine.Evaluate(scenario, rules);
        var second = engine.Evaluate(scenario, rules);

        Assert.Equal(first.ConfigDigest, second.ConfigDigest);
        Assert.StartsWith("Sha256:", first.ConfigDigest, StringComparison.Ordinal);
    }
}
