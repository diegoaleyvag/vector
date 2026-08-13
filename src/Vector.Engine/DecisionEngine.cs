using System.Collections.Immutable;
using Vector.Domain;

namespace Vector.Engine;

/// <summary>
/// Deterministic, integer-only MCDA decision engine. Scores each architecture pattern against a
/// constraint profile dimension-by-dimension in canonical order, applies hard-constraint gating as an
/// independent pass, ranks patterns with a composite key that never lets a hard conflict hide behind a
/// high score, and computes near-tie and one-at-a-time sensitivity signals.
/// </summary>
public sealed class DecisionEngine : IDecisionEngine
{
    private static readonly ConstraintDimension[] CanonicalDimensions =
        [.. Enum.GetValues<ConstraintDimension>().OrderBy(d => (int)d)];

    private static readonly PatternId[] CanonicalPatternIds =
        [.. Enum.GetValues<PatternId>().OrderBy(p => (int)p)];

    /// <inheritdoc />
    public DecisionOutcome Evaluate(Scenario scenario, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(rules);

        var profile = scenario.Profile;

        var ranked = RankPatterns(profile, rules);
        var rankings = ToPatternResults(ranked, profile, rules);

        var configDigest = DigestCalculator.ComputeConfigDigest(profile, rules);

        var eligibleCount = 0;
        foreach (var r in ranked)
        {
            if (r.Scoring.HardStatus == HardStatus.Eligible)
            {
                eligibleCount++;
            }
        }

        var (baselineTop, baselineMargin) = TopEligibleOrFallback(ranked);
        var hasNearTie = eligibleCount >= 2
            && baselineMargin < (long)rules.NearTieMarginBasisPoints * EngineConstants.Scale / 10000;

        var (sensitivity, minFlipDistance) = ComputeSensitivity(profile, rules, baselineTop, baselineMargin);

        var versions = new VersionStamp(EngineConstants.EngineVersion, rules.RulesVersion, rules.RulesContentHash, EngineConstants.Scale);

        return new DecisionOutcome(rankings, configDigest, hasNearTie, baselineMargin, sensitivity, minFlipDistance, versions);
    }

    /// <summary>The per-dimension accounting produced by the scoring+gating core for a single pattern.</summary>
    private readonly record struct ScoringResult(
        ImmutableArray<ContributionTrace> Contributions,
        ImmutableArray<HardConflict> HardConflicts,
        long ScoreScaled,
        HardStatus HardStatus);

    private readonly record struct ScoredPattern(PatternId Id, ScoringResult Scoring);

    /// <summary>
    /// The scoring + hard-gating core. Iterates all eight canonical dimensions, computing capability,
    /// demand, raw fit, the unmet-demand shortfall, and the weighted contribution/penalty under the
    /// weighted unmet-demand penalty model, plus hard-constraint verdicts. This is the single source of
    /// truth reused by both <see cref="Evaluate"/> and the sensitivity probes below.
    /// </summary>
    /// <remarks>
    /// Scoring model: shortfall = max(0, demand - capability) in 0..4 is the nonlinear clip that gives
    /// level sensitivity (a pattern that already meets demand earns no extra credit for exceeding it,
    /// and gets no additional penalty from a demand increase until its own capability is exceeded).
    /// WeightedContributionScaled = weightBp * (100 - 25*shortfall), so a pattern that fully meets every
    /// demand scores exactly Scale (10000 bp * 100 = 1_000_000), since basis points always sum to 10000.
    /// </remarks>
    private static ScoringResult ScorePattern(ArchitecturePattern pattern, ConstraintProfile profile, RuleSet rules)
    {
        var contributions = ImmutableArray.CreateBuilder<ContributionTrace>(CanonicalDimensions.Length);
        var conflicts = ImmutableArray.CreateBuilder<HardConflict>();
        long scoreScaled = 0;

        foreach (var dimension in CanonicalDimensions)
        {
            var setting = profile[dimension];
            var constraintDef = rules.Constraint(dimension);

            var capability = pattern.CapabilityFor(dimension);
            var demand = rules.Demand(dimension, setting.LevelIndex);
            var rawFit = capability - demand;
            var shortfall = Math.Max(0, -rawFit);

            var weightBp = profile.Weights[dimension];

            var weightedContribution = (long)weightBp * (100 - 25 * shortfall);
            var shortfallPenalty = 25L * weightBp * shortfall;
            var fitScaled = (long)(4 - shortfall) * 250_000;

            scoreScaled += weightedContribution;

            var sign = rawFit switch
            {
                > 0 => ContributionSign.Positive,
                < 0 => ContributionSign.Negative,
                _ => ContributionSign.Neutral,
            };

            var hardVerdict = setting.IsHard
                ? (rawFit < 0 ? HardVerdict.Conflict : HardVerdict.Compatible)
                : HardVerdict.NotApplicable;

            var levelLabel = rules.LevelLabel(dimension, setting.LevelIndex);
            var rationale = pattern.RationaleFor(dimension);

            contributions.Add(new ContributionTrace(
                dimension,
                setting.LevelIndex,
                levelLabel,
                capability,
                demand,
                rawFit,
                shortfall,
                fitScaled,
                weightBp,
                weightedContribution,
                shortfallPenalty,
                sign,
                setting.IsHard,
                hardVerdict,
                rationale));

            if (setting.IsHard && rawFit < 0)
            {
                var reason = $"Requires {levelLabel} on {constraintDef.Title}, which this pattern cannot meet.";
                conflicts.Add(new HardConflict(dimension, setting.LevelIndex, reason));
            }
        }

        var hardStatus = conflicts.Count > 0 ? HardStatus.Vetoed : HardStatus.Eligible;

        return new ScoringResult(contributions.ToImmutable(), conflicts.ToImmutable(), scoreScaled, hardStatus);
    }

