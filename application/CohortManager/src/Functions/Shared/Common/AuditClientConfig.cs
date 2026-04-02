namespace Common;

using System.ComponentModel.DataAnnotations;

public class AuditClientConfig
{
    [Required]
    public required string AuditTopicName { get; set; }
    public BlobStorageConfig? AzureWebJobsStorage { get; set; }
}
