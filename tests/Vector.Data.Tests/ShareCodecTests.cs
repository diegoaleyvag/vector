using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Vector.Data.Dtos;
using Vector.Data.Serialization;
using Vector.Data.Sharing;

namespace Vector.Data.Tests;

public class ShareCodecTests
{
    private static SharePayload MakePayload(int[] levels, int[] tiers, bool[] hard, string? scenarioId = "scn.example", string rulesVersion = "1.0.0") =>
        new(scenarioId, [.. levels], [.. tiers], [.. hard], rulesVersion);

    public static IEnumerable<object[]> BoundaryPayloads()
    {
        yield return [MakePayload([0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0], [false, false, false, false, false, false, false, false], null)];
        yield return [MakePayload([4, 4, 4, 4, 4, 4, 4, 4], [3, 3, 3, 3, 3, 3, 3, 3], [true, true, true, true, true, true, true, true], "scn.all-max")];
        yield return [MakePayload([1, 2, 3, 0, 4, 2, 1, 3], [0, 1, 2, 3, 1, 2, 0, 3], [true, false, true, false, true, false, true, false], "scn.mixed")];
        yield return [MakePayload([2, 2, 1, 1, 2, 1, 1, 1], [3, 2, 1, 1, 3, 1, 2, 1], [false, false, false, false, false, false, false, false], "scn.policy-assistant")];
    }

    [Theory]
    [MemberData(nameof(BoundaryPayloads))]
    public void EncodeThenDecode_RoundTrips_ForBoundaryPayloads(SharePayload payload)
    {
        var fragment = ShareCodec.Encode(payload);
        var result = ShareCodec.Decode(fragment);

        Assert.True(result.Ok);
        Assert.Equal(ShareError.None, result.Error);
        Assert.Equal(payload, result.Payload);
    }

    [Fact]
    public void EncodeThenDecode_RoundTrips_ForManyRandomValidPayloads()
    {
        var random = new Random(1234567);

        for (var i = 0; i < 500; i++)
        {
            var levels = Enumerable.Range(0, 8).Select(_ => random.Next(0, 5)).ToArray();
            var tiers = Enumerable.Range(0, 8).Select(_ => random.Next(0, 4)).ToArray();
            var hard = Enumerable.Range(0, 8).Select(_ => random.Next(0, 2) == 1).ToArray();
            var scenarioId = random.Next(0, 3) == 0 ? null : $"scn.random-{i}";
            var payload = MakePayload(levels, tiers, hard, scenarioId);

            var fragment = ShareCodec.Encode(payload);
            var result = ShareCodec.Decode(fragment);

            Assert.True(result.Ok);
            Assert.Equal(payload, result.Payload);
        }
    }

    [Fact]
    public void Encode_IsDeterministic_SamePayloadYieldsSameString()
    {
        var payload = MakePayload([2, 2, 1, 1, 2, 1, 1, 1], [3, 2, 1, 1, 3, 1, 2, 1], [false, false, false, false, false, false, false, false]);

        var first = ShareCodec.Encode(payload);
        var second = ShareCodec.Encode(payload);

        Assert.Equal(first, second);
    }

