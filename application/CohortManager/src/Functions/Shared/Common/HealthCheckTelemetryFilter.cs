namespace Common;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

public class HealthCheckFilterTelemetryProcessor : ITelemetryProcessor
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
}
