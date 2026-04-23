namespace NHS.CohortManager.CohortDistributionServices;

using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides cached access to the allocation config data.
/// Registered as a singleton so the file is read once and reused across calls.
/// Config is loaded eagerly in the constructor and the provider does not mutate it after creation.
/// Callers receive the cached instance returned by reference, so this type does not guarantee that the config object itself is immutable.
/// </summary>
public class AllocationConfigProvider : IAllocationConfigProvider
{
    private readonly AllocationConfigDataList _cachedConfig;

    public AllocationConfigProvider(ILogger<AllocationConfigProvider> logger)
    {
        var configFilePath = Path.Combine(Environment.CurrentDirectory, "AllocateServiceProvider", "allocationConfig.json");
        var configFile = File.ReadAllText(configFilePath);
        _cachedConfig = JsonSerializer.Deserialize<AllocationConfigDataList>(configFile)
            ?? throw new InvalidOperationException($"Failed to deserialize allocation config from '{configFilePath}'. The file content was null or invalid.");
        logger.LogInformation("Allocation config loaded from file and cached");
    }
    
    public AllocationConfigDataList GetConfig() => _cachedConfig;
}
