namespace Common;

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;

public class FunctionContextAuthResolver : IFunctionContextAuthResolver
{
    private static readonly ConcurrentDictionary<string, FunctionEndpoint?> FunctionEndpointCache = new();

    public FunctionContextAuthResolver()
    {
    }

    public Cis2User? GetCis2User(FunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue("Cis2User", out var user) ? user as Cis2User : null;
    }

    public bool IsAuthenticationRequired(FunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var authAttribute = GetEndpoint(context)?.Metadata.GetMetadata<AuthenticationAttribute>();
        return authAttribute != null;
    }

    public Role[] GetRequiredRoles(FunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var authAttribute = GetEndpoint(context)?.Metadata.GetMetadata<AuthenticationAttribute>();
        return authAttribute?.Roles ?? Array.Empty<Role>();
    }

    private static FunctionEndpoint? GetEndpoint(FunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return FunctionEndpointCache.GetOrAdd(context.FunctionDefinition.EntryPoint, CreateEndpoint);
    }

    private static FunctionEndpoint? CreateEndpoint(string entryPoint)
    {
        if (string.IsNullOrWhiteSpace(entryPoint))
        {
            return null;
        }

        var separatorIndex = entryPoint.LastIndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == entryPoint.Length - 1)
        {
            return null;
        }

        var typeName = entryPoint[..separatorIndex];
        var methodName = entryPoint[(separatorIndex + 1)..];

        var declaringType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, throwOnError: false, ignoreCase: false))
            .FirstOrDefault(type => type != null);

        var method = declaringType?.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

        return method == null
            ? null
            : new FunctionEndpoint(method.GetCustomAttributes(inherit: true));
    }
}
