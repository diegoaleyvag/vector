using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using Vector.Data.Dtos;
using Vector.Data.Serialization;
using Vector.Domain;

namespace Vector.Data.Sharing;

/// <summary>
/// Encodes and decodes a <see cref="SharePayload"/> to/from a compact, URL-fragment-safe string:
/// compact JSON, optionally Brotli-compressed (whichever of raw/compressed is smaller wins), then
/// base64url with no padding, prefixed with a version tag. Pure: no Blazor, no I/O. <see cref="Decode"/>
/// never throws; every failure mode is reported as a <see cref="ShareDecodeResult"/>.
/// </summary>
public static class ShareCodec
{
    /// <summary>The only version prefix this codec currently accepts.</summary>
    public const string Version = "v1";

    /// <summary>Hard cap on the encoded (post-version-prefix) fragment length, checked before any decoding work.</summary>
    public const int MaxEncodedChars = 1800;

    /// <summary>Hard cap on decompressed payload bytes; enforced by bounding the decompression output buffer.</summary>
    public const int MaxDecodedBytes = 4096;

    private const byte CompressedHeader = (byte)'c';
    private const byte RawHeader = (byte)'r';
    private const int BrotliQuality = 5;
    private const int BrotliWindow = 22;

    private const int MaxScenarioIdLength = 64;
    private const int MaxRulesVersionLength = 32;

    /// <summary>Encodes a payload into a versioned, URL-fragment-safe string. Deterministic: the same payload always yields the same string.</summary>
    public static string Encode(SharePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var dto = ToDto(payload);
        var json = JsonSerializer.SerializeToUtf8Bytes(dto, VectorJsonContext.Default.SharePayloadDto);
        var compressed = CompressBrotli(json);

        byte header;
        byte[] body;
        if (compressed.Length < json.Length)
        {
            header = CompressedHeader;
            body = compressed;
        }
        else
        {
            header = RawHeader;
            body = json;
        }

        var withHeader = new byte[1 + body.Length];
        withHeader[0] = header;
        body.CopyTo(withHeader, 1);

        return $"{Version}.{Base64UrlEncode(withHeader)}";
    }

    /// <summary>Decodes a share fragment. Never throws: any failure is reported via the returned result's <see cref="ShareDecodeResult.Error"/>.</summary>
    public static ShareDecodeResult Decode(string? fragment)
    {
        try
        {
            return DecodeCore(fragment);
        }
        catch (Exception ex)
        {
            // Belt-and-braces: ShareCodec must never throw. Any unanticipated failure degrades to a
            // generic schema-invalid result rather than propagating.
            return ShareDecodeResult.Fail(ShareError.SchemaInvalid, $"Unexpected decode failure: {ex.Message}");
        }
    }

    private static ShareDecodeResult DecodeCore(string? fragment)
    {
        if (string.IsNullOrEmpty(fragment))
        {
            return ShareDecodeResult.Fail(ShareError.Empty, "The share fragment is empty.");
        }

        var dot = fragment.IndexOf('.');
        if (dot < 0)
        {
            return ShareDecodeResult.Fail(ShareError.BadVersion, "Missing version prefix.");
        }

        var version = fragment[..dot];
        if (version != Version)
        {
            return ShareDecodeResult.Fail(ShareError.BadVersion, $"Unsupported version '{version}'; expected '{Version}'.");
        }

        var encoded = fragment[(dot + 1)..];
        if (encoded.Length > MaxEncodedChars)
        {
            return ShareDecodeResult.Fail(ShareError.TooLarge, $"Encoded payload length {encoded.Length} exceeds the {MaxEncodedChars}-character cap.");
        }

        byte[] withHeader;
        try
        {
            withHeader = Base64UrlDecode(encoded);
        }
        catch (FormatException ex)
        {
            return ShareDecodeResult.Fail(ShareError.BadEncoding, $"Invalid base64url encoding: {ex.Message}");
        }

        if (withHeader.Length < 1)
        {
            return ShareDecodeResult.Fail(ShareError.BadEncoding, "Decoded payload is empty.");
        }

        var header = withHeader[0];
        var body = withHeader.AsSpan(1);

        byte[] jsonBytes;
        if (header == CompressedHeader)
        {
            if (!TryDecompressBrotli(body, MaxDecodedBytes, out jsonBytes))
            {
                return ShareDecodeResult.Fail(ShareError.DecompressFailed, $"Compressed payload could not be decompressed within the {MaxDecodedBytes}-byte cap.");
            }
        }
        else if (header == RawHeader)
        {
            if (body.Length > MaxDecodedBytes)
            {
                return ShareDecodeResult.Fail(ShareError.DecompressFailed, $"Raw payload of {body.Length} bytes exceeds the {MaxDecodedBytes}-byte cap.");
            }

            jsonBytes = body.ToArray();
        }
        else
        {
            return ShareDecodeResult.Fail(ShareError.BadEncoding, $"Unrecognized payload header byte 0x{header:X2}.");
        }

        SharePayloadDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(jsonBytes, VectorJsonContext.Default.SharePayloadDto);
        }
        catch (JsonException ex)
        {
            return ShareDecodeResult.Fail(ShareError.SchemaInvalid, $"Payload JSON is malformed: {ex.Message}");
        }

        if (dto is null)
        {
            return ShareDecodeResult.Fail(ShareError.SchemaInvalid, "Payload JSON deserialized to null.");
        }

        var validationError = Validate(dto);
        if (validationError is not null)
        {
            return ShareDecodeResult.Fail(ShareError.SchemaInvalid, validationError);
        }

