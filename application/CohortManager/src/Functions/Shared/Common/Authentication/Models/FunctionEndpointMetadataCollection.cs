namespace Common;

public sealed class FunctionEndpointMetadataCollection
{
    private readonly IReadOnlyList<object> _metadata;

    public FunctionEndpointMetadataCollection(IEnumerable<object> metadata)
    {
        _metadata = metadata.ToArray();
    }

    public T? GetMetadata<T>() where T : class
    {
        return _metadata.OfType<T>().FirstOrDefault();
    }
}
