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

    private const string MethodologyPermalink =
        "https://github.com/diegoaleyvag/vector/blob/384dd00294ffec38f215b989bb9335404793a0d8/docs/decision-method.md";

    [Fact]
    public void ShellRetainsReturnAndMethodologyLinks_WithPublicRepositoryLink()
    {
        var cut = RenderComponent<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => { })));

        Assert.Contains("Return to studio", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Methodology", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Source repository", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("https://github.com/diegoaleyvag/vector", cut.Markup, StringComparison.Ordinal);

        var demoLinks = cut.FindAll("a")
            .Where(a => a.TextContent.Trim() == "Stable public demo")
            .ToList();

        Assert.Single(demoLinks);
        Assert.Equal("https://five-decisions-vector.vercel.app", demoLinks[0].GetAttribute("href"));
    }

    [Fact]
    public void ShellMethodologyLinks_PointToExactPublicPermalink_AndAreAccessible()
    {
        var cut = RenderComponent<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => { })));

        var methodologyLinks = cut.FindAll("a")
            .Where(a => a.TextContent.Trim() == "Methodology")
            .ToList();

        Assert.Equal(2, methodologyLinks.Count);
        Assert.All(methodologyLinks, link =>
        {
            Assert.Equal(MethodologyPermalink, link.GetAttribute("href"));
            Assert.Equal("_blank", link.GetAttribute("target"));
            Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
        });
    }
}
