namespace HealthChecks.Extensions;

using Azure.Identity;
using Azure.Storage.Blobs;
using Common;
using Microsoft.Extensions.DependencyInjection;

public static class BlobStorageHealthCheckExtension
{
    public static IServiceCollection AddBlobStorageHealthCheck(this IServiceCollection services, string name, BlobStorageConfig azureWebJobsStorage)
    {
        // Register blob storage health checks
        services.AddHealthChecks()
            .AddCheck<BlobStorageHealthCheck>(
                "Storage HealthCheck For " + name,
                tags: new[] { "Blob", "Azure Storage" });
        // Register BlobServiceClient service for health check
        services.AddSingleton<BlobServiceClient>(provider =>
        {
            if (azureWebJobsStorage == null)
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                return new BlobServiceClient(connectionString);
            }
            return new BlobServiceClient(new Uri(azureWebJobsStorage.BlobServiceUri), new DefaultAzureCredential());
        });

        return services;
    }
}
