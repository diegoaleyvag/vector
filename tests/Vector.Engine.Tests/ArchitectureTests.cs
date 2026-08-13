using System.Reflection;
using Vector.Domain;

namespace Vector.Engine.Tests;

/// <summary>Invariant 17: neither Vector.Domain nor Vector.Engine may reference Blazor, ASP.NET Core, System.Text.Json, or Newtonsoft.Json.</summary>
public class ArchitectureTests
{
    private static readonly string[] ForbiddenSubstrings = ["Blazor", "AspNetCore", "System.Text.Json", "Newtonsoft"];

    [Fact]
    public void DomainAssembly_HasNoForbiddenReferences()
    {
        AssertNoForbiddenReferences(typeof(RuleSet).Assembly);
    }

    [Fact]
    public void EngineAssembly_HasNoForbiddenReferences()
    {
        AssertNoForbiddenReferences(typeof(DecisionEngine).Assembly);
    }

    private static void AssertNoForbiddenReferences(Assembly assembly)
    {
        var referencedNames = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();

        foreach (var forbidden in ForbiddenSubstrings)
        {
            var match = referencedNames.FirstOrDefault(name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            Assert.True(match is null, $"{assembly.GetName().Name} should not reference any assembly containing '{forbidden}', but found '{match}'.");
        }
    }
}
