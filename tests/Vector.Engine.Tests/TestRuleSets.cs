using System.Collections.Immutable;
using Vector.Domain;
using Vector.Engine;

namespace Vector.Engine.Tests;

/// <summary>
/// Hand-crafted rule sets and constraint profiles used to exercise the engine's invariants.
/// The real rule content arrives in a later phase (authored JSON); these fixtures are small,
/// deliberately simple, and their numeric behavior is documented inline where a test depends on it.
/// </summary>
public static class TestRuleSets
{
    /// <summary>The eight canonical dimensions with a display title and polarity, in canonical order.</summary>
    private static readonly (ConstraintDimension Dim, string Title, ConstraintPolarity Polarity)[] DimensionInfo =
    [
        (ConstraintDimension.DataSensitivity, "Data Sensitivity", ConstraintPolarity.Demand),
        (ConstraintDimension.LatencyTarget, "Latency Target", ConstraintPolarity.Demand),
        (ConstraintDimension.CostPressure, "Cost Pressure", ConstraintPolarity.Demand),
        (ConstraintDimension.DeterminismReproducibility, "Determinism & Reproducibility", ConstraintPolarity.Demand),
        (ConstraintDimension.KnowledgeFreshness, "Knowledge Freshness", ConstraintPolarity.Demand),
        (ConstraintDimension.ToolActionNeed, "Tool Action Need", ConstraintPolarity.Demand),
        (ConstraintDimension.HumanReview, "Human Review", ConstraintPolarity.Demand),
        (ConstraintDimension.OperationalMaturity, "Operational Maturity", ConstraintPolarity.Capacity),
    ];

    private static readonly ImmutableArray<int> IncreasingCurve = [0, 1, 2, 3, 4];
    private static readonly ImmutableArray<int> DecreasingCurve = [4, 3, 2, 1, 0];

    /// <summary>
    /// Standard pattern capability matrix (canonical dimension order), hand-tuned so that:
    /// - HumanReview capability is identical (2) across all four patterns.
    /// - ToolUsingAgent is capability-0 on DeterminismReproducibility (a clean, unambiguous hard-veto target)
    ///   yet dominates on ToolActionNeed/KnowledgeFreshness, making it the top RAW scorer whenever those two
    ///   dimensions are weighted heavily - the scenario used to prove hard-veto dominance over a high score.
    /// </summary>
    private static readonly ImmutableArray<int> DirectStructuredCallCapabilities = [4, 4, 4, 4, 0, 0, 2, 4];
    private static readonly ImmutableArray<int> DeterministicWorkflowCapabilities = [4, 3, 3, 4, 0, 2, 2, 3];
    private static readonly ImmutableArray<int> RetrievalAugmentedGenerationCapabilities = [2, 2, 2, 1, 4, 1, 2, 2];
    private static readonly ImmutableArray<int> ToolUsingAgentCapabilities = [1, 1, 1, 0, 3, 4, 2, 1];

    public static ImmutableArray<ConstraintDefinition> BuildConstraints()
    {
        var builder = ImmutableArray.CreateBuilder<ConstraintDefinition>(DimensionInfo.Length);
        foreach (var info in DimensionInfo)
        {
            var curve = info.Polarity == ConstraintPolarity.Capacity ? DecreasingCurve : IncreasingCurve;
            builder.Add(new ConstraintDefinition(info.Dim, info.Title, info.Polarity, $"Help for {info.Title}.", 4, 1, BuildLevels(info.Title), curve));
        }
        return builder.ToImmutable();
    }

    private static ImmutableArray<LevelMetadata> BuildLevels(string title) =>
        [.. Enumerable.Range(0, 5).Select(i => new LevelMetadata(i, $"{title} L{i}", $"Help for {title} at level {i}.", $"Evidence for {title} at level {i}."))];

    private static ImmutableArray<string> BuildRationales(string patternName) =>
        [.. DimensionInfo.Select(d => $"{patternName} rationale for {d.Title}.")];

