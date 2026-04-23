namespace Common;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

/// <summary>
/// Filters health check telemetry emitted by the worker process.
/// Note: In the isolated worker model, RequestTelemetry for HTTP triggers is emitted
/// by the host process and does not flow through this processor.
/// Host-emitted request telemetry cannot be filtered here — see
/// https://github.com/Azure/azure-functions-dotnet-worker/issues/2024
/// Trace telemetry is filtered via host.json logLevel settings.
/// </summary>
public class HealthCheckFilterTelemetryProcessor : ITelemetryProcessor, ITelemetryInitializer
{
    private readonly ITelemetryProcessor _next;

    public HealthCheckFilterTelemetryProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        if (IsHealthCheckTelemetry(item) && !IsException(item))
        {
            return;
        }

        _next.Process(item);
    }

    private static bool IsHealthCheckTelemetry(ITelemetry item)
    {
        //request telemetry is emitted by the host process and does not flow through this processor,
        //so we won't see those here to filter on. We can only filter on dependency and trace telemetry emitted by the worker process.
        //this is confirmed by microsoft support in this GitHub issue https://github.com/Azure/azure-functions-dotnet-worker/issues/2024
        if (item is RequestTelemetry request)
        {
            return request.Name?.Equals("health", StringComparison.OrdinalIgnoreCase) == true ||
                   request.Url?.AbsolutePath.Contains("/health", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (item is DependencyTelemetry dependency)
        {
            return dependency.Data?.Contains("/health", StringComparison.OrdinalIgnoreCase) == true ||
                   dependency.Name?.Contains("health", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (item is TraceTelemetry trace)
        {
            if (trace.Message?.Contains("Functions.health", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (trace.Properties.TryGetValue("CategoryName", out var categoryName) &&
                categoryName.Contains("Health", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsException(ITelemetry item)
    {
        if (item is RequestTelemetry request)
        {
            return !request.Success.GetValueOrDefault(true);
        }

        if (item is TraceTelemetry trace)
        {
            return trace.SeverityLevel >= SeverityLevel.Error;
        }

        return item is ExceptionTelemetry;
    }

    public void Initialize(ITelemetry telemetry)
    {
        throw new NotImplementedException();
    }
}
