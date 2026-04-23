namespace NHS.CohortManager.CohortDistributionServices;

public interface IAllocationConfigProvider
{
    Task<AllocationConfigDataList> GetConfigAsync();
}