        return ShareDecodeResult.Success(FromDto(dto));
    }

    /// <summary>Builds a <see cref="SharePayload"/> from a scenario/custom profile's settings, in canonical dimension order.</summary>
    public static SharePayload FromProfile(ConstraintProfile profile, string rulesVersion, string? scenarioId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(rulesVersion);

        var levels = ImmutableArray.CreateBuilder<int>(8);
        var tiers = ImmutableArray.CreateBuilder<int>(8);
        var hard = ImmutableArray.CreateBuilder<bool>(8);

        foreach (var setting in profile.Settings)
        {
            levels.Add(setting.LevelIndex);
            tiers.Add(setting.WeightTier);
            hard.Add(setting.IsHard);
        }

        return new SharePayload(scenarioId, levels.ToImmutable(), tiers.ToImmutable(), hard.ToImmutable(), rulesVersion);
    }

    /// <summary>Reconstructs a <see cref="ConstraintProfile"/> from a payload's canonical-order arrays.</summary>
    public static ConstraintProfile ToProfile(SharePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var dimensions = Enum.GetValues<ConstraintDimension>().OrderBy(d => (int)d).ToArray();
        var settings = ImmutableArray.CreateBuilder<ConstraintSetting>(8);
        for (var i = 0; i < dimensions.Length; i++)
        {
            settings.Add(new ConstraintSetting(dimensions[i], payload.Levels[i], payload.WeightTiers[i], payload.Hard[i]));
        }

        return new ConstraintProfile(settings.ToImmutable());
    }

    private static SharePayloadDto ToDto(SharePayload payload) => new()
    {
        ScenarioId = payload.ScenarioId,
        Levels = [.. payload.Levels],
        WeightTiers = [.. payload.WeightTiers],
        Hard = [.. payload.Hard],
        RulesVersion = payload.RulesVersion,
    };

    private static SharePayload FromDto(SharePayloadDto dto) => new(
        dto.ScenarioId,
        [.. dto.Levels],
        [.. dto.WeightTiers],
        [.. dto.Hard],
        dto.RulesVersion);

    private static string? Validate(SharePayloadDto dto)
    {
        if (dto.Levels is null || dto.Levels.Length != 8)
        {
            return $"'levels' must contain exactly 8 entries, found {dto.Levels?.Length ?? 0}.";
        }

        if (dto.WeightTiers is null || dto.WeightTiers.Length != 8)
        {
            return $"'weightTiers' must contain exactly 8 entries, found {dto.WeightTiers?.Length ?? 0}.";
        }

        if (dto.Hard is null || dto.Hard.Length != 8)
        {
            return $"'hard' must contain exactly 8 entries, found {dto.Hard?.Length ?? 0}.";
        }

        foreach (var level in dto.Levels)
        {
            if (level is < 0 or > 4)
            {
                return $"'levels' contains out-of-range value {level} (must be 0..4).";
            }
        }

        foreach (var tier in dto.WeightTiers)
        {
            if (tier is < 0 or > 3)
            {
                return $"'weightTiers' contains out-of-range value {tier} (must be 0..3).";
            }
        }

        if (dto.ScenarioId is { Length: > MaxScenarioIdLength })
        {
            return $"'scenarioId' length {dto.ScenarioId.Length} exceeds the {MaxScenarioIdLength}-character cap.";
        }

        if (dto.RulesVersion is null || dto.RulesVersion.Length > MaxRulesVersionLength)
        {
            return $"'rulesVersion' is missing or exceeds the {MaxRulesVersionLength}-character cap.";
        }

        return null;
    }

    private static byte[] CompressBrotli(byte[] source)
    {
        try
        {
            var buffer = new byte[BrotliEncoder.GetMaxCompressedLength(source.Length)];
            if (!BrotliEncoder.TryCompress(source, buffer, out var bytesWritten, BrotliQuality, BrotliWindow))
            {
                // Compression cannot fail in practice for a buffer sized via GetMaxCompressedLength, but if it
                // ever did, falling back to the raw bytes keeps Encode total and lets the raw/compressed
                // size comparison naturally prefer the (larger) raw representation.
                return source;
            }

            return buffer[..bytesWritten];
        }
        catch (PlatformNotSupportedException)
        {
            // Some runtimes (notably the browser WebAssembly runtime without the wasm-tools workload) do not
            // provide native Brotli. Returning the source unchanged makes Encode fall back to the raw ('r')
            // representation, which needs no native compression and stays well within the size cap for this
            // tiny payload. Encode therefore works everywhere; it simply skips compression when unavailable.
            return source;
        }
    }

    private static bool TryDecompressBrotli(ReadOnlySpan<byte> source, int cap, out byte[] result)
    {
        var buffer = new byte[cap];
        try
        {
            if (BrotliDecoder.TryDecompress(source, buffer, out var bytesWritten))
            {
                result = buffer[..bytesWritten];
                return true;
            }
        }
        catch (PlatformNotSupportedException)
        {
            // Native Brotli is unavailable on this runtime (see CompressBrotli). A compressed ('c') payload
            // cannot be read here; report it as a failed decompression rather than throwing. In practice the
            // app only ever produces raw ('r') payloads on such runtimes, so this path is only reachable via
            // a hand-crafted link and is rejected cleanly.
        }

        result = [];
        return false;
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        foreach (var ch in value)
        {
            var isValid = (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch is '-' or '_';
            if (!isValid)
            {
                throw new FormatException($"Character '{ch}' is not valid base64url.");
            }
        }

        var base64 = value.Replace('-', '+').Replace('_', '/');
        var padded = (base64.Length % 4) switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => throw new FormatException("Base64url input has an invalid length."),
        };

        return Convert.FromBase64String(padded);
    }
}
