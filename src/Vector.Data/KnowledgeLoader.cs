using System.Text.Json;
using Vector.Data.Mapping;
using Vector.Data.Serialization;
using Vector.Domain;

namespace Vector.Data;

/// <summary>
/// Deserializes the versioned knowledge content JSON (via the source-generated
/// <see cref="VectorJsonContext"/>) and maps it into the domain's <see cref="RuleSet"/> and
/// <see cref="Scenario"/> collection. Pure: takes JSON text in, returns domain objects, no I/O.
/// </summary>
public static class KnowledgeLoader
{
    /// <summary>
    /// Parses the knowledge content JSON. Throws <see cref="DataMappingException"/> if the JSON is
    /// well-formed but contains content the domain model cannot represent (unknown enum member, bad
    /// mitigation-id reference, mismatched array lengths, etc.).
    /// </summary>
    public static (RuleSet Rules, IReadOnlyList<Scenario> Scenarios) Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        Dtos.KnowledgeFileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, VectorJsonContext.Default.KnowledgeFileDto);
        }
        catch (JsonException ex)
        {
            throw new DataMappingException($"The knowledge file JSON is malformed: {ex.Message}", ex);
        }

        if (dto is null)
        {
            throw new DataMappingException("The knowledge file JSON deserialized to null.");
        }

        var rules = KnowledgeMapper.ToRuleSet(dto);
        var scenarios = KnowledgeMapper.ToScenarios(dto);
        return (rules, scenarios);
    }
}
