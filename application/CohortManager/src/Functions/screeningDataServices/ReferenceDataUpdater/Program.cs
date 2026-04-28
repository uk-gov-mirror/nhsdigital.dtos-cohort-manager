using Common;
using DataServices.Core;
using DataServices.Database;
using HealthChecks.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReferenceDataUpdater;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .AddDataServicesHandler<DataServicesContext>()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IBlobStorageHelper, BlobStorageHelper>();
        services.AddScoped<IReferenceDataInsertHandler, ReferenceDataInsertHandler>();
        services.AddDatabaseHealthCheck("ReferenceDataUpdater");
    })
    .AddTelemetry()
    .Build();

await host.RunAsync();
