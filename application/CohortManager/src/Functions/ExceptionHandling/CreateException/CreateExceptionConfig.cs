namespace NHS.Screening.CreateException;

using System.ComponentModel.DataAnnotations;

public class CreateExceptionConfig
{
    [Required]
    public required string ExceptionManagementDataServiceURL {get; set;}
    [Required]
    public required string DemographicDataServiceURL {get; set;}
}
