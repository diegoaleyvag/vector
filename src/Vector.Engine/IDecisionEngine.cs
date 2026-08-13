using Vector.Domain;

namespace Vector.Engine;

/// <summary>Evaluates a scenario's constraint profile against a rule set to produce a deterministic, ranked decision outcome.</summary>
public interface IDecisionEngine
{
    /// <summary>
    /// Scores every pattern in <paramref name="rules"/> against <paramref name="scenario"/>'s constraint profile,
    /// applies hard-constraint gating, ranks the patterns, and computes near-tie and sensitivity signals.
    /// </summary>
    /// <param name="scenario">The scenario to evaluate. Only <see cref="Scenario.Profile"/> affects the result; other fields are metadata.</param>
    /// <param name="rules">The rule content (constraints, patterns, advisories) to evaluate against.</param>
    /// <returns>A fully deterministic <see cref="DecisionOutcome"/>.</returns>
    DecisionOutcome Evaluate(Scenario scenario, RuleSet rules);
}
