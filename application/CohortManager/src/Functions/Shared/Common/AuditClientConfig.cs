namespace Common;

using System.ComponentModel.DataAnnotations;

public class AuditClientConfig
{
    [Required]
    public required string AuditTopicName { get; set; }
    [Required]
    public required string AzureWebJobsStorage { get; set; }
}
