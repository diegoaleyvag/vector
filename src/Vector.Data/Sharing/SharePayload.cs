using System.Collections.Immutable;

namespace Vector.Data.Sharing;

/// <summary>
/// The minimal, non-free-text state needed to reconstruct a constraint profile from a share link:
/// an optional originating scenario id, and the level/weight-tier/hard settings for all eight canonical
/// dimensions in CANONICAL order (index 0 = DataSensitivity ... 7 = OperationalMaturity). Deliberately
/// carries no rationale, titles, or other free text.
/// </summary>
public sealed record SharePayload(
    string? ScenarioId,
    ImmutableArray<int> Levels,
    ImmutableArray<int> WeightTiers,
    ImmutableArray<bool> Hard,
    string RulesVersion)
{
    public bool Equals(SharePayload? other) =>
        other is not null
        && ScenarioId == other.ScenarioId
        && Levels.SequenceEqual(other.Levels)
        && WeightTiers.SequenceEqual(other.WeightTiers)
        && Hard.SequenceEqual(other.Hard)
        && RulesVersion == other.RulesVersion;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ScenarioId);
        foreach (var l in Levels)
        {
            hash.Add(l);
        }
        foreach (var w in WeightTiers)
        {
            hash.Add(w);
        }
        foreach (var h in Hard)
        {
            hash.Add(h);
        }
        hash.Add(RulesVersion);
        return hash.ToHashCode();
    }
}
