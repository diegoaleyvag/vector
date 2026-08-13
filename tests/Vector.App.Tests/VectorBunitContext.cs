using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Vector.App.Services;
using Vector.Data;
using Vector.Domain;
using Vector.Engine;

namespace Vector.App.Tests;

/// <summary>
/// Shared bUnit fixture for Vector.App component tests. Registers the real RuleSet/Scenarios/
/// DecisionEngine/StudioState - loaded from the actual content JSON copied into the test output,
/// exactly as Program.cs does at startup - and puts JSInterop in loose mode so incidental calls (e.g.
/// ElementReference.FocusAsync after a scenario loads) don't fail tests that aren't about JS interop.
/// Tests that assert a specific JS call configure their own handler on top of this.
/// </summary>
public abstract class VectorBunitContext : BunitContext
{
    protected RuleSet Rules { get; }

    protected IReadOnlyList<Scenario> Scenarios { get; }

    protected VectorBunitContext()
    {
        var (rules, scenarios) = KnowledgeLoader.Parse(ContentFile.ReadAllText());
        Rules = rules;
        Scenarios = scenarios;

        Services.AddSingleton(rules);
        Services.AddSingleton(scenarios);
        Services.AddSingleton<IDecisionEngine, DecisionEngine>();
        Services.AddScoped<StudioState>();

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Returns the single StudioState instance for this test's DI scope.</summary>
    protected StudioState GetState() => Services.GetRequiredService<StudioState>();

    /// <summary>
    /// Renders a parameterless component. Deliberately hides the obsolete <c>BunitContext.RenderComponent</c>
    /// (bUnit 2.9 renamed it to <c>Render</c>) so test call sites can keep using the familiar name.
    /// </summary>
    protected new IRenderedComponent<TComponent> RenderComponent<TComponent>()
        where TComponent : IComponent =>
        Render<TComponent>((ComponentParameterCollectionBuilder<TComponent> parameters) => { });

    /// <summary>Renders a component with parameters. See remarks on the parameterless overload above.</summary>
    protected new IRenderedComponent<TComponent> RenderComponent<TComponent>(Action<ComponentParameterCollectionBuilder<TComponent>> parameterBuilder)
        where TComponent : IComponent =>
        Render(parameterBuilder);
}