    private static ArchitecturePattern MakePattern(PatternId id, string name, ImmutableArray<int> capabilities) =>
        new(id, name, $"{name} summary.", capabilities, BuildRationales(name),
            ImmutableArray<Tradeoff>.Empty, ImmutableArray<Risk>.Empty, ImmutableArray<string>.Empty);

    public static ImmutableArray<ArchitecturePattern> BuildStandardPatterns() =>
    [
        MakePattern(PatternId.DirectStructuredCall, "Direct Structured Call", DirectStructuredCallCapabilities),
        MakePattern(PatternId.DeterministicWorkflow, "Deterministic Workflow", DeterministicWorkflowCapabilities),
        MakePattern(PatternId.RetrievalAugmentedGeneration, "Retrieval Augmented Generation", RetrievalAugmentedGenerationCapabilities),
        MakePattern(PatternId.ToolUsingAgent, "Tool Using Agent", ToolUsingAgentCapabilities),
    ];

    /// <summary>The standard rule set used by most tests. Only <paramref name="nearTieMarginBasisPoints"/> varies between calls.</summary>
    public static RuleSet BuildStandardRuleSet(int nearTieMarginBasisPoints = 200)
    {
        var constraints = BuildConstraints();
        var patterns = BuildStandardPatterns();
        var contentHash = DigestCalculator.ComputeRulesContentHash(constraints, patterns, nearTieMarginBasisPoints);
        return new RuleSet("1.0.0-test", contentHash, ">=1.0.0 <2.0.0", constraints, patterns, ImmutableArray<Advisory>.Empty, nearTieMarginBasisPoints);
    }

    /// <summary>
    /// A dedicated rule set where DirectStructuredCall and DeterministicWorkflow have byte-identical
    /// capability matrices (both the highest), guaranteeing an exact ScoreScaled tie at the top of the
    /// ranking regardless of the profile's weights - used to prove the ascending-PatternId tiebreak.
    /// </summary>
    public static RuleSet BuildTiebreakRuleSet()
    {
        var constraints = BuildConstraints();
        var patterns = ImmutableArray.Create(
            MakePattern(PatternId.DirectStructuredCall, "Tie A", ImmutableArray.Create(3, 3, 3, 3, 3, 3, 3, 3)),
            MakePattern(PatternId.DeterministicWorkflow, "Tie B", ImmutableArray.Create(3, 3, 3, 3, 3, 3, 3, 3)),
            MakePattern(PatternId.RetrievalAugmentedGeneration, "Tie C", ImmutableArray.Create(1, 1, 1, 1, 1, 1, 1, 1)),
            MakePattern(PatternId.ToolUsingAgent, "Tie D", ImmutableArray.Create(2, 2, 2, 2, 2, 2, 2, 2)));

        const int nearTie = 200;
        var contentHash = DigestCalculator.ComputeRulesContentHash(constraints, patterns, nearTie);
        return new RuleSet("1.0.0-tiebreak", contentHash, ">=1.0.0 <2.0.0", constraints, patterns, ImmutableArray<Advisory>.Empty, nearTie);
    }

    /// <summary>
    /// Builds a profile from explicit per-dimension overrides. Any of the eight canonical dimensions not
    /// mentioned defaults to LevelIndex 0, WeightTier 0 (unweighted), IsHard false - so a fixture only needs
    /// to name the dimensions it actually cares about.
    /// </summary>
    public static ConstraintProfile Profile(params (ConstraintDimension Dim, int Level, int WeightTier, bool IsHard)[] overrides)
    {
        var byDim = overrides.ToDictionary(o => o.Dim);
        var builder = ImmutableArray.CreateBuilder<ConstraintSetting>(DimensionInfo.Length);
        foreach (var info in DimensionInfo)
        {
            builder.Add(byDim.TryGetValue(info.Dim, out var o)
                ? new ConstraintSetting(info.Dim, o.Level, o.WeightTier, o.IsHard)
                : new ConstraintSetting(info.Dim, 0, 0, false));
        }
        return new ConstraintProfile(builder.ToImmutable());
    }

