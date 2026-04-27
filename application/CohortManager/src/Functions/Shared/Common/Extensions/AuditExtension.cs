namespace Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class AuditExtension
{
    public static IHostBuilder AddAuditLogging(this IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddTransient<IAuditLogClient, AuditLogClient>();
        });

        return hostBuilder;
    }

    public static IHostBuilder AddAuditLogging(this IHostBuilder hostBuilder, string serviceBusConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceBusConnectionString);

        return hostBuilder
            .AddAuditLogging()
            .AddServiceBusClient(serviceBusConnectionString);
    }
}
