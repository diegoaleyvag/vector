using Bunit;
using Vector.App.Components;

namespace Vector.App.Tests;

public class SensitivityPanelTests : VectorBunitContext
{
    /// <summary>
    /// 6. SensitivityPanel renders pivotal-change text for a scenario known to be pivotal, or the robust
    /// message otherwise. Rather than hardcoding which of the three real scenarios is pivotal, this
    /// drives the assertion from the engine's own (real) computed outcome for each scenario: a scenario
    /// is only "known to be pivotal" here if it has at least one single-step (distance-1) pivotal entry -
    /// MinFlipDistance can be finite without any single-step neighbor flipping the winner, in which case
    /// the panel still reports the numeric distance rather than either the pivotal list or the robust text.
    /// </summary>
    [Theory]
    [InlineData("scn.policy-assistant")]
    [InlineData("scn.structured-extraction")]
    [InlineData("scn.supervised-research")]
    public void SensitivityPanel_RendersPivotalTextOrRobustMessage(string scenarioId)
    {
        var state = GetState();
        state.LoadScenario(scenarioId);
        var outcome = state.Outcome;

        var cut = RenderComponent<SensitivityPanel>();

        if (outcome.MinFlipDistance == int.MaxValue)
        {
            Assert.Contains("robust to single-step changes", cut.Markup, StringComparison.Ordinal);
        }
        else if (outcome.Sensitivity.Any(e => e.IsPivotal))
        {
            Assert.Contains("would change the leading option from", cut.Markup, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("smallest single-dimension change", cut.Markup, StringComparison.Ordinal);
        }
    }
}
