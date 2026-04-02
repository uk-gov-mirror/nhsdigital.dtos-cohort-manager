namespace NHS.CohortManager.ServiceNowIntegrationService;

using Common;
using System.ComponentModel.DataAnnotations;

public class ServiceNowCohortLookupConfig
{
    [Required]
    public required string ServiceNowCasesDataServiceURL { get; set; }

    [Required]
    public required string CohortDistributionDataServiceURL { get; set; }

    public BlobStorageConfig? AzureWebJobsStorage { get; set; }
}
