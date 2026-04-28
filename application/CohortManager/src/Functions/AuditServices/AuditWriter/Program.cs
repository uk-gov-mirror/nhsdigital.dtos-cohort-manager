using Microsoft.Extensions.Hosting;
using HealthChecks.Extensions;
using Common;
using NHS.CohortManager.AuditServices;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .AddConfiguration<AuditConfig>(out AuditConfig auditConfig)
	//No Need to register DataServicesContext as its already registered as part of AddDatabaseHealthCheck
    // .AddDataServicesHandler<DataServicesContext>()
    .AddServiceBusClient(auditConfig.ServiceBusConnectionString_client_internal)
    .ConfigureServices(services =>
    {
        services.AddBasicHealthCheck("AuditWriter");
    })
    .AddTelemetry()
    .Build();

await host.RunAsync();
