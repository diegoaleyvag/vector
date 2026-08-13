using Vector.Domain;

namespace Vector.Data.Export;

/// <summary>
/// Everything <see cref="MarkdownExporter.Export"/> needs to render an ADR-style markdown document.
/// The date is injected (rather than read from the clock) so exports are fully deterministic and
/// testable. When <paramref name="Scenario"/> is null the export describes a custom, ad-hoc profile
/// instead of a named scenario.
/// </summary>
/// <param name="Scenario">The named scenario evaluated, or null if the user built a custom profile.</param>
/// <param name="Profile">The constraint profile that was evaluated (the scenario's profile, or the custom one).</param>
/// <param name="Rules">The rule content used, for dimension/level/pattern titles.</param>
/// <param name="Outcome">The engine's evaluation of <paramref name="Profile"/> against <paramref name="Rules"/>.</param>
/// <param name="RationaleMarkdown">Author-written rationale markdown; may be empty. Never used to auto-fill the <c>[[ ]]</c> prompts.</param>
/// <param name="FixedDate">A fixed, injected date string (e.g. "2026-08-13") so exports are byte-identical across runs.</param>
public sealed record ExportInput(
    Scenario? Scenario,
    ConstraintProfile Profile,
    RuleSet Rules,
    DecisionOutcome Outcome,
    string RationaleMarkdown,
    string FixedDate);
