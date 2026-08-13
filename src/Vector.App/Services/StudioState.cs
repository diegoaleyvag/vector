using System.Collections.Immutable;
using Vector.Data.Sharing;
using Vector.Domain;
using Vector.Engine;

namespace Vector.App.Services;

/// <summary>
/// Scoped, observable holder of the studio's working state: the loaded rule content, the available
/// scenarios, the currently edited constraint profile, the author's free-text rationale, and the
/// engine's evaluation of that profile. Every mutation recomputes <see cref="Outcome"/> synchronously
/// (the engine is pure and cheap) and raises <see cref="OnChange"/> so subscribed components can
/// re-render. This class has no dependency on Blazor beyond the plain <see cref="OnChange"/> event.
/// </summary>
public sealed class StudioState
{
    private readonly IDecisionEngine _engine;

    /// <summary>The loaded rule content (constraints, patterns, advisories).</summary>
    public RuleSet Rules { get; }

    /// <summary>The authored scenarios available to load.</summary>
    public IReadOnlyList<Scenario> Scenarios { get; }

    /// <summary>
    /// The id of the scenario the working profile currently matches, or null if the profile is a blank
    /// start or has been edited away from any authored scenario.
    /// </summary>
    public string? SelectedScenarioId { get; private set; }

    /// <summary>The constraint profile currently being edited.</summary>
    public ConstraintProfile Profile { get; private set; }

    /// <summary>Author-written rationale markdown. Never auto-filled by Vector; seeded empty.</summary>
    public string RationaleMarkdown { get; private set; } = string.Empty;

    /// <summary>The engine's evaluation of <see cref="Profile"/> against <see cref="Rules"/>.</summary>
    public DecisionOutcome Outcome { get; private set; }

    /// <summary>Raised after any mutation that changes <see cref="Profile"/>, <see cref="Outcome"/>, or <see cref="RationaleMarkdown"/>.</summary>
    public event Action? OnChange;

    public StudioState(IDecisionEngine engine, RuleSet rules, IReadOnlyList<Scenario> scenarios)
    {
        _engine = engine;
        Rules = rules;
        Scenarios = scenarios;
        Profile = BuildBlankProfile(rules);
        Outcome = Evaluate();
    }

    /// <summary>Replaces the working profile with the named scenario's profile.</summary>
    public void LoadScenario(string id)
    {
        var scenario = Scenarios.FirstOrDefault(s => s.Id == id);
        if (scenario is null)
        {
            return;
        }

        Profile = scenario.Profile;
        SelectedScenarioId = id;
        Recompute();
    }

    /// <summary>Resets the working profile to a blank starting point (level 0, default weight tier, not hard, for every dimension).</summary>
    public void StartBlank()
    {
        Profile = BuildBlankProfile(Rules);
        SelectedScenarioId = null;
        Recompute();
    }

    /// <summary>Sets the selected level for a dimension, clamped to the dimension's valid level range.</summary>
    public void SetLevel(ConstraintDimension dimension, int levelIndex)
    {
        var maxLevel = Rules.Constraint(dimension).Levels.Length - 1;
        var clamped = Math.Clamp(levelIndex, 0, maxLevel);
        var current = Profile[dimension];
        ReplaceSetting(current with { LevelIndex = clamped });
    }

    /// <summary>Sets the weight tier for a dimension.</summary>
    public void SetWeightTier(ConstraintDimension dimension, int weightTier)
    {
        var current = Profile[dimension];
        ReplaceSetting(current with { WeightTier = weightTier });
    }

    /// <summary>Marks (or unmarks) a dimension as a hard requirement.</summary>
    public void SetHard(ConstraintDimension dimension, bool isHard)
    {
        var current = Profile[dimension];
        ReplaceSetting(current with { IsHard = isHard });
    }

    /// <summary>Updates the author-written rationale text. Never called by anything derived from the engine's output.</summary>
    public void SetRationale(string rationaleMarkdown)
    {
        RationaleMarkdown = rationaleMarkdown;
        OnChange?.Invoke();
    }

    /// <summary>Replaces the working profile from a decoded share payload.</summary>
    public void HydrateFromShare(SharePayload payload)
    {
        Profile = ShareCodec.ToProfile(payload);
        SelectedScenarioId = payload.ScenarioId;
        Recompute();
    }

    /// <summary>Builds a share payload for the current working profile.</summary>
    public SharePayload ToSharePayload() => ShareCodec.FromProfile(Profile, Rules.RulesVersion, SelectedScenarioId);

    private void ReplaceSetting(ConstraintSetting updated)
    {
        var builder = ImmutableArray.CreateBuilder<ConstraintSetting>(Profile.Settings.Length);
        foreach (var setting in Profile.Settings)
        {
            builder.Add(setting.Dimension == updated.Dimension ? updated : setting);
        }

        Profile = new ConstraintProfile(builder.ToImmutable());
        SelectedScenarioId = null;
        Recompute();
    }

    private void Recompute()
    {
        Outcome = Evaluate();
        OnChange?.Invoke();
    }

    private DecisionOutcome Evaluate()
    {
        var workingScenario = new Scenario("_working", "Working profile", null, ImmutableArray<string>.Empty, Profile);
        return _engine.Evaluate(workingScenario, Rules);
    }

    private static ConstraintProfile BuildBlankProfile(RuleSet rules)
    {
        var dimensions = Enum.GetValues<ConstraintDimension>().OrderBy(d => (int)d);
        var builder = ImmutableArray.CreateBuilder<ConstraintSetting>(8);
        foreach (var dimension in dimensions)
        {
            var defaultWeightTier = rules.Constraint(dimension).DefaultWeightTier;
            builder.Add(new ConstraintSetting(dimension, LevelIndex: 0, WeightTier: defaultWeightTier, IsHard: false));
        }

        return new ConstraintProfile(builder.ToImmutable());
    }
}
