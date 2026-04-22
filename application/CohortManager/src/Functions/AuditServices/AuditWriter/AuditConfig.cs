namespace NHS.CohortManager.AuditServices;

using System.ComponentModel.DataAnnotations;

public class AuditConfig
{
    [Required]
    public required string ServiceBusConnectionString { get; set; }
    [Required]
    public required string AzureWebJobsStorage { get; set; }
    [Required]
    public required string AuditTopicName { get; set; }
}
