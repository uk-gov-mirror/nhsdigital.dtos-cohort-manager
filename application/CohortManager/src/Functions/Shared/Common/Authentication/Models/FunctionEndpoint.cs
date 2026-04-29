namespace Common;

public sealed class FunctionEndpoint
{
    public FunctionEndpoint(IEnumerable<object> metadata)
    {
        Metadata = new FunctionEndpointMetadataCollection(metadata);
    }

    public FunctionEndpointMetadataCollection Metadata { get; }
}
