namespace Common;

public class BlobStorageConfig
{
    public required string AccountName { get; set; }
    public required string BlobServiceUri { get; set; }
    public required string QueueServiceUri { get; set; }
}
