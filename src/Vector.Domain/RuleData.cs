using System.Collections.Immutable;

namespace Vector.Domain;

/// <summary>Describes one selectable level of a constraint dimension.</summary>
/// <param name="Value">The zero-based level index this metadata describes.</param>
/// <param name="Name">Short display name for the level.</param>
/// <param name="Help">Explanatory text shown to the user.</param>
/// <param name="Evidence">Supporting rationale / evidence for why this level maps the way it does.</param>
public sealed record LevelMetadata(int Value, string Name, string Help, string Evidence);

/// <summary>
/// A mitigation that can reduce the impact of a <see cref="Risk"/>.
/// </summary>
/// <param name="Id">Stable identifier for the mitigation.</param>
/// <param name="Description">Human-readable description of the mitigation.</param>
/// <param name="Effort">Relative effort required to apply it.</param>
public sealed record Mitigation(string Id, string Description, MitigationEffort Effort);

/// <summary>
/// A risk that may be relevant to an architecture pattern, optionally tied to a specific constraint dimension level.
/// </summary>
/// <param name="Id">Stable identifier for the risk.</param>
/// <param name="Title">Short title.</param>
/// <param name="Description">Full description of the risk.</param>
/// <param name="Severity">Severity of the risk.</param>
/// <param name="RelatedDimension">
/// If set, the risk only activates when the profile's level on this dimension is at or above
/// <see cref="ActivatesAtOrAboveLevel"/>. If null, the risk is always active for the pattern.
/// </param>
/// <param name="ActivatesAtOrAboveLevel">Threshold level index (inclusive) for activation, when <see cref="RelatedDimension"/> is set.</param>
/// <param name="Mitigations">Mitigations that reduce this risk.</param>
public sealed record Risk(
    string Id,
    string Title,
    string Description,
    RiskSeverity Severity,
    ConstraintDimension? RelatedDimension,
    int? ActivatesAtOrAboveLevel,
    ImmutableArray<Mitigation> Mitigations)
{
    public bool Equals(Risk? other) =>
        other is not null
        && Id == other.Id
        && Title == other.Title
        && Description == other.Description
        && Severity == other.Severity
        && RelatedDimension == other.RelatedDimension
        && ActivatesAtOrAboveLevel == other.ActivatesAtOrAboveLevel
        && Mitigations.SequenceEqual(other.Mitigations);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Title);
        hash.Add(Description);
        hash.Add(Severity);
        hash.Add(RelatedDimension);
        hash.Add(ActivatesAtOrAboveLevel);
        foreach (var m in Mitigations)
        {
            hash.Add(m);
        }
        return hash.ToHashCode();
    }
}

/// <summary>Describes the gain and cost tradeoff an architecture pattern makes on a given dimension.</summary>
/// <param name="Dimension">The constraint dimension this tradeoff pertains to.</param>
/// <param name="Gain">What is gained.</param>
/// <param name="Cost">What is given up.</param>
public sealed record Tradeoff(ConstraintDimension Dimension, string Gain, string Cost);

/// <summary>
/// A non-scoring educational caution shown for a pattern when the profile's level on <see cref="Dimension"/>
/// satisfies <see cref="Op"/> against <see cref="Level"/>. Advisories never affect score or ranking.
/// </summary>
/// <param name="Pattern">The pattern this advisory applies to.</param>
/// <param name="Dimension">The constraint dimension examined.</param>
/// <param name="Op">The comparison operator.</param>
/// <param name="Level">The level compared against.</param>
/// <param name="Message">The advisory message shown to the user.</param>
/// <param name="Mitigations">Suggested mitigations.</param>
public sealed record Advisory(
    PatternId Pattern,
    ConstraintDimension Dimension,
    AdvisoryOp Op,
    int Level,
    string Message,
    ImmutableArray<Mitigation> Mitigations)
{
    public bool Equals(Advisory? other) =>
        other is not null
        && Pattern == other.Pattern
        && Dimension == other.Dimension
        && Op == other.Op
        && Level == other.Level
        && Message == other.Message
        && Mitigations.SequenceEqual(other.Mitigations);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Pattern);
        hash.Add(Dimension);
        hash.Add(Op);
        hash.Add(Level);
        hash.Add(Message);
        foreach (var m in Mitigations)
        {
            hash.Add(m);
        }
        return hash.ToHashCode();
    }

    /// <summary>Evaluates whether this advisory applies given the profile's level on <see cref="Dimension"/>.</summary>
    public bool Matches(int levelIndex) => Op switch
    {
        AdvisoryOp.GreaterOrEqual => levelIndex >= Level,
        AdvisoryOp.Equal => levelIndex == Level,
        AdvisoryOp.LessOrEqual => levelIndex <= Level,
        _ => throw new ArgumentOutOfRangeException(nameof(Op), Op, "Unknown advisory operator."),
    };
}

