namespace NHS.CohortManager.CohortDistributionServices;

public interface IAllocationConfigProvider
{
    AllocationConfigDataList GetConfig();
}
