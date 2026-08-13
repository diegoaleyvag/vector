using System.Text.Json.Serialization;

namespace Vector.Data.Dtos;

/// <summary>
/// Wire format for <see cref="Sharing.ShareCodec"/>: the minimal, non-free-text payload embedded in a
/// share link. Arrays are in CANONICAL dimension order (index 0 = DataSensitivity ... 7 = OperationalMaturity).
/// </summary>
public sealed class SharePayloadDto
{
    [JsonPropertyName("scenarioId")]
    public string? ScenarioId { get; set; }

    [JsonPropertyName("levels")]
    public int[] Levels { get; set; } = [];

    [JsonPropertyName("weightTiers")]
    public int[] WeightTiers { get; set; } = [];

    [JsonPropertyName("hard")]
    public bool[] Hard { get; set; } = [];

    [JsonPropertyName("rulesVersion")]
    public string RulesVersion { get; set; } = "";
}
