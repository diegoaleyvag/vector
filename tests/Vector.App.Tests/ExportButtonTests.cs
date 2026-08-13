using Bunit;
using Vector.App.Components;

namespace Vector.App.Tests;

public class ExportButtonTests : VectorBunitContext
{
    /// <summary>
    /// 10. Export action invokes the JS downloadFile interop (asserted via bUnit's JSInterop) with a
    /// .md filename, and the produced markdown contains the config digest and the [[ ]] rationale prompts.
    /// </summary>
    [Fact]
    public void ExportButton_InvokesDownloadFile_WithMarkdownFilenameAndContent()
    {
        JSInterop.SetupVoid("downloadFile", _ => true);

        var cut = RenderComponent<ExportButton>();
        cut.Find("button").Click();

        var invocation = JSInterop.VerifyInvoke("downloadFile");
        var filename = Assert.IsType<string>(invocation.Arguments[0]);
        var markdown = Assert.IsType<string>(invocation.Arguments[1]);

        Assert.EndsWith(".md", filename, StringComparison.Ordinal);
        Assert.Contains("Sha256:", markdown, StringComparison.Ordinal);
        Assert.Contains("[[", markdown, StringComparison.Ordinal);
        Assert.Contains("]]", markdown, StringComparison.Ordinal);
    }
}
