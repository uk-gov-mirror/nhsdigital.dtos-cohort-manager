namespace NHS.Screening.RetrieveMeshFile;

using Common;
using System.ComponentModel.DataAnnotations;

public class RetrieveMeshFileConfig
{
    public string? MeshApiBaseUrl { get; set; }
    [Required]
    public required string BSSMailBox { get; set; }
    [Required]
    public string? MeshPassword { get; set; }
    [Required]
    public required string MeshSharedKey { get; set; }
    public string? MeshKeyPassphrase { get; set; }
    public string? MeshKeyName { get; set; }
    public string KeyVaultConnectionString { get; set; }
    public BlobStorageConfig? AzureWebJobsStorage { get; set; }
    public BlobStorageConfig? nemsmeshfolder_STORAGE { get; set; }
    public string? ServerSideCerts { get; set; }
    public string? MeshCertName { get; set; }
    public bool? BypassServerCertificateValidation { get; set; }
}
