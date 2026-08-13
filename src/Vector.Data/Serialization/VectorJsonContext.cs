using System.Text.Json.Serialization;
using Vector.Data.Dtos;

namespace Vector.Data.Serialization;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for every JSON shape Vector.Data reads or
/// writes. Vector.Data owns ALL System.Text.Json usage in the solution; serialization is fully
/// reflection-free (metadata + serialization code generated at compile time).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(KnowledgeFileDto))]
[JsonSerializable(typeof(SharePayloadDto))]
public partial class VectorJsonContext : JsonSerializerContext
{
}
