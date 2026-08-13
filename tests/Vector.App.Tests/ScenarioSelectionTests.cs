using Bunit;
using Vector.App.Pages;

namespace Vector.App.Tests;

public class ScenarioSelectionTests : VectorBunitContext
{
    /// <summary>1. Selecting a scenario populates 8 ConstraintRows with that scenario's level names.</summary>
    [Fact]
    public void SelectingScenario_PopulatesEightConstraintRowsWithItsLevelNames()
    {
        var cut = RenderComponent<StudioPage>();
        var scenario = Scenarios.Single(s => s.Id == "scn.policy-assistant");

        // Scenario ids contain dots (e.g. "scn.policy-assistant"), which are not valid inside an
        // unescaped CSS id selector, so match on the id attribute value instead.
        cut.Find($"[id='scenario-{scenario.Id}']").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(8, cut.FindAll("fieldset").Count);

            foreach (var setting in scenario.Profile.Settings)
            {
                var expectedLevelName = Rules.LevelLabel(setting.Dimension, setting.LevelIndex);
                var rangeInput = cut.Find($"#constraint-{setting.Dimension}-range");
                Assert.Equal(expectedLevelName, rangeInput.GetAttribute("aria-valuetext"));
            }
        });
    }
}
