using System.Collections.Immutable;
using System.Globalization;
using Vector.Domain;

namespace Vector.Engine.Tests;

public class DigestCalculatorTests
{
    private static readonly IDecisionEngine Engine = new DecisionEngine();

    // Pinned golden digest for TestRuleSets.BuildStandardRuleSet() + TestRuleSets.BalancedProfile().
    // Computed once from this exact fixture; any change to the fixture, the engine's hashed fields,
    // or the digest algorithm itself must be accompanied by recomputing and re-pinning this value.
    private const string GoldenDigest = "Sha256:25f71a72fc2040a9f37024065fbd64ea89757ad83b7423188def55fc5b40fa91";

    private static string ComputeGoldenDigest()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var scenario = TestRuleSets.BuildScenario("golden", "Golden", TestRuleSets.BalancedProfile());
        return Engine.Evaluate(scenario, rules).ConfigDigest;
    }

    /// <summary>Invariant 2: a fixed fixture hashes to a pinned digest, unchanged under non-invariant cultures.</summary>
    [Fact]
    public void Digest_MatchesGoldenValue_AndIsCultureInvariant()
    {
        Assert.Equal(GoldenDigest, ComputeGoldenDigest());

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal(GoldenDigest, ComputeGoldenDigest());

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(GoldenDigest, ComputeGoldenDigest());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>Invariant 14: changing a weight tier, level index, hard flag, or RulesVersion changes the digest; changing scenario metadata does not.</summary>
    [Fact]
    public void Digest_ChangesWithConfigChanges_ButNotWithMetadata()
    {
        var rules = TestRuleSets.BuildStandardRuleSet();
        var baseProfile = TestRuleSets.BalancedProfile();
        var baseScenario = TestRuleSets.BuildScenario("d1", "Baseline", baseProfile);
        var baseDigest = Engine.Evaluate(baseScenario, rules).ConfigDigest;

        var weightTierChanged = new ConstraintProfile([.. baseProfile.Settings.Select(s =>
            s.Dimension == ConstraintDimension.DataSensitivity ? s with { WeightTier = s.WeightTier == 3 ? 2 : s.WeightTier + 1 } : s)]);
        Assert.NotEqual(baseDigest, Engine.Evaluate(TestRuleSets.BuildScenario("d1", "Baseline", weightTierChanged), rules).ConfigDigest);

        var levelChanged = new ConstraintProfile([.. baseProfile.Settings.Select(s =>
            s.Dimension == ConstraintDimension.DataSensitivity ? s with { LevelIndex = s.LevelIndex == 4 ? 3 : s.LevelIndex + 1 } : s)]);
        Assert.NotEqual(baseDigest, Engine.Evaluate(TestRuleSets.BuildScenario("d1", "Baseline", levelChanged), rules).ConfigDigest);

        var hardFlagChanged = new ConstraintProfile([.. baseProfile.Settings.Select(s =>
            s.Dimension == ConstraintDimension.DataSensitivity ? s with { IsHard = !s.IsHard } : s)]);
        Assert.NotEqual(baseDigest, Engine.Evaluate(TestRuleSets.BuildScenario("d1", "Baseline", hardFlagChanged), rules).ConfigDigest);

        var constraints = TestRuleSets.BuildConstraints();
        var patterns = TestRuleSets.BuildStandardPatterns();
        var contentHash = DigestCalculator.ComputeRulesContentHash(constraints, patterns, 200);
        var rulesVersionChanged = new RuleSet("2.0.0-test", contentHash, ">=1.0.0 <2.0.0", constraints, patterns, ImmutableArray<Advisory>.Empty, 200);
        Assert.NotEqual(baseDigest, Engine.Evaluate(baseScenario, rulesVersionChanged).ConfigDigest);

        // Scenario metadata must NOT affect the digest.
        var metadataChanged = new Scenario("different-id", "Completely Different Title", "Some description", ["assumption one"], baseProfile);
        Assert.Equal(baseDigest, Engine.Evaluate(metadataChanged, rules).ConfigDigest);
    }

    /// <summary>Invariant 16: mutating a capability value changes ComputeRulesContentHash.</summary>
    [Fact]
    public void RulesContentHash_ChangesWhenCapabilityMutates()
    {
        var constraints = TestRuleSets.BuildConstraints();
        var patterns = TestRuleSets.BuildStandardPatterns();
        var originalHash = DigestCalculator.ComputeRulesContentHash(constraints, patterns, 200);

        var original = patterns[0];
        var mutatedCapability = original.Capabilities[0] == 4 ? 3 : original.Capabilities[0] + 1;
        var mutatedPattern = new ArchitecturePattern(
            original.Id,
            original.Name,
            original.Summary,
            original.Capabilities.SetItem(0, mutatedCapability),
            original.Rationales,
            original.Tradeoffs,
            original.Risks,
            original.VariantNotes);
        var mutatedPatterns = patterns.SetItem(0, mutatedPattern);

        var mutatedHash = DigestCalculator.ComputeRulesContentHash(constraints, mutatedPatterns, 200);

        Assert.NotEqual(originalHash, mutatedHash);
    }

    [Fact]
    public void RulesContentHash_ChangesWhenNearTieMarginChanges()
    {
        var constraints = TestRuleSets.BuildConstraints();
        var patterns = TestRuleSets.BuildStandardPatterns();

        var hash1 = DigestCalculator.ComputeRulesContentHash(constraints, patterns, 200);
        var hash2 = DigestCalculator.ComputeRulesContentHash(constraints, patterns, 201);

        Assert.NotEqual(hash1, hash2);
    }
}
