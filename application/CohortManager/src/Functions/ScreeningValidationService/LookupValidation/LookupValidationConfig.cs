namespace NHS.CohortManager.ScreeningValidationService;

using Common;

public class LookupValidationConfig
{
    public string ExceptionFunctionUrl {get;set;}
    public string BsSelectGpPracticeUrl {get;set;}
    public string BsSelectOutCodeUrl {get;set;}
    public string CurrentPostingUrl {get;set;}
    public string ExcludedSMULookupUrl {get;set;}
    public string BlobContainerName {get;set;}
    public BlobStorageConfig? AzureWebJobsStorage {get;set;}
}