    /// <summary>
    /// Scores and ranks all four patterns. Ranking key: HardStatus ascending (Eligible before Vetoed),
    /// then ScoreScaled descending, then PatternId ascending as a deterministic tiebreak.
    /// </summary>
    private static ImmutableArray<ScoredPattern> RankPatterns(ConstraintProfile profile, RuleSet rules)
    {
        var scored = new ScoredPattern[CanonicalPatternIds.Length];
        for (var i = 0; i < CanonicalPatternIds.Length; i++)
        {
            var id = CanonicalPatternIds[i];
            scored[i] = new ScoredPattern(id, ScorePattern(rules.Pattern(id), profile, rules));
        }

        Array.Sort(scored, CompareScoredPatterns);
        return [.. scored];
    }

    private static int CompareScoredPatterns(ScoredPattern a, ScoredPattern b)
    {
        var statusCompare = a.Scoring.HardStatus.CompareTo(b.Scoring.HardStatus);
        if (statusCompare != 0)
        {
            return statusCompare;
        }

        var scoreCompare = b.Scoring.ScoreScaled.CompareTo(a.Scoring.ScoreScaled);
        if (scoreCompare != 0)
        {
            return scoreCompare;
        }

        return ((int)a.Id).CompareTo((int)b.Id);
    }

