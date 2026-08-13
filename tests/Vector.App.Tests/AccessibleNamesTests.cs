using Bunit;
using Vector.App.Pages;

namespace Vector.App.Tests;

public class AccessibleNamesTests : VectorBunitContext
{
    /// <summary>11. Every interactive control exposes an accessible name (an associated label, or an aria-label).</summary>
    [Fact]
    public void EveryInteractiveControl_HasAnAccessibleName()
    {
        var cut = RenderComponent<StudioPage>();

        foreach (var input in cut.FindAll("input, select, textarea"))
        {
            var id = input.GetAttribute("id");
            var ariaLabel = input.GetAttribute("aria-label");
            var hasLabel = !string.IsNullOrEmpty(id) && cut.FindAll($"label[for='{id}']").Count > 0;

            Assert.True(
                !string.IsNullOrWhiteSpace(ariaLabel) || hasLabel,
                $"<{input.TagName} id='{id}'> has no associated <label> and no aria-label.");
        }

        foreach (var button in cut.FindAll("button"))
        {
            var ariaLabel = button.GetAttribute("aria-label");
            var hasText = !string.IsNullOrWhiteSpace(button.TextContent);

            Assert.True(
                hasText || !string.IsNullOrWhiteSpace(ariaLabel),
                "A <button> has neither visible text nor an aria-label.");
        }
    }
}
