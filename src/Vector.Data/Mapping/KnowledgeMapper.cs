using System.Collections.Immutable;
using Vector.Data.Dtos;
using Vector.Domain;
using Vector.Engine;

namespace Vector.Data.Mapping;

/// <summary>
/// Pure, static mapping from the wire-shaped <see cref="KnowledgeFileDto"/> to the domain's
/// <see cref="RuleSet"/> and <see cref="Scenario"/> collection. Every enum-ish string, mitigation-id
/// reference, and array length is validated here; any problem throws a <see cref="DataMappingException"/>
/// naming the offending field and value rather than letting an obscure exception surface later.
/// </summary>
public static class KnowledgeMapper
{
    /// <summary>Maps the content file's constraints, patterns, and advisories into a domain <see cref="RuleSet"/>.</summary>
    public static RuleSet ToRuleSet(KnowledgeFileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var mitigationPool = BuildMitigationPool(dto.Mitigations);

        var constraints = dto.Constraints
            .Select(MapConstraint)
            .ToImmutableArray();

        var patterns = dto.Patterns
            .Select(p => MapPattern(p, mitigationPool))
            .ToImmutableArray();

        var advisories = dto.Advisories
            .Select(a => MapAdvisory(a, mitigationPool))
            .ToImmutableArray();

        var rulesContentHash = DigestCalculator.ComputeRulesContentHash(constraints, patterns, dto.NearTieMarginBasisPoints);

        try
        {
            return new RuleSet(
                dto.RulesVersion,
                rulesContentHash,
                dto.EngineCompatRange,
                constraints,
                patterns,
                advisories,
                dto.NearTieMarginBasisPoints);
        }
        catch (ArgumentException ex)
        {
            throw new DataMappingException($"Invalid rule set content: {ex.Message}", ex);
        }
    }

    /// <summary>Maps the content file's scenarios into domain <see cref="Scenario"/> values.</summary>
    public static IReadOnlyList<Scenario> ToScenarios(KnowledgeFileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return dto.Scenarios.Select(MapScenario).ToList();
    }

    private static Dictionary<string, Mitigation> BuildMitigationPool(List<MitigationDto> mitigations)
    {
        var pool = new Dictionary<string, Mitigation>(StringComparer.Ordinal);
        foreach (var m in mitigations)
        {
            if (string.IsNullOrEmpty(m.Id))
            {
                throw new DataMappingException("A mitigation pool entry is missing an 'id'.");
            }

            var effort = ParseEnum<MitigationEffort>(m.Effort, $"mitigations['{m.Id}'].effort");

            if (!pool.TryAdd(m.Id, new Mitigation(m.Id, m.Description, effort)))
            {
                throw new DataMappingException($"Duplicate mitigation id '{m.Id}' in the mitigation pool.");
            }
        }

        return pool;
    }

    private static ImmutableArray<Mitigation> ResolveMitigations(
        IReadOnlyList<string> ids, Dictionary<string, Mitigation> pool, string context)
    {
        var builder = ImmutableArray.CreateBuilder<Mitigation>(ids.Count);
        foreach (var id in ids)
        {
            if (!pool.TryGetValue(id, out var mitigation))
            {
                throw new DataMappingException($"Unknown mitigation id '{id}' referenced by {context}.");
            }

            builder.Add(mitigation);
        }

        return builder.ToImmutable();
    }

    private static ConstraintDefinition MapConstraint(ConstraintDto dto)
    {
        var dimension = ParseEnum<ConstraintDimension>(dto.Dimension, "constraint.dimension");
        var polarity = ParseEnum<ConstraintPolarity>(dto.Polarity, $"constraints['{dto.Dimension}'].polarity");

        if (dto.DemandCurve.Count != dto.Levels.Count)
        {
            throw new DataMappingException(
                $"constraints['{dto.Dimension}'] has demandCurve.length ({dto.DemandCurve.Count}) != levels.length ({dto.Levels.Count}).");
        }

        var levels = dto.Levels
            .Select(l => new LevelMetadata(l.Value, l.Name, l.Help, l.Evidence))
            .ToImmutableArray();

        var demandCurve = dto.DemandCurve.ToImmutableArray();
        foreach (var demand in demandCurve)
        {
            if (demand is < 0 or > 4)
            {
                throw new DataMappingException(
                    $"constraints['{dto.Dimension}'].demandCurve contains out-of-range value {demand} (must be 0..4).");
            }
        }

        try
        {
            return new ConstraintDefinition(
                dimension,
                dto.Title,
                polarity,
                dto.Help,
                dto.MaxLevel,
                dto.DefaultWeightTier,
                levels,
                demandCurve);
        }
        catch (ArgumentException ex)
        {
            throw new DataMappingException($"Invalid constraint '{dto.Dimension}': {ex.Message}", ex);
        }
    }