    private static ImmutableArray<PatternResult> ToPatternResults(ImmutableArray<ScoredPattern> ranked, ConstraintProfile profile, RuleSet rules)
    {
        var builder = ImmutableArray.CreateBuilder<PatternResult>(ranked.Length);
        for (var i = 0; i < ranked.Length; i++)
        {
            var scored = ranked[i];
            var pattern = rules.Pattern(scored.Id);
            var activeRisks = ComputeActiveRisks(pattern, profile);
            var activeAdvisories = ComputeActiveAdvisories(scored.Id, profile, rules);

            builder.Add(new PatternResult(
                scored.Id,
                i + 1,
                scored.Scoring.HardStatus,
                scored.Scoring.HardStatus == HardStatus.Eligible,
                scored.Scoring.ScoreScaled,
                (double)scored.Scoring.ScoreScaled / EngineConstants.Scale,
                scored.Scoring.Contributions,
                scored.Scoring.HardConflicts,
                activeRisks,
                activeAdvisories));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// A risk is active iff it has no related dimension (always active) or the profile's level on its
    /// related dimension is at or above its activation threshold.
    /// </summary>
    private static ImmutableArray<Risk> ComputeActiveRisks(ArchitecturePattern pattern, ConstraintProfile profile)
    {
        if (pattern.Risks.IsDefaultOrEmpty)
        {
            return ImmutableArray<Risk>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<Risk>();
        foreach (var risk in pattern.Risks)
        {
            if (risk.RelatedDimension is null)
            {
                builder.Add(risk);
                continue;
            }

            var levelIndex = profile[risk.RelatedDimension.Value].LevelIndex;
            var threshold = risk.ActivatesAtOrAboveLevel ?? 0;
            if (levelIndex >= threshold)
            {
                builder.Add(risk);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>An advisory applies to a pattern iff it targets that pattern and the profile's level on its dimension satisfies its operator.</summary>
    private static ImmutableArray<Advisory> ComputeActiveAdvisories(PatternId patternId, ConstraintProfile profile, RuleSet rules)
    {
        if (rules.Advisories.IsDefaultOrEmpty)
        {
            return ImmutableArray<Advisory>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<Advisory>();
        foreach (var advisory in rules.Advisories)
        {
            if (advisory.Pattern != patternId)
            {
                continue;
            }

            var levelIndex = profile[advisory.Dimension].LevelIndex;
            if (advisory.Matches(levelIndex))
            {
                builder.Add(advisory);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Returns the eligible top-1 pattern and the margin over the eligible runner-up. Convention when
    /// fewer than two patterns are eligible ("no contest"): with exactly one eligible pattern, the margin
    /// is that pattern's own score; with zero eligible patterns, the margin is <see cref="long.MaxValue"/>
    /// and the reported "top" falls back to the overall rank-1 pattern (which will be vetoed).
    /// </summary>
    private static (PatternId Top, long Margin) TopEligibleOrFallback(ImmutableArray<ScoredPattern> ranked)
    {
        ScoredPattern? first = null;
        ScoredPattern? second = null;
        foreach (var r in ranked)
        {
            if (r.Scoring.HardStatus != HardStatus.Eligible)
            {
                continue;
            }

            if (first is null)
            {
                first = r;
            }
            else if (second is null)
            {
                second = r;
                break;
            }
        }

        if (first is not null && second is not null)
        {
            return (first.Value.Id, first.Value.Scoring.ScoreScaled - second.Value.Scoring.ScoreScaled);
        }

        if (first is not null)
        {
            return (first.Value.Id, first.Value.Scoring.ScoreScaled);
        }

        return (ranked[0].Id, long.MaxValue);
    }

    /// <summary>
    /// One-at-a-time sensitivity analysis: for each dimension, holding all other settings fixed, probes
    /// every alternate level to find the smallest level-distance that flips the eligible winner
    /// (<see cref="DecisionOutcome.MinFlipDistance"/>), and records a <see cref="SensitivityEntry"/> for
    /// each immediate (+/-1) neighbor level as the most informative, human-facing signal.
    /// </summary>
    private static (ImmutableArray<SensitivityEntry> Entries, int MinFlipDistance) ComputeSensitivity(
        ConstraintProfile baselineProfile, RuleSet rules, PatternId baselineTop, long baselineMargin)
    {
        var entries = ImmutableArray.CreateBuilder<SensitivityEntry>();
        var minFlipDistance = int.MaxValue;

        foreach (var dimension in CanonicalDimensions)
        {
            var constraintDef = rules.Constraint(dimension);
            var maxLevelIndex = constraintDef.Levels.Length - 1;
            var baselineLevel = baselineProfile[dimension].LevelIndex;

            for (var testedLevel = 0; testedLevel <= maxLevelIndex; testedLevel++)
            {
                if (testedLevel == baselineLevel)
                {
                    continue;
                }

                var testedProfile = WithLevel(baselineProfile, dimension, testedLevel);
                var testedRanked = RankPatterns(testedProfile, rules);
                var (testedTop, testedMargin) = TopEligibleOrFallback(testedRanked);
                var winnerChanged = testedTop != baselineTop;

                var distance = Math.Abs(testedLevel - baselineLevel);
                if (winnerChanged && distance < minFlipDistance)
                {
                    minFlipDistance = distance;
                }

                if (distance == 1)
                {
                    entries.Add(new SensitivityEntry(
                        dimension,
                        baselineLevel,
                        testedLevel,
                        baselineTop,
                        testedTop,
                        winnerChanged,
                        baselineMargin,
                        testedMargin,
                        IsPivotal: winnerChanged));
                }
            }
        }

        return (entries.ToImmutable(), minFlipDistance);
    }

    private static ConstraintProfile WithLevel(ConstraintProfile profile, ConstraintDimension dimension, int newLevel)
    {
        var settings = profile.Settings.ToBuilder();
        for (var i = 0; i < settings.Count; i++)
        {
            if (settings[i].Dimension == dimension)
            {
                settings[i] = settings[i] with { LevelIndex = newLevel };
                break;
            }
        }

        return new ConstraintProfile(settings.ToImmutable());
    }
}
