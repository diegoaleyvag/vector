using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Vector.App;
using Vector.App.Services;
using Vector.Data;
using Vector.Engine;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Load the decision content BEFORE running so the rest of the app can treat it as always-present.
// A failure here (missing file, malformed JSON, or content the domain model cannot represent) is
// handled by registering a StartupError instead of the studio services; App.razor renders a static
// error screen rather than letting the studio start against absent/invalid data (fail-closed).
using (var startupHttp = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) })
{
    try
    {
        var json = await startupHttp.GetStringAsync("data/vector-knowledge.v1.json");
        var (rules, scenarios) = KnowledgeLoader.Parse(json);
        builder.Services.AddSingleton(rules);
        builder.Services.AddSingleton(scenarios);
        builder.Services.AddSingleton<IDecisionEngine, DecisionEngine>();
        builder.Services.AddScoped<StudioState>();
    }
    catch (Exception ex)
    {
        builder.Services.AddSingleton(new StartupError(ex.Message));
    }
}

await builder.Build().RunAsync();
