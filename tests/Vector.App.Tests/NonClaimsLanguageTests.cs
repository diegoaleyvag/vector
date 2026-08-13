using Bunit;
using Microsoft.AspNetCore.Components;
using Vector.App.Layout;
using Vector.App.Pages;

namespace Vector.App.Tests;

public class NonClaimsLanguageTests : VectorBunitContext
{
    /// <summary>
    /// 12. The non-claims footer text is present (in MainLayout), and no "best/correct architecture"
    /// phrasing appears anywhere in the rendered studio page.
    /// </summary>
    [Fact]
    public void NonClaimsFooter_IsPresent_AndForbiddenPhrasingNeverAppears()
    {
        var layout = RenderComponent<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => { })));

        Assert.Contains("does not certify or endorse any architecture", layout.Markup, StringComparison.Ordinal);

        var page = RenderComponent<StudioPage>();
        Assert.DoesNotContain("best architecture", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correct architecture", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recommended architecture", page.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
