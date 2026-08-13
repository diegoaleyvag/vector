using System.Globalization;
using System.Text;
using Vector.Domain;

namespace Vector.Data.Export;

/// <summary>
/// Renders a deterministic, ADR-style markdown document summarizing a decision-support evaluation.
/// Pure: no Blazor, no clock access (the date is injected via <see cref="ExportInput.FixedDate"/>), no
/// I/O. Always frames output as decision support ("leading under these constraints"), never as a
/// verdict of correctness ("best"/"correct" architecture never appear).
/// </summary>
public static class MarkdownExporter
{
    private const string Sha256Prefix = "Sha256:";
    private const int DigestShortLength = 12;

    /// <summary>Renders the full ADR markdown document for the given evaluation.</summary>
    public static string Export(ExportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var sb = new StringBuilder();

        AppendHeader(sb, input);
        AppendContext(sb, input);
        AppendConstraintsTable(sb, input);
        AppendPatternsConsidered(sb, input);
        AppendHardConflicts(sb, input);
        AppendScoringTrace(sb, input);
        AppendShortlist(sb, input);
        AppendRationale(sb, input);
        AppendSensitivityNotes(sb, input);
        AppendRisksAndMitigations(sb, input);
        AppendConsequences(sb, input);
        AppendUnresolvedQuestions(sb, input);
        AppendFooter(sb, input);

        return sb.ToString();
    }

    private static string ShortDigest(string configDigest)
    {
        var hex = configDigest.StartsWith(Sha256Prefix, StringComparison.Ordinal)
            ? configDigest[Sha256Prefix.Length..]
            : configDigest;

        return hex.Length >= DigestShortLength ? hex[..DigestShortLength] : hex;
    }

    private static string Title(ExportInput input) =>
        input.Scenario is not null ? input.Scenario.Title : "Custom constraint profile";

    private static void AppendHeader(StringBuilder sb, ExportInput input)
    {
        sb.Append("# Architecture Decision Record: ").Append(Title(input)).Append('\n').Append('\n');
        sb.Append("Status: Draft — decision support only").Append('\n').Append('\n');
        sb.Append("Date: ").Append(input.FixedDate).Append('\n').Append('\n');
        sb.Append("Engine version: ").Append(input.Outcome.Versions.EngineVersion)
          .Append(" · Rules version: ").Append(input.Outcome.Versions.RulesVersion).Append('\n').Append('\n');
        sb.Append("Config digest: `Sha256:").Append(ShortDigest(input.Outcome.ConfigDigest)).Append("…`").Append('\n').Append('\n');
    }

    private static void AppendContext(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Context & Scenario").Append('\n').Append('\n');

        if (input.Scenario is not null)
        {
            if (!string.IsNullOrEmpty(input.Scenario.Description))
            {
                sb.Append(input.Scenario.Description).Append('\n').Append('\n');
            }

            sb.Append("Assumptions:").Append('\n');
            if (input.Scenario.Assumptions.IsDefaultOrEmpty)
            {
                sb.Append("- No assumptions recorded.").Append('\n');
            }
            else
            {
                foreach (var assumption in input.Scenario.Assumptions)
                {
                    sb.Append("- ").Append(assumption).Append('\n');
                }
            }
        }
        else
        {
            sb.Append("Custom constraint profile (no predefined scenario).").Append('\n').Append('\n');
            sb.Append("Assumptions:").Append('\n');
            sb.Append("- No assumptions recorded.").Append('\n');
        }

        sb.Append('\n');
    }

