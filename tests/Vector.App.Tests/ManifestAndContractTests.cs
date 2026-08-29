using System.Text.Json;
using System.Security.Cryptography;

namespace Vector.App.Tests;

public sealed class ManifestAndContractTests
{
    private const string ExpectedSchemaSha256 =
        "bd6af84d9b07a61abf39319e9a1dc2e4d0c0482b022f942a5b1b350b0eab1418";

    [Fact]
    public void VendoredPortableSchema_MatchesFrozenContractBytes()
    {
        var schemaBytes = File.ReadAllBytes(SchemaPath);
        var actualHash = Convert.ToHexString(SHA256.HashData(schemaBytes)).ToLowerInvariant();

        Assert.Equal(ExpectedSchemaSha256, actualHash);
    }

    [Fact]
    public void PortfolioManifest_ValidatesAgainstVendoredPortableSchema()
    {
        using var schema = JsonDocument.Parse(File.ReadAllBytes(SchemaPath));
        using var manifest = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "portfolio.project.json")));

        var errors = PortableJsonSchemaValidator.Validate(manifest.RootElement, schema.RootElement);

        Assert.Empty(errors);
    }

    [Fact]
    public void AppStylesheet_VendorsApprovedBrandVersionAndHashes()
    {
        var css = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "css", "app.css"));

        Assert.Contains("BRAND_VERSION=1.0.0", css, StringComparison.Ordinal);
        Assert.Contains("tokens=edab728f0eb029e902a50ec8d3d5df44f99b5dd5dbcbf4a554efa1b4ffbf2668", css, StringComparison.Ordinal);
        Assert.Contains("shell=8406b1c70038f69fcb86f01dbb3505d3ae1d2b28e3d2928e061e7a75b1ee8b70", css, StringComparison.Ordinal);
        Assert.Contains("schema=bd6af84d9b07a61abf39319e9a1dc2e4d0c0482b022f942a5b1b350b0eab1418", css, StringComparison.Ordinal);
        Assert.Contains("--fd-field: oklch(97.4% 0.012 165)", css, StringComparison.Ordinal);
        Assert.Contains("--fd-graphite: oklch(19% 0.014 256)", css, StringComparison.Ordinal);
        Assert.Contains("--fd-survey: oklch(52% 0.135 246)", css, StringComparison.Ordinal);
        Assert.Contains("--fd-signal: oklch(61.5% 0.16 68)", css, StringComparison.Ordinal);
    }

    private static string SchemaPath => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "portfolio.project.schema.json");
}
