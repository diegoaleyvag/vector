using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Vector.App.Pages;

namespace Vector.App.Tests;

public class BadShareFragmentTests : VectorBunitContext
{
    /// <summary>
    /// 9. A bad share fragment ("#v9.zzz") yields the blank profile plus a visible role="status" banner
    /// with the correct reason text (an unsupported version prefix).
    /// </summary>
    [Fact]
    public void BadShareFragment_YieldsBlankProfile_AndVisibleStatusBanner()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.BaseUri + "#v9.zzz");

        var cut = RenderComponent<StudioPage>();

        var banner = cut.Find("[role='status']");
        Assert.Contains("different version", banner.TextContent, StringComparison.OrdinalIgnoreCase);

        var state = GetState();
        Assert.Null(state.SelectedScenarioId);
        Assert.All(state.Profile.Settings, s => Assert.Equal(0, s.LevelIndex));
        Assert.All(state.Profile.Settings, s => Assert.False(s.IsHard));
        Assert.All(state.Profile.Settings, s => Assert.Equal(Rules.Constraint(s.Dimension).DefaultWeightTier, s.WeightTier));
    }
}
