namespace NHS.CohortManager.CohortDistributionServices;

using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides cached access to the allocation config data.
/// Registered as a singleton so the file is read once and reused across calls.
/// </summary>
public class AllocationConfigProvider(ILogger<AllocationConfigProvider> logger) : IAllocationConfigProvider
{
    private AllocationConfigDataList? _cachedConfig;
    private readonly ILogger<AllocationConfigProvider> _logger = logger;

    public async Task<AllocationConfigDataList> GetConfigAsync()
    {
        if (_cachedConfig is not null)
        {
            return _cachedConfig;
        }

        var configFilePath = Path.Combine(Environment.CurrentDirectory, "AllocateServiceProvider", "allocationConfig.json");
        var configFile = await File.ReadAllTextAsync(configFilePath);
        _cachedConfig = JsonSerializer.Deserialize<AllocationConfigDataList>(configFile)!;
        _logger.LogInformation("Allocation config loaded from file and cached");

        return _cachedConfig;
    }
}
