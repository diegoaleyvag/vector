namespace Vector.Data;

/// <summary>
/// Thrown by <see cref="Mapping.KnowledgeMapper"/> when the parsed content JSON contains a value the
/// domain model cannot represent: an unrecognized enum member name, an unresolved mitigation-id
/// reference, an array of the wrong length, or a demand curve that does not match its levels array.
/// The message always names the offending field and value so authoring mistakes are easy to locate.
/// </summary>
public sealed class DataMappingException : Exception
{
    public DataMappingException(string message)
        : base(message)
    {
    }

    public DataMappingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
