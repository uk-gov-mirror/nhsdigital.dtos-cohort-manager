namespace NHS.CohortManager.AuditServices;

using Common;
using System.ComponentModel.DataAnnotations;

public class AuditConfig
{
    [Required]
    public required string ServiceBusConnectionString { get; set; }
    public BlobStorageConfig? AzureWebJobsStorage { get; set; }
    [Required]
    public required string AuditTopicName { get; set; }
}
