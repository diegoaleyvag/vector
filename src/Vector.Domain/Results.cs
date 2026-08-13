using System.Collections.Immutable;

namespace Vector.Domain;

/// <summary>
/// The full per-dimension accounting for a single pattern's score under the weighted unmet-demand
/// penalty model: capability, demand, raw fit, the unmet-demand shortfall, the per-dimension fit and
/// weighted contribution/penalty (all integer, SCALE-fixed-point), and the hard-constraint verdict.
/// </summary>
/// <remarks>
/// <paramref name="Shortfall"/> = max(0, Demand - Capability) is the nonlinear clip that gives the
/// engine level sensitivity: a pattern that fully meets demand (Shortfall 0) always earns the full
/// per-dimension contribution regardless of how much capability it has to spare, while any unmet
/// demand reduces its contribution and adds to <paramref name="ShortfallPenaltyScaled"/>.
/// </remarks>
public sealed record ContributionTrace(
    ConstraintDimension Dimension,
    int LevelIndex,
    string LevelLabel,
    int Capability,
    int Demand,
    int RawFit,
    int Shortfall,
    long FitScaled,
    int WeightBasisPoints,
    long WeightedContributionScaled,
    long ShortfallPenaltyScaled,
    ContributionSign Sign,
    bool IsHard,
    HardVerdict HardVerdict,
    string Rationale);

/// <summary>Records a single hard-constraint violation: the pattern could not meet the demanded level on this dimension.</summary>
public sealed record HardConflict(ConstraintDimension Dimension, int LevelIndex, string Reason);

/// <summary>
/// The full evaluation result for one architecture pattern under a scenario: eligibility, rank,
/// score, per-dimension contribution trace, any hard conflicts, and active risks/advisories.
/// </summary>
public sealed record PatternResult(
    PatternId Pattern,
    int Rank,
    HardStatus HardStatus,
    bool IsEligible,
    long ScoreScaled,
    double Score,
    ImmutableArray<ContributionTrace> Contributions,
    ImmutableArray<HardConflict> HardConflicts,
    ImmutableArray<Risk> ActiveRisks,
    ImmutableArray<Advisory> ActiveAdvisories)
{
    public bool Equals(PatternResult? other) =>
        other is not null
        && Pattern == other.Pattern
        && Rank == other.Rank
        && HardStatus == other.HardStatus
        && IsEligible == other.IsEligible
        && ScoreScaled == other.ScoreScaled
        && Score.Equals(other.Score)
        && Contributions.SequenceEqual(other.Contributions)
        && HardConflicts.SequenceEqual(other.HardConflicts)
        && ActiveRisks.SequenceEqual(other.ActiveRisks)
        && ActiveAdvisories.SequenceEqual(other.ActiveAdvisories);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Pattern);
        hash.Add(Rank);
        hash.Add(HardStatus);
        hash.Add(IsEligible);
        hash.Add(ScoreScaled);
        hash.Add(Score);
        foreach (var c in Contributions)
        {
            hash.Add(c);
        }
        foreach (var c in HardConflicts)
        {
            hash.Add(c);
        }
        foreach (var r in ActiveRisks)
        {
            hash.Add(r);
        }
        foreach (var a in ActiveAdvisories)
        {
            hash.Add(a);
        }
        return hash.ToHashCode();
    }
}

/// <summary>
/// One probe of the one-at-a-time sensitivity analysis: what happens to the eligible top-1 pattern
/// when a single dimension's level is changed from its baseline value while all others stay fixed.
/// </summary>
public sealed record SensitivityEntry(
    ConstraintDimension Dimension,
    int BaselineLevel,
    int TestedLevel,
    PatternId BaselineTop,
    PatternId TestedTop,
    bool WinnerChanged,
    long BaselineMarginScaled,
    long TestedMarginScaled,
    bool IsPivotal);

/// <summary>Identifies the exact engine and rule content versions used to produce a decision, plus the fixed-point scale.</summary>
public sealed record VersionStamp(string EngineVersion, string RulesVersion, string RulesContentHash, int Scale);

/// <summary>
/// The full, deterministic result of evaluating a scenario against a rule set: the ranked patterns,
/// a content digest of the exact configuration that produced it, near-tie/robustness signals, and
/// the one-at-a-time sensitivity analysis.
/// </summary>
public sealed record DecisionOutcome(
    ImmutableArray<PatternResult> Rankings,
    string ConfigDigest,
    bool HasNearTie,
    long TopMarginScaled,
    ImmutableArray<SensitivityEntry> Sensitivity,
    int MinFlipDistance,
    VersionStamp Versions)
{
    public bool Equals(DecisionOutcome? other) =>
        other is not null
        && Rankings.SequenceEqual(other.Rankings)
        && ConfigDigest == other.ConfigDigest
        && HasNearTie == other.HasNearTie
        && TopMarginScaled == other.TopMarginScaled
        && Sensitivity.SequenceEqual(other.Sensitivity)
        && MinFlipDistance == other.MinFlipDistance
        && Versions == other.Versions;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var r in Rankings)
        {
            hash.Add(r);
        }
        hash.Add(ConfigDigest);
        hash.Add(HasNearTie);
        hash.Add(TopMarginScaled);
        foreach (var s in Sensitivity)
        {
            hash.Add(s);
        }
        hash.Add(MinFlipDistance);
        hash.Add(Versions);
        return hash.ToHashCode();
    }
}

/// <summary>A durable record pairing the scenario that was evaluated with the outcome it produced.</summary>
public sealed record DecisionRecord(string Id, Scenario Scenario, DecisionOutcome Outcome, VersionStamp Versions, string ConfigDigest);
