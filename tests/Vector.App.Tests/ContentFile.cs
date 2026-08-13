namespace Vector.App.Tests;

/// <summary>Locates the real content JSON copied into the test output directory by the csproj's Content item.</summary>
internal static class ContentFile
{
    public static string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "vector-knowledge.v1.json");

    public static string ReadAllText() => File.ReadAllText(Path);
}