/// <summary>
/// Defines one of the eight constraint dimensions: its selectable levels and the demand curve mapping
/// each level index to a demand value in 0..4. Capacity-polarity dimensions are represented by an
/// inverted (decreasing) demand curve so the scoring engine stays uniform across polarities.
/// </summary>
public sealed record ConstraintDefinition
{
    /// <summary>The dimension identity.</summary>
    public ConstraintDimension Dimension { get; }

    /// <summary>Display title.</summary>
    public string Title { get; }

    /// <summary>Whether rising levels mean rising demand or rising capacity/tolerance.</summary>
    public ConstraintPolarity Polarity { get; }

    /// <summary>Explanatory help text.</summary>
    public string Help { get; }

    /// <summary>The highest valid level index (levels are 0..MaxLevel).</summary>
    public int MaxLevel { get; }

    /// <summary>Default weight tier (0..3) suggested for this dimension.</summary>
    public int DefaultWeightTier { get; }

    /// <summary>Metadata for each level; length == DemandCurve.Length.</summary>
    public ImmutableArray<LevelMetadata> Levels { get; }

    /// <summary>Demand value (0..4) for each level index; length == Levels.Length.</summary>
    public ImmutableArray<int> DemandCurve { get; }

    public ConstraintDefinition(
        ConstraintDimension dimension,
        string title,
        ConstraintPolarity polarity,
        string help,
        int maxLevel,
        int defaultWeightTier,
        ImmutableArray<LevelMetadata> levels,
        ImmutableArray<int> demandCurve)
    {
        if (demandCurve.Length != levels.Length)
        {
            throw new ArgumentException(
                $"DemandCurve.Length ({demandCurve.Length}) must equal Levels.Length ({levels.Length}) for dimension {dimension}.",
                nameof(demandCurve));
        }

        foreach (var demand in demandCurve)
        {
            if (demand is < 0 or > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(demandCurve), demand, $"Demand values must be in 0..4 for dimension {dimension}.");
            }
        }

        Dimension = dimension;
        Title = title;
        Polarity = polarity;
        Help = help;
        MaxLevel = maxLevel;
        DefaultWeightTier = defaultWeightTier;
        Levels = levels;
        DemandCurve = demandCurve;
    }

    public bool Equals(ConstraintDefinition? other) =>
        other is not null
        && Dimension == other.Dimension
        && Title == other.Title
        && Polarity == other.Polarity
        && Help == other.Help
        && MaxLevel == other.MaxLevel
        && DefaultWeightTier == other.DefaultWeightTier
        && Levels.SequenceEqual(other.Levels)
        && DemandCurve.SequenceEqual(other.DemandCurve);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Dimension);
        hash.Add(Title);
        hash.Add(Polarity);
        hash.Add(Help);
        hash.Add(MaxLevel);
        hash.Add(DefaultWeightTier);
        foreach (var l in Levels)
        {
            hash.Add(l);
        }
        foreach (var d in DemandCurve)
        {
            hash.Add(d);
        }
        return hash.ToHashCode();
    }
}

/// <summary>
/// Describes one of the four architecture patterns: its capability profile across all eight canonical
/// dimensions, authored rationale text per dimension, tradeoffs, risks, and free-form variant notes.
/// </summary>
public sealed record ArchitecturePattern
{
    /// <summary>The number of canonical constraint dimensions; Capabilities and Rationales must have this length.</summary>
    public const int DimensionCount = 8;

