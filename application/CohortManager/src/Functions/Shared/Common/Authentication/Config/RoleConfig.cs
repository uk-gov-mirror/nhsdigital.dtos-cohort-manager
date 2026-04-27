namespace Common;

using System.ComponentModel.DataAnnotations;

public class RoleConfig
{
    [Required]
    public required string CohortManagerUserWorkgroupId { get; init; }
    [Required]
    public required string CohortManagerDummyGpRemovalWorkgroupId { get; init; }
}
