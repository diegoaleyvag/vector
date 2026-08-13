using Bunit;
using Vector.App.Components;
using Vector.Domain;

namespace Vector.App.Tests;

public class HardConflictTests : VectorBunitContext
{
    /// <summary>
    /// 3. A profile that induces a hard conflict (set a dimension hard where a pattern can't meet it)
    /// renders the HardConflictPanel as persistent DOM (not a toast) marking that pattern non-viable.
    /// </summary>
    [Fact]
    public void HardConflict_RendersPersistentPanel_MarkingPatternNonViable()
    {
        var state = GetState();

        // DataSensitivity level 4 ("Restricted", demand 4) as a hard requirement: ToolUsingAgent's
        // capability of 1 cannot meet it, so it must be vetoed with a recorded HardConflict.
        state.SetLevel(ConstraintDimension.DataSensitivity, 4);
        state.SetHard(ConstraintDimension.DataSensitivity, true);

        var cut = RenderComponent<ComparisonView>();

        var panel = cut.Find("[role='note']");
        Assert.Contains("Cannot meet a hard requirement", panel.TextContent, StringComparison.Ordinal);

        var agentName = Rules.Pattern(PatternId.ToolUsingAgent).Name;
        Assert.Contains(agentName, panel.TextContent, StringComparison.Ordinal);

        // Persistent: the panel is a static conditional block, not a timed/dismissible toast - it
        // stays in the DOM across re-renders.
        cut.Render(Microsoft.AspNetCore.Components.ParameterView.Empty);
        Assert.NotNull(cut.Find("[role='note']"));
    }
}