    /// <summary>The pattern identity.</summary>
    public PatternId Id { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>Short summary of the pattern.</summary>
    public string Summary { get; }

    /// <summary>Capability value (0..4) per dimension, length 8, canonical order.</summary>
    public ImmutableArray<int> Capabilities { get; }

    /// <summary>Authored rationale text per dimension, length 8, canonical order.</summary>
    public ImmutableArray<string> Rationales { get; }

    /// <summary>Notable gain/cost tradeoffs for this pattern.</summary>
    public ImmutableArray<Tradeoff> Tradeoffs { get; }

    /// <summary>Risks associated with this pattern.</summary>
    public ImmutableArray<Risk> Risks { get; }

    /// <summary>Free-form notes about variants of this pattern.</summary>
    public ImmutableArray<string> VariantNotes { get; }

    public ArchitecturePattern(
        PatternId id,
        string name,
        string summary,
        ImmutableArray<int> capabilities,
        ImmutableArray<string> rationales,
        ImmutableArray<Tradeoff> tradeoffs,
        ImmutableArray<Risk> risks,
        ImmutableArray<string> variantNotes)
    {
        if (capabilities.Length != DimensionCount)
        {
            throw new ArgumentException($"Capabilities must have length {DimensionCount} for pattern {id}.", nameof(capabilities));
        }

        if (rationales.Length != DimensionCount)
        {
            throw new ArgumentException($"Rationales must have length {DimensionCount} for pattern {id}.", nameof(rationales));
        }

        foreach (var capability in capabilities)
        {
            if (capability is < 0 or > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(capabilities), capability, $"Capability values must be in 0..4 for pattern {id}.");
            }
        }

        Id = id;
        Name = name;
        Summary = summary;
        Capabilities = capabilities;
        Rationales = rationales;
        Tradeoffs = tradeoffs;
        Risks = risks;
        VariantNotes = variantNotes;
    }

    /// <summary>Returns the capability value (0..4) of this pattern on the given dimension.</summary>
    public int CapabilityFor(ConstraintDimension d) => Capabilities[(int)d - 1];

    /// <summary>Returns the authored rationale text for this pattern on the given dimension.</summary>
    public string RationaleFor(ConstraintDimension d) => Rationales[(int)d - 1];

    public bool Equals(ArchitecturePattern? other) =>
        other is not null
        && Id == other.Id
        && Name == other.Name
        && Summary == other.Summary
        && Capabilities.SequenceEqual(other.Capabilities)
        && Rationales.SequenceEqual(other.Rationales)
        && Tradeoffs.SequenceEqual(other.Tradeoffs)
        && Risks.SequenceEqual(other.Risks)
        && VariantNotes.SequenceEqual(other.VariantNotes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Name);
        hash.Add(Summary);
        foreach (var c in Capabilities)
        {
            hash.Add(c);
        }
        foreach (var r in Rationales)
        {
            hash.Add(r);
        }
        foreach (var t in Tradeoffs)
        {
            hash.Add(t);
        }
        foreach (var r in Risks)
        {
            hash.Add(r);
        }
        foreach (var v in VariantNotes)
        {
            hash.Add(v);
        }
        return hash.ToHashCode();
    }
}

/// <summary>
/// The full rule content used by the engine: constraint definitions, architecture patterns, and
/// educational advisories. Validated on construction to guarantee exactly one constraint per dimension
/// and exactly one pattern per <see cref="PatternId"/>.
/// </summary>
public sealed record RuleSet
{
    /// <summary>Semantic version of this rule content.</summary>
    public string RulesVersion { get; }

    /// <summary>Content hash of the rule data, computed by <c>Vector.Engine.DigestCalculator</c>.</summary>
    public string RulesContentHash { get; }

    /// <summary>Version range of engines compatible with this rule content.</summary>
    public string EngineCompatRange { get; }

    /// <summary>Exactly 8 constraint definitions, one per dimension, in canonical order.</summary>
    public ImmutableArray<ConstraintDefinition> Constraints { get; }

    /// <summary>Exactly 4 architecture patterns, one per <see cref="PatternId"/>.</summary>
    public ImmutableArray<ArchitecturePattern> Patterns { get; }

    /// <summary>Educational, non-scoring advisories.</summary>
    public ImmutableArray<Advisory> Advisories { get; }

