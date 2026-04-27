namespace Common;

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;

public static class FunctionContextExtension
{
    public static bool RequiresAuthentication(this FunctionContext context)
    {
        var authAttribute = context.GetEndpoint()?.Metadata.GetMetadata<AuthenticationAttribute>();
        return authAttribute != null;
    }

    public static Role[] GetRequiredRoles(this FunctionContext context)
    {
        var authAttribute = context.GetEndpoint()?.Metadata.GetMetadata<AuthenticationAttribute>();
        return authAttribute?.Roles ?? Array.Empty<Role>();
    }

    public static Cis2User? GetUser(this FunctionContext context)
    {
        return context.Items.TryGetValue("Cis2User", out var user) ? user as Cis2User : null;
    }

    public static FunctionEndpoint? GetEndpoint(this FunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return FunctionEndpointCache.GetOrAdd(context.FunctionDefinition.EntryPoint, CreateEndpoint);
    }

    private static readonly ConcurrentDictionary<string, FunctionEndpoint?> FunctionEndpointCache = new();

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

public sealed class FunctionEndpoint
{
    public FunctionEndpoint(IEnumerable<object> metadata)
    {
        Metadata = new FunctionEndpointMetadataCollection(metadata);
    }

    public FunctionEndpointMetadataCollection Metadata { get; }
}

public sealed class FunctionEndpointMetadataCollection
{
    private readonly IReadOnlyList<object> _metadata;

    public FunctionEndpointMetadataCollection(IEnumerable<object> metadata)
    {
        _metadata = metadata.ToArray();
    }

    public T? GetMetadata<T>() where T : class
    {
        return _metadata.OfType<T>().FirstOrDefault();
    }
}
