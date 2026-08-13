namespace Vector.Domain;

/// <summary>Identifies one of the four canonical LLM architecture patterns evaluated by the engine.</summary>
/// <remarks>Values are explicit and stable: they are hashed into the config digest.</remarks>
public enum PatternId
{
    DirectStructuredCall = 1,
    DeterministicWorkflow = 2,
    RetrievalAugmentedGeneration = 3,
    ToolUsingAgent = 4,
}

/// <summary>
/// One of the eight canonical constraint dimensions used to score patterns.
/// The integer values 1..8 define the CANONICAL iteration order used everywhere:
/// scoring, trace construction, and digest hashing.
/// </summary>
public enum ConstraintDimension
{
    DataSensitivity = 1,
    LatencyTarget = 2,
    CostPressure = 3,
    DeterminismReproducibility = 4,
    KnowledgeFreshness = 5,
    ToolActionNeed = 6,
    HumanReview = 7,
    OperationalMaturity = 8,
}

/// <summary>Whether a pattern is still under consideration (Eligible) or has been vetoed by a hard constraint.</summary>
public enum HardStatus
{
    Eligible = 0,
    Vetoed = 1,
}

/// <summary>The sign of a raw fit value (capability minus demand) on a single dimension.</summary>
public enum ContributionSign
{
    Negative = -1,
    Neutral = 0,
    Positive = 1,
}

/// <summary>Whether a hard constraint is compatible with, or in conflict with, a pattern's capability on that dimension.</summary>
public enum HardVerdict
{
    NotApplicable = 0,
    Compatible = 1,
    Conflict = 2,
}

/// <summary>
/// Whether higher constraint levels represent a rising demand (Demand) or a rising capacity/tolerance (Capacity).
/// Capacity dimensions are expressed via an inverted (decreasing) demand curve so the engine never special-cases polarity.
/// </summary>
public enum ConstraintPolarity
{
    Demand = 0,
    Capacity = 1,
}

/// <summary>Severity of a risk associated with a pattern.</summary>
public enum RiskSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
}

/// <summary>Relative effort required to apply a mitigation.</summary>
public enum MitigationEffort
{
    Low = 1,
    Medium = 2,
    High = 3,
}

/// <summary>Comparison operator used by an <see cref="Advisory"/> to decide whether it applies to a given constraint level.</summary>
public enum AdvisoryOp
{
    GreaterOrEqual = 0,
    Equal = 1,
    LessOrEqual = 2,
}
