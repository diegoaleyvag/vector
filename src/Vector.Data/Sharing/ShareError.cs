namespace Vector.Data.Sharing;

/// <summary>Reasons <see cref="ShareCodec.Decode(string?)"/> can fail to recover a <see cref="SharePayload"/>.</summary>
public enum ShareError
{
    /// <summary>Decoding succeeded; no error.</summary>
    None = 0,

    /// <summary>The input fragment was null or empty.</summary>
    Empty,

    /// <summary>The version prefix was missing or is not the currently supported version.</summary>
    BadVersion,

    /// <summary>The encoded body was not valid base64url.</summary>
    BadEncoding,

    /// <summary>The encoded fragment (after the version prefix) exceeded <see cref="ShareCodec.MaxEncodedChars"/>.</summary>
    TooLarge,

    /// <summary>The payload claimed to be Brotli-compressed but could not be decompressed within the size cap.</summary>
    DecompressFailed,

    /// <summary>The decompressed bytes were not valid JSON for the payload shape, or failed semantic validation.</summary>
    SchemaInvalid,
}
