using Bunit;
using Vector.App.Components;

namespace Vector.App.Tests;

public class ScoreChartAndTableTests : VectorBunitContext
{
    /// <summary>
    /// 5. ScoreChart has role="img" + aria-label, and a sibling &lt;table&gt; with a &lt;caption&gt;
    /// exists (the textual alternative / source of truth).
    /// </summary>
    [Fact]
    public void ScoreChart_HasImgRoleAndAriaLabel_WithScoreTableCaptionAsTextualAlternative()
    {
        var cut = RenderComponent<ComparisonView>();

        var chart = cut.Find("[role='img']");
        var ariaLabel = chart.GetAttribute("aria-label");
        Assert.False(string.IsNullOrWhiteSpace(ariaLabel));

        var table = cut.Find("table.score-table");
        var caption = table.QuerySelector("caption");
        Assert.NotNull(caption);
        Assert.False(string.IsNullOrWhiteSpace(caption!.TextContent));
    }
}
