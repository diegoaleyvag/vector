namespace Vector.Engine;

/// <summary>
/// Fixed-point and versioning constants for the deterministic MCDA engine. <see cref="Scale"/>,
/// <see cref="RawMin"/>, <see cref="RawMax"/>, and <see cref="EngineVersion"/> are all hashed by
/// <see cref="DigestCalculator"/> as part of the config digest.
/// </summary>
public static class EngineConstants
{
    /// <summary>Fixed-point scale used for all normalized/weighted score arithmetic (1.0 == Scale).</summary>
    public const int Scale = 1_000_000;

    /// <summary>The minimum possible raw fit value (capability - demand).</summary>
    public const int RawMin = -4;

    /// <summary>The maximum possible raw fit value (capability - demand).</summary>
    public const int RawMax = 4;

    /// <summary>The semantic version of this engine implementation.</summary>
    public const string EngineVersion = "1.0.0";
}
