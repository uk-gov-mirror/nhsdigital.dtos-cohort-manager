namespace NHS.Screening.ProcessNemsUpdate;

using Common;
using System.ComponentModel.DataAnnotations;

public class ProcessNemsUpdateConfig
{
    [Required]
    public required string RetrievePdsDemographicURL { get; set; }

    [Required]
    public required string NemsMessages { get; set; }

    [Required]
    public required string ManageNemsSubscriptionSubscribeURL { get; set; }

    [Required]
    public required string ManageNemsSubscriptionUnsubscribeURL { get; set; }

    [Required]
    public required string ServiceBusConnectionString_client_internal { get; set; }

    [Required]
    public required string ParticipantManagementTopic { get; set; }

    [Required]
    public required string DemographicDataServiceURL { get; set; }

    public BlobStorageConfig? AzureWebJobsStorage { get; set; }
    public BlobStorageConfig? nemsmeshfolder_STORAGE { get; set; }

    public string NemsPoisonContainer { get; set; } = "nems-poison";
}
