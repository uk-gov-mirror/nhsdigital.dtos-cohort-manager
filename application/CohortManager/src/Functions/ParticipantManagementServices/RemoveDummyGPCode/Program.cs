using Common;
using HealthChecks.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NHS.CohortManager.ParticipantManagementServices;

var host = new HostBuilder()
    .AddConfiguration(out RemoveDummyGpCodeConfig config)
    .AddAuthentication()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ICreateResponse, CreateResponse>();
        services.AddBasicHealthCheck("RemoveDummyGPCode");
    })
    .AddAuditLogging(config.ServiceBusConnectionString_client_internal)
    .AddTelemetry()
    .AddHttpClient()
    .AddServiceBusClient(config.ServiceBusConnectionString_client_internal)
    .Build();

await host.RunAsync();
