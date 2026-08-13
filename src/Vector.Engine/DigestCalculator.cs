using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Vector.Domain;

namespace Vector.Engine;

/// <summary>
/// Computes deterministic, integer-only, culture-invariant SHA-256 digests over engine/rule version
/// identifiers, engine constants, and constraint profile settings. Nothing floating-point or
/// culture-sensitive participates in the hashed byte stream: numbers are written as fixed-width
/// big-endian integers and the only strings hashed are version identifiers, written length-prefixed.
/// </summary>
public static class DigestCalculator
{
    private const string ConfigDigestPrefix = "Vector.MCDA";
    private const string RulesContentPrefix = "Vector.MCDA.Rules";

    private const byte ProfileSectionMarker = 0x01;
    private const byte EndMarker = 0x02;

    private static readonly char[] HexAlphabet = "0123456789abcdef".ToCharArray();

    /// <summary>
    /// Computes the config digest for a constraint profile evaluated under a given rule set's version
    /// identifiers. Only the profile's settings (dimension, level index, weight tier, hard flag) and the
    /// engine/rules version identifiers participate; scenario metadata (Id/Title/Description/Assumptions)
    /// is intentionally excluded so cosmetic edits never change the digest.
    /// </summary>
    public static string ComputeConfigDigest(ConstraintProfile profile, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(rules);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        WriteString(hash, ConfigDigestPrefix);
        WriteString(hash, EngineConstants.EngineVersion);
        WriteString(hash, rules.RulesVersion);
        WriteString(hash, rules.RulesContentHash);
        WriteInt32(hash, EngineConstants.Scale);
        WriteInt32(hash, EngineConstants.RawMin);
        WriteInt32(hash, EngineConstants.RawMax);
        WriteByte(hash, ProfileSectionMarker);

        // profile.Settings is already canonicalized to ascending dimension order by ConstraintProfile.
        foreach (var setting in profile.Settings)
        {
            WriteInt32(hash, (int)setting.Dimension);
            WriteInt32(hash, setting.LevelIndex);
            WriteInt32(hash, setting.WeightTier);
            WriteByte(hash, (byte)(setting.IsHard ? 1 : 0));
        }

        WriteByte(hash, EndMarker);

        return FinishAsSha256Digest(hash);
    }

    /// <summary>
    /// Computes a content hash over the rule content that drives scoring: each constraint's demand
    /// curve, each pattern's capability matrix, and the near-tie margin. Used to detect rule drift
    /// (e.g. authoring a new capability value) independently of the engine/profile digest above.
    /// </summary>
    public static string ComputeRulesContentHash(
        ImmutableArray<ConstraintDefinition> constraints,
        ImmutableArray<ArchitecturePattern> patterns,
        int nearTieMarginBasisPoints)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        WriteString(hash, RulesContentPrefix);

        var orderedConstraints = constraints.Sort((a, b) => ((int)a.Dimension).CompareTo((int)b.Dimension));
        foreach (var c in orderedConstraints)
        {
            WriteInt32(hash, (int)c.Dimension);
            WriteInt32(hash, c.DemandCurve.Length);
            foreach (var demand in c.DemandCurve)
            {
                WriteInt32(hash, demand);
            }
        }

        var orderedPatterns = patterns.Sort((a, b) => ((int)a.Id).CompareTo((int)b.Id));
        foreach (var p in orderedPatterns)
        {
            WriteInt32(hash, (int)p.Id);
            WriteInt32(hash, p.Capabilities.Length);
            foreach (var capability in p.Capabilities)
            {
                WriteInt32(hash, capability);
            }
        }

        WriteInt32(hash, nearTieMarginBasisPoints);

        return FinishAsSha256Digest(hash);
    }

    private static void WriteString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> lengthBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, bytes.Length);
        hash.AppendData(lengthBuffer);
        hash.AppendData(bytes);
    }

    private static void WriteInt32(IncrementalHash hash, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void WriteByte(IncrementalHash hash, byte value) => hash.AppendData([value]);

    private static string FinishAsSha256Digest(IncrementalHash hash)
    {
        Span<byte> digest = stackalloc byte[32];
        hash.GetHashAndReset(digest);

        var chars = new char[7 + digest.Length * 2];
        "Sha256:".CopyTo(chars);
        for (var i = 0; i < digest.Length; i++)
        {
            chars[7 + i * 2] = HexAlphabet[digest[i] >> 4];
            chars[7 + i * 2 + 1] = HexAlphabet[digest[i] & 0xF];
        }

        return new string(chars);
    }
}