    /// <summary>Basis-point margin threshold used to flag a near-tie at the top of the ranking.</summary>
    public int NearTieMarginBasisPoints { get; }

    private readonly ImmutableArray<ConstraintDefinition> _constraintsByDimension;
    private readonly ImmutableArray<ArchitecturePattern> _patternsById;

    public RuleSet(
        string rulesVersion,
        string rulesContentHash,
        string engineCompatRange,
        ImmutableArray<ConstraintDefinition> constraints,
        ImmutableArray<ArchitecturePattern> patterns,
        ImmutableArray<Advisory> advisories,
        int nearTieMarginBasisPoints)
    {
        if (constraints.Length != 8)
        {
            throw new ArgumentException($"RuleSet must contain exactly 8 constraints, found {constraints.Length}.", nameof(constraints));
        }

        if (patterns.Length != 4)
        {
            throw new ArgumentException($"RuleSet must contain exactly 4 patterns, found {patterns.Length}.", nameof(patterns));
        }

        var byDimension = new ConstraintDefinition?[8];
        foreach (var c in constraints)
        {
            var idx = (int)c.Dimension - 1;
            if (idx is < 0 or >= 8 || byDimension[idx] is not null)
            {
                throw new ArgumentException($"Duplicate or invalid constraint dimension: {c.Dimension}.", nameof(constraints));
            }
            byDimension[idx] = c;
        }

        for (var i = 0; i < 8; i++)
        {
            if (byDimension[i] is null)
            {
                throw new ArgumentException($"Missing constraint for dimension {(ConstraintDimension)(i + 1)}.", nameof(constraints));
            }
        }

        var byId = new ArchitecturePattern?[4];
        foreach (var p in patterns)
        {
            var idx = (int)p.Id - 1;
            if (idx is < 0 or >= 4 || byId[idx] is not null)
            {
                throw new ArgumentException($"Duplicate or invalid pattern id: {p.Id}.", nameof(patterns));
            }
            byId[idx] = p;
        }

        for (var i = 0; i < 4; i++)
        {
            if (byId[i] is null)
            {
                throw new ArgumentException($"Missing pattern for id {(PatternId)(i + 1)}.", nameof(patterns));
            }
        }

        RulesVersion = rulesVersion;
        RulesContentHash = rulesContentHash;
        EngineCompatRange = engineCompatRange;
        Constraints = constraints;
        Patterns = patterns;
        Advisories = advisories;
        NearTieMarginBasisPoints = nearTieMarginBasisPoints;
        _constraintsByDimension = [.. byDimension!];
        _patternsById = [.. byId!];
    }

    /// <summary>Returns the constraint definition for the given dimension.</summary>
    public ConstraintDefinition Constraint(ConstraintDimension d) => _constraintsByDimension[(int)d - 1];

    /// <summary>Returns the demand value (0..4) for the given dimension at the given level index.</summary>
    public int Demand(ConstraintDimension d, int levelIndex) => Constraint(d).DemandCurve[levelIndex];

    /// <summary>Returns the architecture pattern for the given identity.</summary>
    public ArchitecturePattern Pattern(PatternId id) => _patternsById[(int)id - 1];

    /// <summary>Returns the display label for the given dimension at the given level index.</summary>
    public string LevelLabel(ConstraintDimension d, int levelIndex) => Constraint(d).Levels[levelIndex].Name;

    public bool Equals(RuleSet? other) =>
        other is not null
        && RulesVersion == other.RulesVersion
        && RulesContentHash == other.RulesContentHash
        && EngineCompatRange == other.EngineCompatRange
        && Constraints.SequenceEqual(other.Constraints)
        && Patterns.SequenceEqual(other.Patterns)
        && Advisories.SequenceEqual(other.Advisories)
        && NearTieMarginBasisPoints == other.NearTieMarginBasisPoints;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RulesVersion);
        hash.Add(RulesContentHash);
        hash.Add(EngineCompatRange);
        foreach (var c in Constraints)
        {
            hash.Add(c);
        }
        foreach (var p in Patterns)
        {
            hash.Add(p);
        }
        foreach (var a in Advisories)
        {
            hash.Add(a);
        }
        hash.Add(NearTieMarginBasisPoints);
        return hash.ToHashCode();
    }
}
