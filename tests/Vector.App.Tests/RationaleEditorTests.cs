using Bunit;
using Vector.App.Components;

namespace Vector.App.Tests;

public class RationaleEditorTests : VectorBunitContext
{
    /// <summary>7. RationaleEditor textarea is empty on load and the app never writes to it from engine output.</summary>
    [Fact]
    public void RationaleEditor_IsEmptyOnLoad_AndNeverAutoFilledByEngineOutput()
    {
        var cut = RenderComponent<RationaleEditor>();
        var textarea = cut.Find("textarea");
        Assert.Equal(string.Empty, textarea.TextContent);

        var state = GetState();
        Assert.Equal(string.Empty, state.RationaleMarkdown);

        // Recomputing the outcome (by loading a scenario) must not touch the author's rationale text.
        state.LoadScenario(Scenarios[0].Id);
        Assert.Equal(string.Empty, state.RationaleMarkdown);
    }
}
