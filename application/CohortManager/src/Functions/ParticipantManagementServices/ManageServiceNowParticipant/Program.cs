using Common;
using DataServices.Client;
using HealthChecks.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Model;
using NHS.CohortManager.ParticipantManagementServices;

var host = new HostBuilder()
    .AddConfiguration(out ManageServiceNowParticipantConfig config)
    .AddConfiguration<AuditClientConfig>()
        .AddDataServicesHandler()
        .AddDataService<ParticipantManagement>(config.ParticipantManagementURL)
        .Build()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Register health checks
        services.AddBasicHealthCheck("ManageServiceNowParticipant");
        services.AddTransient<IBlobStorageHelper, BlobStorageHelper>();
    })
    .AddTelemetry()
    .AddExceptionHandler()
    .AddAuditLogging(config.ServiceBusConnectionString_client_internal)
    .AddHttpClient()
    .Build();

await host.RunAsync();