    /// <summary>All eight dimensions at level 2 with equal weight tier 1 and no hard constraints - a plain, general-purpose profile.</summary>
    public static ConstraintProfile BalancedProfile() => Profile(
        (ConstraintDimension.DataSensitivity, 2, 1, false),
        (ConstraintDimension.LatencyTarget, 2, 1, false),
        (ConstraintDimension.CostPressure, 2, 1, false),
        (ConstraintDimension.DeterminismReproducibility, 2, 1, false),
        (ConstraintDimension.KnowledgeFreshness, 2, 1, false),
        (ConstraintDimension.ToolActionNeed, 2, 1, false),
        (ConstraintDimension.HumanReview, 2, 1, false),
        (ConstraintDimension.OperationalMaturity, 2, 1, false));

    /// <summary>
    /// Hits the shortfall boundary cases against the standard rule set in one profile: DataSensitivity
    /// (level 0, demand 0) gives DirectStructuredCall shortfall 0 (FitScaled == Scale, capability 4 comfortably
    /// meets); KnowledgeFreshness (level 4, demand 4) gives DirectStructuredCall shortfall 4 (FitScaled == 0,
    /// capability 0 fully misses); HumanReview (level 2, demand 2) gives every pattern shortfall 0
    /// (FitScaled == Scale, contributing equally), since all four patterns share capability 2 on HumanReview
    /// and exactly meet a demand of 2.
    /// </summary>
    public static ConstraintProfile BoundaryProfile() => Profile(
        (ConstraintDimension.DataSensitivity, 0, 1, false),
        (ConstraintDimension.KnowledgeFreshness, 4, 1, false),
        (ConstraintDimension.HumanReview, 2, 1, false));

    /// <summary>
    /// Every dimension set to exactly ToolUsingAgent's own capability (or, for the Capacity-polarity
    /// OperationalMaturity, a level whose demand it exactly meets) so that ToolUsingAgent has shortfall 0
    /// on all eight dimensions. Since basis points always sum to 10000, this guarantees
    /// ToolUsingAgent's ScoreScaled == EngineConstants.Scale regardless of how weight is distributed.
    /// </summary>
    public static ConstraintProfile MeetsAllDemandsProfile() => Profile(
        (ConstraintDimension.DataSensitivity, 1, 1, false),
        (ConstraintDimension.LatencyTarget, 1, 1, false),
        (ConstraintDimension.CostPressure, 1, 1, false),
        (ConstraintDimension.DeterminismReproducibility, 0, 1, false),
        (ConstraintDimension.KnowledgeFreshness, 3, 1, false),
        (ConstraintDimension.ToolActionNeed, 4, 1, false),
        (ConstraintDimension.HumanReview, 2, 1, false),
        (ConstraintDimension.OperationalMaturity, 3, 1, false));

    /// <summary>
    /// Concentrates all 10000bp of weight on KnowledgeFreshness at level 4 (demand 4). DirectStructuredCall
    /// has capability 0 there, so its shortfall is the maximum (4), and since it is the only weighted
    /// dimension, its ScoreScaled is exactly 0 - the minimum any pattern can score.
    /// </summary>
    public static ConstraintProfile FullShortfallProfile() => Profile(
        (ConstraintDimension.KnowledgeFreshness, 4, 1, false));

    /// <summary>
    /// A profile where DeterminismReproducibility is a hard constraint at level 1 (demand 1). Only
    /// ToolUsingAgent has capability 0 there, so it alone violates (rawFit -1) regardless of
    /// <paramref name="hardWeightTier"/>. ToolActionNeed and KnowledgeFreshness are set to level 4
    /// (demand 4, where ToolUsingAgent's capabilities of 4 and 3 respectively are the strongest of all
    /// four patterns) and weighted heavily (tier 3 each) - verified across hardWeightTier in 0..3, this
    /// keeps ToolUsingAgent's raw ScoreScaled the highest of all four patterns even though it ends up
    /// vetoed (margins of 250000/178600/125000/83300 scaled units at hardWeightTier 0/1/2/3 respectively).
    /// </summary>
    public static ConstraintProfile HardVetoProfile(int hardWeightTier) => Profile(
        (ConstraintDimension.DeterminismReproducibility, 1, hardWeightTier, true),
        (ConstraintDimension.ToolActionNeed, 4, 3, false),
        (ConstraintDimension.KnowledgeFreshness, 4, 3, false));

