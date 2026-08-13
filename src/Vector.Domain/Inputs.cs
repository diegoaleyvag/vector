using System.Collections.Immutable;

namespace Vector.Domain;

/// <summary>
/// A normalized set of basis-point weights (summing to exactly 10000) across all eight canonical
/// constraint dimensions, derived from small-integer weight tiers via the Hamilton / largest-remainder method.
/// </summary>
public sealed class WeightSet : IEquatable<WeightSet>
{
    /// <summary>Total basis points a valid WeightSet must sum to.</summary>
    public const int TotalBasisPoints = 10000;

    private static readonly ConstraintDimension[] CanonicalDimensions =
        [.. Enum.GetValues<ConstraintDimension>().OrderBy(d => (int)d)];

    /// <summary>Basis-point weight per dimension, in canonical dimension order, summing to exactly 10000.</summary>
    public ImmutableSortedDictionary<ConstraintDimension, int> BasisPoints { get; }

    private WeightSet(ImmutableSortedDictionary<ConstraintDimension, int> basisPoints)
    {
        BasisPoints = basisPoints;
    }

    /// <summary>Returns the basis-point weight assigned to the given dimension.</summary>
    public int this[ConstraintDimension d] => BasisPoints[d];

    /// <summary>
    /// Builds a WeightSet from small-integer weight tiers (typically 0..3) using the Hamilton /
    /// largest-remainder apportionment method: each dimension's ideal share of 10000 basis points is
    /// floored, and the leftover basis points are distributed one at a time to the dimensions with the
    /// largest fractional remainder (ties broken by ascending dimension value). If every tier is zero,
    /// basis points are distributed equally across all dimensions. The result always sums to exactly 10000.
    /// </summary>
    public static WeightSet FromTiers(IReadOnlyDictionary<ConstraintDimension, int> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        var dims = CanonicalDimensions;
        var raw = new long[dims.Length];
        long sumRaw = 0;
        for (var i = 0; i < dims.Length; i++)
        {
            raw[i] = tiers.TryGetValue(dims[i], out var t) ? t : 0;
            sumRaw += raw[i];
        }

        if (sumRaw == 0)
        {
            // Equal distribution: treat every dimension as having equal raw weight of 1.
            for (var i = 0; i < dims.Length; i++)
            {
                raw[i] = 1;
            }
            sumRaw = dims.Length;
        }

        var floors = new long[dims.Length];
        var remainders = new long[dims.Length];
        long sumFloors = 0;
        for (var i = 0; i < dims.Length; i++)
        {
            var numerator = raw[i] * TotalBasisPoints;
            floors[i] = numerator / sumRaw;
            remainders[i] = numerator % sumRaw;
            sumFloors += floors[i];
        }

        var remaining = (int)(TotalBasisPoints - sumFloors);
        var order = Enumerable.Range(0, dims.Length)
            .OrderByDescending(i => remainders[i])
            .ThenBy(i => (int)dims[i])
            .ToArray();

        var bp = new int[dims.Length];
        for (var i = 0; i < dims.Length; i++)
        {
            bp[i] = (int)floors[i];
        }

        for (var k = 0; k < remaining; k++)
        {
            bp[order[k]] += 1;
        }

        var builder = ImmutableSortedDictionary.CreateBuilder<ConstraintDimension, int>();
        for (var i = 0; i < dims.Length; i++)
        {
            builder[dims[i]] = bp[i];
        }

        return new WeightSet(builder.ToImmutable());
    }

    public bool Equals(WeightSet? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return BasisPoints.SequenceEqual(other.BasisPoints);
    }

    public override bool Equals(object? obj) => Equals(obj as WeightSet);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kv in BasisPoints)
        {
            hash.Add(kv.Key);
            hash.Add(kv.Value);
        }
        return hash.ToHashCode();
    }
}

/// <summary>The user's chosen level and weight tier for a single constraint dimension.</summary>
/// <param name="Dimension">The constraint dimension.</param>
/// <param name="LevelIndex">The zero-based selected level index.</param>
/// <param name="WeightTier">The authoritative weight tier (0..3) for this dimension.</param>
/// <param name="IsHard">Whether this dimension is a hard (vetoing) constraint.</param>
public sealed record ConstraintSetting(ConstraintDimension Dimension, int LevelIndex, int WeightTier, bool IsHard);

/// <summary>
/// A complete, order-invariant set of constraint settings covering all eight canonical dimensions
/// exactly once. Settings are canonicalized (sorted) by dimension regardless of construction order.
/// </summary>
public sealed class ConstraintProfile : IEquatable<ConstraintProfile>
{
    private readonly Lazy<WeightSet> _weights;

    /// <summary>The eight constraint settings, sorted by ascending <see cref="ConstraintDimension"/> value.</summary>
    public ImmutableArray<ConstraintSetting> Settings { get; }

    public ConstraintProfile(ImmutableArray<ConstraintSetting> settings)
    {
        if (settings.Length != 8)
        {
            throw new ArgumentException($"ConstraintProfile requires exactly 8 settings, got {settings.Length}.", nameof(settings));
        }

        var seen = new bool[8];
        foreach (var s in settings)
        {
            var idx = (int)s.Dimension - 1;
            if (idx is < 0 or >= 8 || seen[idx])
            {
                throw new ArgumentException($"Duplicate or invalid dimension in settings: {s.Dimension}.", nameof(settings));
            }
            seen[idx] = true;

            if (s.LevelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), s.LevelIndex, $"LevelIndex must be >= 0 for dimension {s.Dimension}.");
            }
        }

        Settings = settings.Sort((a, b) => ((int)a.Dimension).CompareTo((int)b.Dimension));
        _weights = new Lazy<WeightSet>(() => WeightSet.FromTiers(
            Settings.ToDictionary(s => s.Dimension, s => s.WeightTier)));
    }

    /// <summary>Returns the setting for the given dimension.</summary>
    public ConstraintSetting this[ConstraintDimension d] => Settings[(int)d - 1];

    /// <summary>The normalized basis-point weights derived from this profile's weight tiers.</summary>
    public WeightSet Weights => _weights.Value;

    public bool Equals(ConstraintProfile? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Settings.SequenceEqual(other.Settings);
    }

    public override bool Equals(object? obj) => Equals(obj as ConstraintProfile);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var s in Settings)
        {
            hash.Add(s);
        }
        return hash.ToHashCode();
    }
}

/// <summary>
/// A named decision scenario: a constraint profile plus purely descriptive metadata. The metadata
/// (Id, Title, Description, Assumptions) is NOT part of the config digest.
/// </summary>
/// <param name="Id">Stable identifier for the scenario.</param>
/// <param name="Title">Display title.</param>
/// <param name="Description">Optional longer description.</param>
/// <param name="Assumptions">Free-form list of assumptions behind this scenario.</param>
/// <param name="Profile">The constraint profile driving the decision.</param>
public sealed record Scenario(string Id, string Title, string? Description, ImmutableArray<string> Assumptions, ConstraintProfile Profile)
{
    public bool Equals(Scenario? other) =>
        other is not null
        && Id == other.Id
        && Title == other.Title
        && Description == other.Description
        && Assumptions.SequenceEqual(other.Assumptions)
        && Profile.Equals(other.Profile);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Title);
        hash.Add(Description);
        foreach (var a in Assumptions)
        {
            hash.Add(a);
        }
        hash.Add(Profile);
        return hash.ToHashCode();
    }
}