    private static ArchitecturePattern MapPattern(PatternDto dto, Dictionary<string, Mitigation> mitigationPool)
    {
        var id = ParseEnum<PatternId>(dto.Id, "pattern.id");

        if (dto.Capabilities.Count != ArchitecturePattern.DimensionCount)
        {
            throw new DataMappingException(
                $"patterns['{dto.Id}'].capabilities has length {dto.Capabilities.Count}, expected {ArchitecturePattern.DimensionCount}.");
        }

        if (dto.Rationales.Count != ArchitecturePattern.DimensionCount)
        {
            throw new DataMappingException(
                $"patterns['{dto.Id}'].rationales has length {dto.Rationales.Count}, expected {ArchitecturePattern.DimensionCount}.");
        }

        foreach (var capability in dto.Capabilities)
        {
            if (capability is < 0 or > 4)
            {
                throw new DataMappingException(
                    $"patterns['{dto.Id}'].capabilities contains out-of-range value {capability} (must be 0..4).");
            }
        }

        var tradeoffs = dto.Tradeoffs
            .Select(t => new Tradeoff(ParseEnum<ConstraintDimension>(t.Dimension, $"patterns['{dto.Id}'].tradeoffs[].dimension"), t.Gain, t.Cost))
            .ToImmutableArray();

        var risks = dto.Risks
            .Select(r => MapRisk(r, dto.Id, mitigationPool))
            .ToImmutableArray();

        try
        {
            return new ArchitecturePattern(
                id,
                dto.Name,
                dto.Summary,
                dto.Capabilities.ToImmutableArray(),
                dto.Rationales.ToImmutableArray(),
                tradeoffs,
                risks,
                dto.VariantNotes.ToImmutableArray());
        }
        catch (ArgumentException ex)
        {
            throw new DataMappingException($"Invalid pattern '{dto.Id}': {ex.Message}", ex);
        }
    }

    private static Risk MapRisk(RiskDto dto, string patternId, Dictionary<string, Mitigation> mitigationPool)
    {
        var severity = ParseEnum<RiskSeverity>(dto.Severity, $"patterns['{patternId}'].risks['{dto.Id}'].severity");

        ConstraintDimension? relatedDimension = dto.RelatedDimension is null
            ? null
            : ParseEnum<ConstraintDimension>(dto.RelatedDimension, $"patterns['{patternId}'].risks['{dto.Id}'].relatedDimension");

        var mitigations = ResolveMitigations(dto.MitigationIds, mitigationPool, $"patterns['{patternId}'].risks['{dto.Id}']");

        return new Risk(dto.Id, dto.Title, dto.Description, severity, relatedDimension, dto.ActivatesAtOrAboveLevel, mitigations);
    }

    private static Advisory MapAdvisory(AdvisoryDto dto, Dictionary<string, Mitigation> mitigationPool)
    {
        var pattern = ParseEnum<PatternId>(dto.Pattern, "advisory.pattern");
        var dimension = ParseEnum<ConstraintDimension>(dto.Dimension, $"advisories['{dto.Pattern}/{dto.Dimension}'].dimension");
        var op = ParseEnum<AdvisoryOp>(dto.Op, $"advisories['{dto.Pattern}/{dto.Dimension}'].op");
        var mitigations = ResolveMitigations(dto.MitigationIds, mitigationPool, $"advisories['{dto.Pattern}/{dto.Dimension}']");

        return new Advisory(pattern, dimension, op, dto.Level, dto.Message, mitigations);
    }

    private static Scenario MapScenario(ScenarioDto dto)
    {
        if (dto.Profile.Count != 8)
        {
            throw new DataMappingException(
                $"scenarios['{dto.Id}'].profile must cover exactly 8 dimensions, found {dto.Profile.Count}.");
        }

        var settings = ImmutableArray.CreateBuilder<ConstraintSetting>(8);
        foreach (var dimension in Enum.GetValues<ConstraintDimension>().OrderBy(d => (int)d))
        {
            if (!dto.Profile.TryGetValue(dimension.ToString(), out var setting))
            {
                throw new DataMappingException(
                    $"scenarios['{dto.Id}'].profile is missing an entry for dimension '{dimension}'.");
            }

            settings.Add(new ConstraintSetting(dimension, setting.Level, setting.WeightTier, setting.Hard));
        }

        try
        {
            var profile = new ConstraintProfile(settings.ToImmutable());
            return new Scenario(dto.Id, dto.Title, dto.Framing, dto.Assumptions.ToImmutableArray(), profile);
        }
        catch (ArgumentException ex)
        {
            throw new DataMappingException($"Invalid scenario '{dto.Id}': {ex.Message}", ex);
        }
    }

    private static TEnum ParseEnum<TEnum>(string? value, string fieldDescription)
        where TEnum : struct, Enum
    {
        if (value is null || !Enum.TryParse<TEnum>(value, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new DataMappingException(
                $"Unknown {typeof(TEnum).Name} value '{value}' for {fieldDescription}.");
        }

        return parsed;
    }
}
