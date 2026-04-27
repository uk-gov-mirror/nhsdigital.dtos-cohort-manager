namespace Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class AuditExtension
{
    public static IHostBuilder AddAuditLogging(this IHostBuilder hostBuilder, string serviceBusConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceBusConnectionString);

        hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddTransient<IAuditLogClient, AuditLogClient>();
        });

        hostBuilder.AddConfiguration<AuditClientConfig>(out var auditConfig);

        return hostBuilder
            .AddServiceBusClient(serviceBusConnectionString);
    }
}