    private static void AppendConstraintsTable(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Constraints & Weights").Append('\n').Append('\n');
        sb.Append("| Dimension | Level | Weight tier | Hard? |").Append('\n');
        sb.Append("|---|---|---|---|").Append('\n');

        foreach (var setting in input.Profile.Settings)
        {
            var constraintDef = input.Rules.Constraint(setting.Dimension);
            var levelName = input.Rules.LevelLabel(setting.Dimension, setting.LevelIndex);
            sb.Append("| ").Append(constraintDef.Title)
              .Append(" | ").Append(levelName)
              .Append(" | ").Append(setting.WeightTier.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(setting.IsHard ? "Yes" : "No")
              .Append(" |").Append('\n');
        }

        sb.Append('\n');
    }

    private static void AppendPatternsConsidered(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Patterns Considered").Append('\n').Append('\n');

        foreach (var pattern in input.Rules.Patterns)
        {
            sb.Append("- **").Append(pattern.Name).Append("** — ").Append(pattern.Summary).Append('\n');
        }

        sb.Append('\n');
    }

    private static void AppendHardConflicts(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Hard Conflicts").Append('\n').Append('\n');

        var anyConflicts = false;
        foreach (var result in input.Outcome.Rankings)
        {
            if (result.HardConflicts.IsDefaultOrEmpty)
            {
                continue;
            }

            anyConflicts = true;
            var pattern = input.Rules.Pattern(result.Pattern);
            sb.Append("**").Append(pattern.Name).Append(":**").Append('\n');
            foreach (var conflict in result.HardConflicts)
            {
                sb.Append("- ").Append(conflict.Reason).Append('\n');
            }
        }

        if (!anyConflicts)
        {
            sb.Append("No hard conflicts under the current profile.").Append('\n');
        }

        sb.Append('\n');
    }

    private static void AppendScoringTrace(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Scoring Trace").Append('\n').Append('\n');
        sb.Append("| Rank | Pattern | Eligible | Score (scaled) | Score |").Append('\n');
        sb.Append("|---|---|---|---|---|").Append('\n');

        foreach (var result in input.Outcome.Rankings)
        {
            var pattern = input.Rules.Pattern(result.Pattern);
            sb.Append("| ").Append(result.Rank.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(pattern.Name)
              .Append(" | ").Append(result.IsEligible ? "Yes" : "No")
              .Append(" | ").Append(result.ScoreScaled.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(result.Score.ToString("0.000000", CultureInfo.InvariantCulture))
              .Append(" |").Append('\n');
        }

        sb.Append('\n');

        foreach (var result in input.Outcome.Rankings)
        {
            var pattern = input.Rules.Pattern(result.Pattern);
            sb.Append("<details>").Append('\n');
            sb.Append("<summary>").Append(pattern.Name).Append(" — contribution detail</summary>").Append('\n').Append('\n');
            sb.Append("| Dimension | Capability | Demand | Shortfall | Weight (bp) | Weighted contribution | Rationale |").Append('\n');
            sb.Append("|---|---|---|---|---|---|---|").Append('\n');

            foreach (var contribution in result.Contributions)
            {
                var constraintDef = input.Rules.Constraint(contribution.Dimension);
                sb.Append("| ").Append(constraintDef.Title)
                  .Append(" | ").Append(contribution.Capability.ToString(CultureInfo.InvariantCulture))
                  .Append(" | ").Append(contribution.Demand.ToString(CultureInfo.InvariantCulture))
                  .Append(" | ").Append(contribution.Shortfall.ToString(CultureInfo.InvariantCulture))
                  .Append(" | ").Append(contribution.WeightBasisPoints.ToString(CultureInfo.InvariantCulture))
                  .Append(" | ").Append(contribution.WeightedContributionScaled.ToString(CultureInfo.InvariantCulture))
                  .Append(" | ").Append(contribution.Rationale)
                  .Append(" |").Append('\n');
            }

            sb.Append('\n').Append("</details>").Append('\n').Append('\n');
        }
    }

    private static void AppendShortlist(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Shortlist / Leading Option").Append('\n').Append('\n');

        var leading = input.Outcome.Rankings.FirstOrDefault(r => r.Rank == 1);
        if (leading is not null)
        {
            var pattern = input.Rules.Pattern(leading.Pattern);
            sb.Append("Leading under these constraints: **").Append(pattern.Name)
              .Append("** (rank 1, score ").Append(leading.Score.ToString("0.000000", CultureInfo.InvariantCulture)).Append(").").Append('\n').Append('\n');
        }

        if (input.Outcome.HasNearTie)
        {
            sb.Append("The top two patterns are effectively co-leading (within the near-tie margin); treat this as a close call rather than a clear winner.").Append('\n').Append('\n');
        }
    }

    private static void AppendRationale(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Rationale (Author-Written)").Append('\n').Append('\n');
        sb.Append("- **Name:** [[ Name the architecture pattern you are recommending. ]]").Append('\n');
        sb.Append("- **Why it fits:** [[ Explain why this pattern fits the scenario's constraints. ]]").Append('\n');
        sb.Append("- **Why not the alternatives:** [[ Explain why the other patterns were not chosen. ]]").Append('\n');
        sb.Append("- **What would change this:** [[ Describe what would need to change for this decision to be revisited. ]]").Append('\n').Append('\n');

        if (!string.IsNullOrEmpty(input.RationaleMarkdown))
        {
            sb.Append(input.RationaleMarkdown).Append('\n').Append('\n');
        }
    }

    private static void AppendSensitivityNotes(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Sensitivity Notes").Append('\n').Append('\n');

        var pivotal = input.Outcome.Sensitivity.Where(s => s.IsPivotal).ToList();
        if (pivotal.Count == 0)
        {
            sb.Append("[[ Note any sensitivity considerations relevant to this decision. ]]").Append('\n').Append('\n');
            return;
        }

        foreach (var entry in pivotal)
        {
            var constraintDef = input.Rules.Constraint(entry.Dimension);
            var baselineLabel = input.Rules.LevelLabel(entry.Dimension, entry.BaselineLevel);
            var testedLabel = input.Rules.LevelLabel(entry.Dimension, entry.TestedLevel);
            var baselinePattern = input.Rules.Pattern(entry.BaselineTop);
            var testedPattern = input.Rules.Pattern(entry.TestedTop);

            sb.Append("- Changing **").Append(constraintDef.Title).Append("** from ").Append(baselineLabel)
              .Append(" to ").Append(testedLabel).Append(" flips the leading pattern from ")
              .Append(baselinePattern.Name).Append(" to ").Append(testedPattern.Name).Append('.').Append('\n');
        }

        sb.Append('\n');
    }

    private static void AppendRisksAndMitigations(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Risks & Mitigations").Append('\n').Append('\n');

        var toShow = input.Outcome.HasNearTie
            ? input.Outcome.Rankings.Where(r => r.Rank is 1 or 2)
            : input.Outcome.Rankings.Where(r => r.Rank == 1);

        var anyRisk = false;
        foreach (var result in toShow)
        {
            if (result.ActiveRisks.IsDefaultOrEmpty)
            {
                continue;
            }

            var pattern = input.Rules.Pattern(result.Pattern);
            sb.Append("**").Append(pattern.Name).Append(":**").Append('\n');
            foreach (var risk in result.ActiveRisks)
            {
                anyRisk = true;
                sb.Append("- **").Append(risk.Title).Append("** (").Append(risk.Severity).Append("): ").Append(risk.Description).Append('\n');
                foreach (var mitigation in risk.Mitigations)
                {
                    sb.Append("  - Mitigation: ").Append(mitigation.Description).Append(" (Effort: ").Append(mitigation.Effort).Append(')').Append('\n');
                }
            }
        }

        if (!anyRisk)
        {
            sb.Append("No active risks recorded for the leading pattern(s) under this profile.").Append('\n');
        }

        sb.Append('\n');
    }

    private static void AppendConsequences(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Consequences").Append('\n').Append('\n');
        sb.Append("[[ Describe the broader consequences of adopting this pattern. ]]").Append('\n').Append('\n');

        var leading = input.Outcome.Rankings.FirstOrDefault(r => r.Rank == 1);
        if (leading is not null)
        {
            var pattern = input.Rules.Pattern(leading.Pattern);
            if (!pattern.Tradeoffs.IsDefaultOrEmpty)
            {
                sb.Append("Tradeoffs for the leading pattern (").Append(pattern.Name).Append("):").Append('\n');
                foreach (var tradeoff in pattern.Tradeoffs)
                {
                    var constraintDef = input.Rules.Constraint(tradeoff.Dimension);
                    sb.Append("- ").Append(constraintDef.Title).Append(" — Gain: ").Append(tradeoff.Gain)
                      .Append("; Cost: ").Append(tradeoff.Cost).Append('\n');
                }

                sb.Append('\n');
            }
        }
    }

    private static void AppendUnresolvedQuestions(StringBuilder sb, ExportInput input)
    {
        sb.Append("## Unresolved Questions").Append('\n').Append('\n');
        sb.Append("[[ List any unresolved questions or follow-ups before this decision is finalized. ]]").Append('\n').Append('\n');
    }

    private static void AppendFooter(StringBuilder sb, ExportInput input)
    {
        sb.Append("---").Append('\n');
        sb.Append("Engine ").Append(input.Outcome.Versions.EngineVersion)
          .Append(" · Rules ").Append(input.Outcome.Versions.RulesVersion)
          .Append(" · Digest `Sha256:").Append(ShortDigest(input.Outcome.ConfigDigest)).Append("…`").Append('\n');
    }
}
