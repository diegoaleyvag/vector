using Bunit;
using Microsoft.AspNetCore.Components;
using Vector.App.Layout;
using Vector.App.Pages;

namespace Vector.App.Tests;

public sealed class PresentationFlowTests : VectorBunitContext
{
    [Fact]
    public void StudioPresentsScenarioFirst_WithProgressiveConstraints_AndVisibleDecisionSummary()
    {
        var cut = RenderComponent<StudioPage>();
        var markup = cut.Markup;

        Assert.InRange(markup.IndexOf("scenario-picker", StringComparison.Ordinal), 0, int.MaxValue);
        Assert.True(
            markup.IndexOf("scenario-picker", StringComparison.Ordinal)
                < markup.IndexOf("comparison-view", StringComparison.Ordinal));
        Assert.Contains("advanced-constraints", markup, StringComparison.Ordinal);
        Assert.Contains("5 more dimensions", markup, StringComparison.Ordinal);
        Assert.Contains("decision-summary", markup, StringComparison.Ordinal);
        Assert.Contains("trace-disclosure", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellRetainsReturnAndMethodologyLinks_WithConservativeRepositoryStatus()
    {
        var cut = RenderComponent<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => { })));

        Assert.Contains("Return to studio", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Methodology", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Repository pending C2", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Demo pending C2/C3", cut.Markup, StringComparison.Ordinal);
    }
}
