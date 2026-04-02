namespace NHS.Screening.NemsMeshRetrieval;

using Common;
using System.ComponentModel.DataAnnotations;

public class NemsMeshRetrievalConfig
{
    public string NemsMeshApiBaseUrl { get; set; }
    [Required]
    public required string NemsMeshMailBox { get; set; }
    [Required]
    public required string NemsMeshPassword { get; set; }
    [Required]
    public required string NemsMeshSharedKey {get; set;}
    public string NemsMeshKeyPassphrase {get; set;}
    public string NemsMeshKeyName {get; set;}
    public string KeyVaultConnectionString {get; set;}
    public BlobStorageConfig? AzureWebJobsStorage { get; set; }
    public BlobStorageConfig? nemsmeshfolder_STORAGE { get; set;}
    public string NemsMeshInboundContainer { get; set; } = "nems-updates";
    public string NemsMeshConfigContainer { get; set; } = "nems-config";
    public string NemsMeshServerSideCerts { get; set; }
    public string NemsMeshCertName { get; set; }
    public bool? NemsMeshBypassServerCertificateValidation {get;set;}
}
