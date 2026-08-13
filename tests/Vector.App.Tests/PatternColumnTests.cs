using Bunit;
using Vector.App.Components;

namespace Vector.App.Tests;

public class PatternColumnTests : VectorBunitContext
{
    /// <summary>4. Expanding a PatternColumn shows 8 contribution rows each with a non-empty rationale.</summary>
    [Fact]
    public void PatternColumn_ShowsEightContributionRows_EachWithNonEmptyRationale()
    {
        var state = GetState();
        state.LoadScenario("scn.policy-assistant");
        var result = state.Outcome.Rankings[0];

        var cut = RenderComponent<PatternColumn>(parameters => parameters
            .Add(p => p.Result, result)
            .Add(p => p.Rules, Rules));

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(8, rows.Count);

        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td");
            var rationale = cells[^1].TextContent;
            Assert.False(string.IsNullOrWhiteSpace(rationale));
        }
    }
}