    [Theory]
    [MemberData(nameof(BoundaryPayloads))]
    public void Encode_PicksTheSmallerOfRawAndCompressed(SharePayload payload)
    {
        // Independently reproduce, using only public APIs, the exact bytes ShareCodec would have
        // produced for each representation, then check the fragment's header byte reflects whichever
        // representation is actually smaller.
        var dto = new SharePayloadDto
        {
            ScenarioId = payload.ScenarioId,
            Levels = [.. payload.Levels],
            WeightTiers = [.. payload.WeightTiers],
            Hard = [.. payload.Hard],
            RulesVersion = payload.RulesVersion,
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(dto, VectorJsonContext.Default.SharePayloadDto);

        var compressedBuffer = new byte[BrotliEncoder.GetMaxCompressedLength(json.Length)];
        BrotliEncoder.TryCompress(json, compressedBuffer, out var compressedLength, quality: 5, window: 22);

        var expectedHeader = compressedLength < json.Length ? (byte)'c' : (byte)'r';

        var fragment = ShareCodec.Encode(payload);
        var headerByte = DecodeHeaderByteForTest(fragment);

        Assert.Equal(expectedHeader, headerByte);
    }

    private static byte DecodeHeaderByteForTest(string fragment)
    {
        var encoded = fragment[(fragment.IndexOf('.') + 1)..];
        var base64 = encoded.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64,
        };
        var bytes = Convert.FromBase64String(base64);
        return bytes[0];
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decode_EmptyOrNullFragment_ReturnsEmptyError(string? fragment)
    {
        var result = ShareCodec.Decode(fragment);

        Assert.False(result.Ok);
        Assert.Equal(ShareError.Empty, result.Error);
        Assert.Null(result.Payload);
    }

    [Theory]
    [InlineData("v2.someEncodedThing")]
    [InlineData("v0.abc")]
    [InlineData("noDotAtAllHere")]
    public void Decode_BadVersion_ReturnsBadVersionError(string fragment)
    {
        var result = ShareCodec.Decode(fragment);

        Assert.False(result.Ok);
        Assert.Equal(ShareError.BadVersion, result.Error);
    }

    [Fact]
    public void Decode_EncodedFragmentOverSizeCap_ReturnsTooLarge_WithoutAttemptingDecode()
    {
        var oversized = "v1." + new string('A', ShareCodec.MaxEncodedChars + 1);

        var result = ShareCodec.Decode(oversized);

        Assert.False(result.Ok);
        Assert.Equal(ShareError.TooLarge, result.Error);
    }

    [Theory]
    [InlineData("v1.not_valid!!chars$$")]
    [InlineData("v1.####")]
    public void Decode_InvalidBase64Url_ReturnsBadEncoding(string fragment)
    {
        var result = ShareCodec.Decode(fragment);

        Assert.False(result.Ok);
        Assert.Equal(ShareError.BadEncoding, result.Error);
    }

    [Fact]
    public void Decode_CompressedPayloadExpandingPastCap_ReturnsDecompressFailed()
    {
        // Highly compressible data that decompresses to well over the 4096-byte cap.
        var oversizedSource = Encoding.UTF8.GetBytes(new string('0', 20_000));
        var compressedBuffer = new byte[BrotliEncoder.GetMaxCompressedLength(oversizedSource.Length)];
        BrotliEncoder.TryCompress(oversizedSource, compressedBuffer, out var compressedLength, quality: 5, window: 22);

        var withHeader = new byte[1 + compressedLength];
        withHeader[0] = (byte)'c';
        compressedBuffer.AsSpan(0, compressedLength).CopyTo(withHeader.AsSpan(1));

        var fragment = "v1." + Base64UrlEncodeForTest(withHeader);

        var result = ShareCodec.Decode(fragment);

        Assert.False(result.Ok);
        Assert.Equal(ShareError.DecompressFailed, result.Error);
    }

    private static string Base64UrlEncodeForTest(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Theory]
    [MemberData(nameof(SemanticInvalidPayloads))]
    public void Decode_SemanticallyInvalidPayload_ReturnsSchemaInvalid(SharePayload payload)
    {
        var fragment = ShareCodec.Encode(payload);
        var result = ShareCodec.Decode(fragment);

        Assert.False(result.Ok);
        Assert.Equal(ShareError.SchemaInvalid, result.Error);
        Assert.NotNull(result.Message);
    }

    public static IEnumerable<object[]> SemanticInvalidPayloads()
    {
        // 7 levels instead of 8.
        yield return [new SharePayload("scn.x", ImmutableArray.Create(0, 1, 2, 3, 4, 0, 1), ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(false, false, false, false, false, false, false, false), "1.0.0")];
        // Weight tier out of range (4, valid range is 0..3).
        yield return [new SharePayload("scn.x", ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(4, 0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(false, false, false, false, false, false, false, false), "1.0.0")];
        // Level out of range (5, valid range is 0..4).
        yield return [new SharePayload("scn.x", ImmutableArray.Create(5, 0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(false, false, false, false, false, false, false, false), "1.0.0")];
        // Over-long scenario id (65 chars, cap is 64).
        yield return [new SharePayload(new string('x', 65), ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(false, false, false, false, false, false, false, false), "1.0.0")];
    }

    [Fact]
    public void Decode_NeverThrows_ForThousandsOfRandomFuzzStrings()
    {
        var random = new Random(987654321);
        var alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.!@#$%^&*()[]{}<> \t\r\né中";

        for (var i = 0; i < 5000; i++)
        {
            var length = random.Next(0, 200);
            var chars = new char[length];
            for (var j = 0; j < length; j++)
            {
                chars[j] = alphabet[random.Next(alphabet.Length)];
            }

            var candidate = new string(chars);

            var result = ShareCodec.Decode(candidate);

            Assert.NotNull(result);
            Assert.Equal(result.Error == ShareError.None, result.Ok);
            if (result.Ok)
            {
                Assert.NotNull(result.Payload);
            }
            else
            {
                Assert.Null(result.Payload);
            }
        }
    }

    [Fact]
    public void FromProfile_ToProfile_RoundTrips_CanonicalOrder()
    {
        var (rules, scenarios) = KnowledgeLoader.Parse(ContentFile.ReadAllText());
        var scenario = scenarios.Single(s => s.Id == "scn.policy-assistant");

        var payload = ShareCodec.FromProfile(scenario.Profile, rules.RulesVersion, scenario.Id);
        var roundTripped = ShareCodec.ToProfile(payload);

        // ImmutableArray<T> implements IEquatable<ImmutableArray<T>> via reference equality of the
        // backing array, which Assert.Equal would otherwise prefer over element-wise comparison -
        // compare as plain arrays instead so this is a real structural check.
        Assert.Equal(scenario.Profile.Settings.ToArray(), roundTripped.Settings.ToArray());
    }
}
