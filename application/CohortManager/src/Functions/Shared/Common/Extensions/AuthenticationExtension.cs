namespace Common;

using Hl7.Fhir.Model.CdsHooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class AuthenticationExtension
{
    public static IHostBuilder AddAuthentication(this IHostBuilder hostBuilder)
    {

        hostBuilder.AddConfiguration<AuthConfig>(out var authConfig);
        if(!authConfig.ByPassAuthentication)
        {
            hostBuilder.AddConfiguration<RoleConfig>();
            hostBuilder.ConfigureFunctionsWorkerDefaults(workerOptions =>
            {
                workerOptions.UseMiddleware<Cis2AuthMiddleware>();
                workerOptions.UseMiddleware<PermissionsMiddleware>();
            });
            hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<IFunctionContextAuthResolver, FunctionContextAuthResolver>();
                services.AddSingleton<IAuthenticationService, JwtAuthentication>();
                services.AddScoped<ICis2UserService,Cis2UserService>();
                services.AddSingleton<IRoleManager, RoleManager>();
            });
        }
        else
        {
            hostBuilder.ConfigureFunctionsWorkerDefaults();
        }
        return hostBuilder;
     }
}
