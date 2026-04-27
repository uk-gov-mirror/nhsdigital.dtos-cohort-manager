using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DataServices.Core;
using DataServices.Database;
using HealthChecks.Extensions;
using Common;
using NHS.CohortManager.AuditServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .AddConfiguration<AuditConfig>(out AuditConfig auditConfig)
    .AddDataServicesHandler<DataServicesContext>()
    .AddServiceBusClient(auditConfig.ServiceBusConnectionString)
    .ConfigureServices(services =>
    {
        services.AddTransient<IBlobStorageHelper, BlobStorageHelper>();
        services.AddDatabaseHealthCheck("AuditWriter");
    })
    .AddTelemetry()
    .Build();

await host.RunAsync();
