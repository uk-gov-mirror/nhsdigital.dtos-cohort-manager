using Microsoft.Extensions.Hosting;
using HealthChecks.Extensions;
using Common;
using NHS.CohortManager.AuditServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .AddConfiguration<AuditConfig>(out AuditConfig auditConfig)
    // .AddDataServicesHandler<DataServicesContext>()
    .AddServiceBusClient(auditConfig.ServiceBusConnectionString_client_internal)
    .ConfigureServices(services =>
    {
        services.AddDatabaseHealthCheck("AuditWriter");
    })
    .AddTelemetry()
    .Build();

await host.RunAsync();
