using System.Text.Json.Serialization;

namespace Vector.Data.Dtos;

/// <summary>
/// Root DTO mirroring <c>vector-knowledge.v1.json</c> exactly. Enum-ish fields (dimension, polarity,
/// severity, effort, op, pattern id) are kept as raw strings so that <see cref="Mapping.KnowledgeMapper"/>
/// controls parsing and can throw a precise <see cref="DataMappingException"/> on any unknown value.
/// </summary>
public sealed class KnowledgeFileDto
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("rulesVersion")]
    public string RulesVersion { get; set; } = "";

    [JsonPropertyName("contentRevision")]
    public int ContentRevision { get; set; }

    [JsonPropertyName("engineCompatRange")]
    public string EngineCompatRange { get; set; } = "";

    [JsonPropertyName("nearTieMarginBasisPoints")]
    public int NearTieMarginBasisPoints { get; set; }

    [JsonPropertyName("constraints")]
    public List<ConstraintDto> Constraints { get; set; } = [];

    [JsonPropertyName("patterns")]
    public List<PatternDto> Patterns { get; set; } = [];

    [JsonPropertyName("advisories")]
    public List<AdvisoryDto> Advisories { get; set; } = [];

    [JsonPropertyName("mitigations")]
    public List<MitigationDto> Mitigations { get; set; } = [];

    [JsonPropertyName("scenarios")]
    public List<ScenarioDto> Scenarios { get; set; } = [];
}

public sealed class LevelDto
{
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("help")]
    public string Help { get; set; } = "";

    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = "";
}

public sealed class ConstraintDto
{
    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("polarity")]
    public string Polarity { get; set; } = "";

    [JsonPropertyName("help")]
    public string Help { get; set; } = "";

    [JsonPropertyName("maxLevel")]
    public int MaxLevel { get; set; }

    [JsonPropertyName("defaultWeightTier")]
    public int DefaultWeightTier { get; set; }

    [JsonPropertyName("levels")]
    public List<LevelDto> Levels { get; set; } = [];

    [JsonPropertyName("demandCurve")]
    public List<int> DemandCurve { get; set; } = [];
}

public sealed class TradeoffDto
{
    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = "";

    [JsonPropertyName("gain")]
    public string Gain { get; set; } = "";

    [JsonPropertyName("cost")]
    public string Cost { get; set; } = "";
}

public sealed class RiskDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "";

    [JsonPropertyName("relatedDimension")]
    public string? RelatedDimension { get; set; }

    [JsonPropertyName("activatesAtOrAboveLevel")]
    public int? ActivatesAtOrAboveLevel { get; set; }

    [JsonPropertyName("mitigationIds")]
    public List<string> MitigationIds { get; set; } = [];
}

public sealed class PatternDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("capabilities")]
    public List<int> Capabilities { get; set; } = [];

    [JsonPropertyName("rationales")]
    public List<string> Rationales { get; set; } = [];

    [JsonPropertyName("tradeoffs")]
    public List<TradeoffDto> Tradeoffs { get; set; } = [];

    [JsonPropertyName("risks")]
    public List<RiskDto> Risks { get; set; } = [];

    [JsonPropertyName("variantNotes")]
    public List<string> VariantNotes { get; set; } = [];
}

public sealed class MitigationDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("effort")]
    public string Effort { get; set; } = "";
}

public sealed class AdvisoryDto
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = "";

    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("mitigationIds")]
    public List<string> MitigationIds { get; set; } = [];
}

public sealed class ProfileSettingDto
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("weightTier")]
    public int WeightTier { get; set; }

    [JsonPropertyName("hard")]
    public bool Hard { get; set; }
}

public sealed class ScenarioDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("framing")]
    public string Framing { get; set; } = "";

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("profile")]
    public Dictionary<string, ProfileSettingDto> Profile { get; set; } = [];
}
