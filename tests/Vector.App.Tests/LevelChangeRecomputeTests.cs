using Bunit;
using Vector.App.Pages;
using Vector.Domain;

namespace Vector.App.Tests;

public class LevelChangeRecomputeTests : VectorBunitContext
{
    /// <summary>2. Changing a level input recomputes: the ScoreTable's cell text for a pattern changes.</summary>
    [Fact]
    public void ChangingLevelInput_RecomputesScoreTableCellForAPattern()
    {
        var cut = RenderComponent<StudioPage>();
        var patternName = Rules.Pattern(PatternId.ToolUsingAgent).Name;
        var before = FindScoreCell(cut, patternName);

        // DataSensitivity level 4 ("Restricted", demand 4) versus the blank profile's level 0 (demand 0)
        // changes ToolUsingAgent's shortfall on this dimension (capability 1), so its score must change.
        cut.Find($"#constraint-{ConstraintDimension.DataSensitivity}-number").Change("4");

        cut.WaitForAssertion(() => Assert.NotEqual(before, FindScoreCell(cut, patternName)));
    }

    private static string FindScoreCell(IRenderedComponent<StudioPage> cut, string patternName)
    {
        foreach (var row in cut.FindAll("table.score-table tbody tr"))
        {
            if (row.TextContent.Contains(patternName, StringComparison.Ordinal))
            {
                return row.QuerySelectorAll("td")[1].TextContent;
            }
        }

        throw new InvalidOperationException($"No score row found for '{patternName}'.");
    }
}
