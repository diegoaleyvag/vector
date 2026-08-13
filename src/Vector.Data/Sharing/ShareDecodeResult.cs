namespace Vector.Data.Sharing;

/// <summary>The outcome of <see cref="ShareCodec.Decode(string?)"/>: either a recovered payload, or a specific error.</summary>
public sealed record ShareDecodeResult(bool Ok, SharePayload? Payload, ShareError Error, string? Message)
{
    public static ShareDecodeResult Success(SharePayload payload) => new(true, payload, ShareError.None, null);

    public static ShareDecodeResult Fail(ShareError error, string message) => new(false, null, error, message);
}