    /// <summary>
    /// Baseline for the hard-veto sensitivity-flip invariant. DeterminismReproducibility is hard at level 0
    /// (demand 0, trivially met by every pattern, including ToolUsingAgent). ToolActionNeed/KnowledgeFreshness
    /// are set to level 4 (demand 4) and weighted heavily (tier 3 each, weight 0 on DeterminismReproducibility
    /// itself), making ToolUsingAgent the eligible top-1 (score 875000) ahead of
    /// RetrievalAugmentedGeneration (625000) - only ToolUsingAgent's capabilities of 4/3 on those two
    /// dimensions come close to meeting a demand of 4. Raising DeterminismReproducibility to level 1
    /// (demand 1) flips ToolUsingAgent to a hard conflict (capability 0 &lt; demand 1), making
    /// RetrievalAugmentedGeneration the new eligible top-1 - a pure hard-gating, distance-1 flip (the
    /// underlying ToolActionNeed/KnowledgeFreshness scores are untouched, since DeterminismReproducibility
    /// carries zero weight).
    /// </summary>
    public static ConstraintProfile SensitivityFlipProfile() => Profile(
        (ConstraintDimension.DeterminismReproducibility, 0, 0, true),
        (ConstraintDimension.ToolActionNeed, 4, 3, false),
        (ConstraintDimension.KnowledgeFreshness, 4, 3, false));

    /// <summary>
    /// A dedicated rule set for the pure SOFT-score sensitivity-flip invariant (no hard constraints
    /// anywhere). Only ToolActionNeed (tier 3, 7500bp) and LatencyTarget (tier 1, 2500bp) carry weight;
    /// every other dimension is unweighted filler (capability 2 for all four patterns, so it never
    /// affects score). DirectStructuredCall's ToolActionNeed capability (2) sits exactly at the baseline
    /// demand (level 2), while DeterministicWorkflow's (4) is comfortably above it; DirectStructuredCall
    /// leads on LatencyTarget (capability 3 vs 1) by a smaller margin. At baseline, DirectStructuredCall
    /// leads 1000000 to 937500. Raising ToolActionNeed by one level (demand 3) pushes DirectStructuredCall's
    /// ToolActionNeed capability into shortfall (drops to 812500) while DeterministicWorkflow is unaffected
    /// (still meets demand 3), flipping the eligible top-1 to DeterministicWorkflow (937500) - a genuine
    /// soft-score flip driven purely by the level change, with no gating involved.
    /// </summary>
    public static RuleSet BuildSoftFlipRuleSet()
    {
        var constraints = BuildConstraints();
        var patterns = ImmutableArray.Create(
            MakePattern(PatternId.DirectStructuredCall, "Soft Flip A", ImmutableArray.Create(2, 3, 2, 2, 2, 2, 2, 2)),
            MakePattern(PatternId.DeterministicWorkflow, "Soft Flip B", ImmutableArray.Create(2, 1, 2, 2, 2, 4, 2, 2)),
            MakePattern(PatternId.RetrievalAugmentedGeneration, "Soft Flip Filler 1", ImmutableArray.Create(2, 2, 2, 2, 2, 1, 2, 2)),
            MakePattern(PatternId.ToolUsingAgent, "Soft Flip Filler 2", ImmutableArray.Create(2, 0, 2, 2, 2, 3, 2, 2)));

        const int nearTie = 200;
        var contentHash = DigestCalculator.ComputeRulesContentHash(constraints, patterns, nearTie);
        return new RuleSet("1.0.0-softflip", contentHash, ">=1.0.0 <2.0.0", constraints, patterns, ImmutableArray<Advisory>.Empty, nearTie);
    }

