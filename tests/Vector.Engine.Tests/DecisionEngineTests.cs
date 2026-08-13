using System.Collections.Immutable;
using Vector.Domain;
using Vector.Engine;

namespace Vector.Engine.Tests;

public class DecisionEngineTests
{
    private static readonly IDecisionEngine Engine = new DecisionEngine();

    /// <summary>Invariant 1: evaluating twice yields an identical digest, deep-equal outcome, and identical rank order.</summary>
    [Fact]
    public void Evaluate_CalledTwice_IsFullyDeterministic()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("s1", "Determinism Check", TestRuleSets.BalancedProfile());

        var outcome1 = Engine.Evaluate(scenario, rules);
        var outcome2 = Engine.Evaluate(scenario, rules);

        Assert.Equal(outcome1.ConfigDigest, outcome2.ConfigDigest);
        Assert.Equal(outcome1, outcome2);
        Assert.Equal(outcome1.Rankings.Select(r => r.Pattern), outcome2.Rankings.Select(r => r.Pattern));
    }

    /// <summary>Invariant 3: a pattern that violates a hard constraint is vetoed and ranks below every eligible pattern, even with the highest raw score.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void HardVeto_Dominates_AcrossWeightTiers(int hardWeightTier)
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("veto", "Hard Veto", TestRuleSets.HardVetoProfile(hardWeightTier));

        var outcome = Engine.Evaluate(scenario, rules);

        var vetoed = outcome.Rankings.Single(r => r.Pattern == PatternId.ToolUsingAgent);
        Assert.False(vetoed.IsEligible);
        Assert.Equal(HardStatus.Vetoed, vetoed.HardStatus);

        var eligible = outcome.Rankings.Where(r => r.IsEligible).ToList();
        Assert.NotEmpty(eligible);

        foreach (var other in eligible)
        {
            Assert.True(vetoed.Rank > other.Rank, $"Vetoed pattern rank {vetoed.Rank} should be worse than eligible {other.Pattern} rank {other.Rank}.");
        }

        // The whole point of this fixture: even though it is vetoed, its raw score is the highest of all four.
        Assert.True(outcome.Rankings.All(r => vetoed.ScoreScaled >= r.ScoreScaled), "Vetoed pattern's raw ScoreScaled should be the maximum across all patterns.");
    }

    /// <summary>Invariant 4: a hard constraint with WeightTier 0 still vetoes.</summary>
    [Fact]
    public void HardVeto_WithZeroWeight_StillVetoes()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("veto0", "Zero Weight Hard Veto", TestRuleSets.HardVetoProfile(hardWeightTier: 0));

        var outcome = Engine.Evaluate(scenario, rules);

        var vetoed = outcome.Rankings.Single(r => r.Pattern == PatternId.ToolUsingAgent);
        Assert.Equal(HardStatus.Vetoed, vetoed.HardStatus);
        Assert.False(vetoed.IsEligible);
    }

    /// <summary>Invariant 5: every hard-violating (pattern, dimension) pair appears in HardConflicts with a non-empty reason.</summary>
    [Fact]
    public void HardConflicts_AreEnumeratedWithNonEmptyReasons()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("conflicts", "Conflicts", TestRuleSets.HardVetoProfile(hardWeightTier: 2));

        var outcome = Engine.Evaluate(scenario, rules);

        var vetoed = outcome.Rankings.Single(r => r.Pattern == PatternId.ToolUsingAgent);
        Assert.NotEmpty(vetoed.HardConflicts);
        foreach (var conflict in vetoed.HardConflicts)
        {
            Assert.Equal(ConstraintDimension.DeterminismReproducibility, conflict.Dimension);
            Assert.False(string.IsNullOrWhiteSpace(conflict.Reason));
        }

        // Every other pattern is compatible on the hard dimension at this profile, so no other conflicts exist.
        foreach (var result in outcome.Rankings.Where(r => r.Pattern != PatternId.ToolUsingAgent))
        {
            Assert.Empty(result.HardConflicts);
        }
    }

    /// <summary>Invariant 6: for every pattern, 8 contributions are recorded, their weighted sum equals ScoreScaled exactly, and every rationale is non-empty.</summary>
    [Fact]
    public void AccountingIdentity_HoldsForEveryPattern()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("accounting", "Accounting", TestRuleSets.BalancedProfile());

        var outcome = Engine.Evaluate(scenario, rules);

        foreach (var result in outcome.Rankings)
        {
            Assert.Equal(8, result.Contributions.Length);

            var weightedSum = result.Contributions.Sum(c => c.WeightedContributionScaled);
            Assert.Equal(result.ScoreScaled, weightedSum);

            var penaltySum = result.Contributions.Sum(c => c.ShortfallPenaltyScaled);
            Assert.Equal(result.ScoreScaled, EngineConstants.Scale - penaltySum);

            foreach (var contribution in result.Contributions)
            {
                Assert.False(string.IsNullOrWhiteSpace(contribution.Rationale));
            }
        }
    }

    /// <summary>Invariant 7: constructing the profile with settings in a different order yields an identical outcome and digest.</summary>
    [Fact]
    public void OrderInvariance_ShuffledSettings_ProduceIdenticalOutcome()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var canonicalProfile = TestRuleSets.BalancedProfile();

        var reversedSettings = canonicalProfile.Settings.Reverse().ToImmutableArray();
        var reversedProfile = new ConstraintProfile(reversedSettings);

        var scenarioA = TestRuleSets.BuildScenario("order-a", "Canonical order", canonicalProfile);
        var scenarioB = TestRuleSets.BuildScenario("order-b", "Reversed order", reversedProfile);

        var outcomeA = Engine.Evaluate(scenarioA, rules);
        var outcomeB = Engine.Evaluate(scenarioB, rules);

        Assert.Equal(outcomeA.ConfigDigest, outcomeB.ConfigDigest);
        Assert.Equal(outcomeA, outcomeB);
    }

    /// <summary>Invariant 9: FitScaled stays within [0, Scale], hits the documented shortfall extremes, and equal-shortfall dimensions contribute equally.</summary>
    [Fact]
    public void Shortfall_HitsBoundsAndEqualContributionCorrectly()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("boundary", "Boundary", TestRuleSets.BoundaryProfile());

        var outcome = Engine.Evaluate(scenario, rules);

        foreach (var result in outcome.Rankings)
        {
            foreach (var contribution in result.Contributions)
            {
                Assert.InRange(contribution.FitScaled, 0, EngineConstants.Scale);
                Assert.InRange(contribution.Shortfall, 0, 4);
            }
        }

        var direct = outcome.Rankings.Single(r => r.Pattern == PatternId.DirectStructuredCall);
        var dataSensitivity = direct.Contributions.Single(c => c.Dimension == ConstraintDimension.DataSensitivity);
        Assert.Equal(4, dataSensitivity.RawFit);
        Assert.Equal(0, dataSensitivity.Shortfall);
        Assert.Equal(EngineConstants.Scale, dataSensitivity.FitScaled);

        var knowledgeFreshness = direct.Contributions.Single(c => c.Dimension == ConstraintDimension.KnowledgeFreshness);
        Assert.Equal(-4, knowledgeFreshness.RawFit);
        Assert.Equal(4, knowledgeFreshness.Shortfall);
        Assert.Equal(0, knowledgeFreshness.FitScaled);

        // HumanReview: capability 2 for all four patterns, demand 2 -> shortfall 0 for everyone, contributing equally.
        var humanReviewTraces = outcome.Rankings
            .Select(r => r.Contributions.Single(c => c.Dimension == ConstraintDimension.HumanReview))
            .ToList();

        Assert.All(humanReviewTraces, t => Assert.Equal(0, t.Shortfall));
        Assert.All(humanReviewTraces, t => Assert.Equal(EngineConstants.Scale, t.FitScaled));
        Assert.True(humanReviewTraces.Select(t => t.WeightedContributionScaled).Distinct().Count() == 1, "Equal shortfall and equal weight must contribute equally.");
    }

    /// <summary>Invariant (new): a pattern that fully meets every demand scores exactly Scale, regardless of weight distribution.</summary>
    [Fact]
    public void ScoreScaled_WhenAllDemandsAreMet_EqualsScale()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("meets-all", "Meets All Demands", TestRuleSets.MeetsAllDemandsProfile());

        var outcome = Engine.Evaluate(scenario, rules);

        var toolUsingAgent = outcome.Rankings.Single(r => r.Pattern == PatternId.ToolUsingAgent);
        Assert.All(toolUsingAgent.Contributions, c => Assert.Equal(0, c.Shortfall));
        Assert.Equal(EngineConstants.Scale, toolUsingAgent.ScoreScaled);
    }

    /// <summary>Invariant (new): a pattern short by 4 on the single fully-weighted dimension scores exactly 0.</summary>
    [Fact]
    public void ScoreScaled_WhenFullyShortOnFullWeightDimension_EqualsZero()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("full-shortfall", "Full Shortfall", TestRuleSets.FullShortfallProfile());

        var outcome = Engine.Evaluate(scenario, rules);

        var direct = outcome.Rankings.Single(r => r.Pattern == PatternId.DirectStructuredCall);
        var knowledgeFreshness = direct.Contributions.Single(c => c.Dimension == ConstraintDimension.KnowledgeFreshness);
        Assert.Equal(4, knowledgeFreshness.Shortfall);
        Assert.Equal(0L, direct.ScoreScaled);
    }

    /// <summary>Invariant (new): a dimension every pattern fully meets contributes weightBp*100 identically to everyone and cannot affect relative ranking.</summary>
    [Fact]
    public void FullyMetDimension_ContributesEquallyAndCannotAffectRanking()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();

        // HumanReview capability is 2 for every pattern; demand 0 and demand 2 are both fully met (shortfall 0).
        var lowProfile = TestRuleSets.Profile(
            (ConstraintDimension.HumanReview, 0, 2, false),
            (ConstraintDimension.DataSensitivity, 2, 1, false),
            (ConstraintDimension.ToolActionNeed, 2, 1, false));
        var highProfile = TestRuleSets.Profile(
            (ConstraintDimension.HumanReview, 2, 2, false),
            (ConstraintDimension.DataSensitivity, 2, 1, false),
            (ConstraintDimension.ToolActionNeed, 2, 1, false));

        var lowOutcome = Engine.Evaluate(TestRuleSets.BuildScenario("hr-lo", "Low", lowProfile), rules);
        var highOutcome = Engine.Evaluate(TestRuleSets.BuildScenario("hr-hi", "High", highProfile), rules);

        foreach (var patternId in Enum.GetValues<PatternId>())
        {
            var lowTrace = lowOutcome.Rankings.Single(r => r.Pattern == patternId).Contributions.Single(c => c.Dimension == ConstraintDimension.HumanReview);
            var highTrace = highOutcome.Rankings.Single(r => r.Pattern == patternId).Contributions.Single(c => c.Dimension == ConstraintDimension.HumanReview);

            Assert.Equal(0, lowTrace.Shortfall);
            Assert.Equal(0, highTrace.Shortfall);
            Assert.Equal(lowTrace.WeightedContributionScaled, highTrace.WeightedContributionScaled);
        }

        // Since HumanReview's contribution is unaffected by its own level change (fully met either way),
        // raising its level cannot alter any pattern's total score, hence cannot alter the ranking.
        Assert.Equal(lowOutcome.Rankings.Select(r => (r.Pattern, r.ScoreScaled)), highOutcome.Rankings.Select(r => (r.Pattern, r.ScoreScaled)));
        Assert.Equal(lowOutcome.Rankings.Select(r => r.Pattern), highOutcome.Rankings.Select(r => r.Pattern));
    }

    /// <summary>Invariant 10: with a monotone increasing demand curve, raising a constraint's level never decreases shortfall, never increases WeightedContribution, and never increases rawFit, for any pattern.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Monotonicity_RaisingLevel_NeverImprovesFit(int level)
    {
        var rules = TestRuleSets.BuildStandardRuleSet();

        var lowerProfile = TestRuleSets.Profile((ConstraintDimension.DataSensitivity, level, 1, false));
        var higherProfile = TestRuleSets.Profile((ConstraintDimension.DataSensitivity, level + 1, 1, false));

        var lowerOutcome = Engine.Evaluate(TestRuleSets.BuildScenario("mono-lo", "Lower", lowerProfile), rules);
        var higherOutcome = Engine.Evaluate(TestRuleSets.BuildScenario("mono-hi", "Higher", higherProfile), rules);

        foreach (var patternId in Enum.GetValues<PatternId>())
        {
            var lowerTrace = lowerOutcome.Rankings.Single(r => r.Pattern == patternId)
                .Contributions.Single(c => c.Dimension == ConstraintDimension.DataSensitivity);
            var higherTrace = higherOutcome.Rankings.Single(r => r.Pattern == patternId)
                .Contributions.Single(c => c.Dimension == ConstraintDimension.DataSensitivity);

            Assert.True(higherTrace.RawFit <= lowerTrace.RawFit, $"{patternId}: rawFit at level {level + 1} ({higherTrace.RawFit}) should not exceed rawFit at level {level} ({lowerTrace.RawFit}).");
            Assert.True(higherTrace.Shortfall >= lowerTrace.Shortfall, $"{patternId}: shortfall at level {level + 1} ({higherTrace.Shortfall}) should not be less than shortfall at level {level} ({lowerTrace.Shortfall}).");
            Assert.True(higherTrace.WeightedContributionScaled <= lowerTrace.WeightedContributionScaled, $"{patternId}: WeightedContribution at level {level + 1} should not exceed level {level}.");
        }
    }

    /// <summary>Invariant 11: HasNearTie flips exactly at the NearTieMarginBasisPoints threshold, measured among eligible patterns only.</summary>
    [Fact]
    public void NearTie_FlipsExactlyAtThreshold()
    {
        var profile = TestRuleSets.NearTieProfile();

        var atThresholdRules = TestRuleSets.BuildStandardRuleSet(nearTieMarginBasisPoints: 2500);
        var atThresholdOutcome = Engine.Evaluate(TestRuleSets.BuildScenario("tie-2500", "At threshold", profile), atThresholdRules);
        Assert.Equal(250_000, atThresholdOutcome.TopMarginScaled);
        Assert.False(atThresholdOutcome.HasNearTie);

        var overThresholdRules = TestRuleSets.BuildStandardRuleSet(nearTieMarginBasisPoints: 2501);
        var overThresholdOutcome = Engine.Evaluate(TestRuleSets.BuildScenario("tie-2501", "Over threshold", profile), overThresholdRules);
        Assert.Equal(250_000, overThresholdOutcome.TopMarginScaled);
        Assert.True(overThresholdOutcome.HasNearTie);
    }

    /// <summary>Invariant 12a: a crafted hard-gating +1 level change flips the eligible winner (pivotal, MinFlipDistance 1) with no change to the underlying soft scores.</summary>
    [Fact]
    public void Sensitivity_HardVetoFlipScenario_IsPivotalAtDistanceOne()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("flip", "Sensitivity Flip", TestRuleSets.SensitivityFlipProfile());

        var outcome = Engine.Evaluate(scenario, rules);

        Assert.Equal(PatternId.ToolUsingAgent, outcome.Rankings.First().Pattern);
        Assert.Equal(1, outcome.MinFlipDistance);

        var neighborEntry = outcome.Sensitivity.Single(e => e.Dimension == ConstraintDimension.DeterminismReproducibility && e.TestedLevel == 1);
        Assert.True(neighborEntry.WinnerChanged);
        Assert.True(neighborEntry.IsPivotal);
        Assert.Equal(PatternId.ToolUsingAgent, neighborEntry.BaselineTop);
        Assert.Equal(PatternId.RetrievalAugmentedGeneration, neighborEntry.TestedTop);
    }

    /// <summary>
    /// Invariant 12b (new, restored sensitivity): a pure SOFT-score +1 level change flips the eligible
    /// winner with no hard constraint involved at all - the exact capability the additive/linear scoring
    /// model could never exhibit, since a shared demand shift cancels out of any pairwise score
    /// difference. The weighted unmet-demand penalty model's max(0,*) clip breaks that symmetry.
    /// </summary>
    [Fact]
    public void Sensitivity_SoftScoreFlipScenario_IsPivotalAtDistanceOneWithNoHardConflicts()
    {
        var rules = TestRuleSets.BuildSoftFlipRuleSet();
        var scenario = TestRuleSets.BuildScenario("soft-flip", "Soft Score Flip", TestRuleSets.SoftFlipProfile());

        var outcome = Engine.Evaluate(scenario, rules);

        Assert.Equal(PatternId.DirectStructuredCall, outcome.Rankings.First().Pattern);
        Assert.All(outcome.Rankings, r => Assert.True(r.IsEligible));
        Assert.All(outcome.Rankings, r => Assert.Empty(r.HardConflicts));
        Assert.Equal(1, outcome.MinFlipDistance);

        var neighborEntry = outcome.Sensitivity.Single(e => e.Dimension == ConstraintDimension.ToolActionNeed && e.TestedLevel == 3);
        Assert.True(neighborEntry.WinnerChanged);
        Assert.True(neighborEntry.IsPivotal);
        Assert.Equal(PatternId.DirectStructuredCall, neighborEntry.BaselineTop);
        Assert.Equal(PatternId.DeterministicWorkflow, neighborEntry.TestedTop);
    }

    /// <summary>Invariant 12c: a scenario with an unconditionally dominant pattern has no pivotal entries and MinFlipDistance stays int.MaxValue.</summary>
    [Fact]
    public void Sensitivity_RobustScenario_HasNoPivotalEntriesAndMaxFlipDistance()
    {
        var rules = TestRuleSets.BuildDominantRuleSet();
        var scenario = TestRuleSets.BuildScenario("robust", "Sensitivity Robust", TestRuleSets.DominantRobustProfile());

        var outcome = Engine.Evaluate(scenario, rules);

        Assert.Equal(PatternId.DirectStructuredCall, outcome.Rankings.First().Pattern);
        Assert.Equal(int.MaxValue, outcome.MinFlipDistance);
        Assert.All(outcome.Sensitivity, e => Assert.False(e.IsPivotal));
        Assert.All(outcome.Sensitivity, e => Assert.False(e.WinnerChanged));
    }

    /// <summary>Invariant 13: two patterns with identical ScoreScaled are ordered by ascending PatternId.</summary>
    [Fact]
    public void DeterministicTiebreak_EqualScores_OrderByAscendingPatternId()
    {
        var rules = TestRuleSets.BuildTiebreakRuleSet();
        var scenario = TestRuleSets.BuildScenario("tiebreak", "Tiebreak", TestRuleSets.BalancedProfile());

        var outcome = Engine.Evaluate(scenario, rules);

        Assert.Equal(PatternId.DirectStructuredCall, outcome.Rankings[0].Pattern);
        Assert.Equal(PatternId.DeterministicWorkflow, outcome.Rankings[1].Pattern);
        Assert.Equal(outcome.Rankings[0].ScoreScaled, outcome.Rankings[1].ScoreScaled);
        Assert.Equal(1, outcome.Rankings[0].Rank);
        Assert.Equal(2, outcome.Rankings[1].Rank);
    }

    /// <summary>Invariant 15: re-evaluating a DecisionRecord's scenario under the same rules reproduces the same digest and a deep-equal outcome.</summary>
    [Fact]
    public void DecisionRecord_ReEvaluation_IsReproducible()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("record", "Record Repro", TestRuleSets.BalancedProfile());

        var outcome = Engine.Evaluate(scenario, rules);
        var record = new DecisionRecord("record-1", scenario, outcome, outcome.Versions, outcome.ConfigDigest);

        var replayedOutcome = Engine.Evaluate(record.Scenario, rules);

        Assert.Equal(record.ConfigDigest, replayedOutcome.ConfigDigest);
        Assert.Equal(outcome, replayedOutcome);
    }
}
