using Common;
using Data.Database;
using DataServices.Client;
using HealthChecks.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Model;
using NHS.Screening.DeleteParticipant;

var host = new HostBuilder()
    .AddConfiguration<DeleteParticipantConfig>(out DeleteParticipantConfig config)
    .ConfigureFunctionsWebApplication()
    .AddDataServicesHandler()
        .AddDataService<CohortDistribution>(config.CohortDistributionDataServiceUrl)
        .Build()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ICreateResponse, CreateResponse>();
        services.AddSingleton<IDatabaseHelper, DatabaseHelper>();
        // Register health checks
        services.AddBasicHealthCheck("DeleteParticipant");
    })
    .AddExceptionHandler()
    .AddHttpClient()
    .AddTelemetry()
    .Build();


await host.RunAsync();