    public static ConstraintProfile SoftFlipProfile() => Profile(
        (ConstraintDimension.ToolActionNeed, 2, 3, false),
        (ConstraintDimension.LatencyTarget, 2, 1, false));

    /// <summary>
    /// A dedicated rule set with one unconditionally dominant pattern (capability 4 on every dimension)
    /// used to prove genuine sensitivity robustness under the new scoring model.
    /// </summary>
    public static RuleSet BuildDominantRuleSet()
    {
        var constraints = BuildConstraints();
        var patterns = ImmutableArray.Create(
            MakePattern(PatternId.DirectStructuredCall, "Dominant", ImmutableArray.Create(4, 4, 4, 4, 4, 4, 4, 4)),
            MakePattern(PatternId.DeterministicWorkflow, "Rival A", ImmutableArray.Create(2, 2, 2, 2, 2, 2, 2, 2)),
            MakePattern(PatternId.RetrievalAugmentedGeneration, "Rival B", ImmutableArray.Create(3, 1, 3, 1, 3, 1, 3, 1)),
            MakePattern(PatternId.ToolUsingAgent, "Rival C", ImmutableArray.Create(1, 3, 1, 3, 1, 3, 1, 3)));

        const int nearTie = 200;
        var contentHash = DigestCalculator.ComputeRulesContentHash(constraints, patterns, nearTie);
        return new RuleSet("1.0.0-dominant", contentHash, ">=1.0.0 <2.0.0", constraints, patterns, ImmutableArray<Advisory>.Empty, nearTie);
    }

    /// <summary>
    /// Any levels/weights work here: DirectStructuredCall has capability 4 on every dimension in
    /// <see cref="BuildDominantRuleSet"/>, so shortfall = max(0, demand - 4) is always 0 (demand never
    /// exceeds 4) - its ScoreScaled is always exactly Scale, the maximum any pattern can achieve. It can
    /// never be outscored, and any tie resolves to it via the ascending-PatternId tiebreak (id 1, the
    /// lowest). So no single-dimension level change can ever change the eligible top-1: this profile
    /// proves genuine sensitivity robustness (as opposed to robustness by construction-of-irrelevance).
    /// </summary>
    public static ConstraintProfile DominantRobustProfile() => Profile(
        (ConstraintDimension.DataSensitivity, 2, 1, false),
        (ConstraintDimension.LatencyTarget, 3, 2, false),
        (ConstraintDimension.CostPressure, 1, 1, false),
        (ConstraintDimension.DeterminismReproducibility, 4, 3, false),
        (ConstraintDimension.KnowledgeFreshness, 0, 1, false),
        (ConstraintDimension.ToolActionNeed, 2, 2, false),
        (ConstraintDimension.HumanReview, 3, 1, false),
        (ConstraintDimension.OperationalMaturity, 1, 1, false));

    /// <summary>
    /// Isolates all scoring weight (10000bp) onto LatencyTarget at level 4 (demand 4), where only
    /// DirectStructuredCall (capability 4) fully meets demand (shortfall 0, contributing the full
    /// 1000000); DeterministicWorkflow (capability 3) falls short by 1 (contributing 750000). This makes
    /// the top-1/top-2 margin exactly 250000 scaled units - the value needed to straddle a
    /// NearTieMarginBasisPoints of 2500 (threshold 250000, not-tied, since the comparison is
    /// strict-less-than) versus 2501 (threshold 250100, tied).
    /// </summary>
    public static ConstraintProfile NearTieProfile() => Profile(
        (ConstraintDimension.LatencyTarget, 4, 1, false));

    /// <summary>Wraps a profile into a minimal scenario. Metadata is deliberately trivial: it must never affect the digest.</summary>
    public static Scenario BuildScenario(string id, string title, ConstraintProfile profile) =>
        new(id, title, Description: null, ImmutableArray<string>.Empty, profile);
}
