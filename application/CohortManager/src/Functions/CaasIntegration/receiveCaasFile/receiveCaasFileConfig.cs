namespace NHS.Screening.ReceiveCaasFile;

using Common;
using System.ComponentModel.DataAnnotations;

public class ReceiveCaasFileConfig
{
    [Required]
    public required string DemographicDataServiceURL { get; set; }
    [Required]
    public required string ScreeningLkpDataServiceURL { get; set; }
    [Required]
    public required string DemographicURI { get; set; }
    [Required]
    public required int BatchSize { get; set; }
    public bool AllowDeleteRecords { get; set; }
    [Required]
    public required int maxNumberOfChecks { get; set; }
    public BlobStorageConfig? AzureWebJobsStorage { get; set; }
    public BlobStorageConfig? caasfolder_STORAGE { get; set; }
    [Required]
    public required string inboundBlobName { get; set; }
    [Required]
    public required string ServiceBusConnectionString_client_internal { get; set; }
    [Required]
    public required string GetOrchestrationStatusURL { get; set; }
    [Required]
    public required string ParticipantManagementTopic { get; set; }
}
