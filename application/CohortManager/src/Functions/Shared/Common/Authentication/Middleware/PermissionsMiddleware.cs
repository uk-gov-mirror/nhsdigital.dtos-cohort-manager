namespace Common;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class PermissionsMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ICreateResponse _createResponse;
    private readonly IRoleManager _roleManager;
    private readonly ILogger<PermissionsMiddleware> _logger;
    private readonly AuthConfig _authConfig;
    private readonly IFunctionContextAuthResolver _authResolver;

    public PermissionsMiddleware(ICreateResponse createResponse, IRoleManager roleManager, ILogger<PermissionsMiddleware> logger, IOptions<AuthConfig> authConfig, IFunctionContextAuthResolver authResolver)
    {
        _createResponse = createResponse;
        _roleManager = roleManager;
        _logger = logger;
        _authConfig = authConfig.Value;
        _authResolver = authResolver;
     }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if(_authConfig.ByPassAuthentication)
        {
            _logger.LogInformation("Authentication is bypassed, skipping permissions check.");
            await next(context);
            return;
        }

        if (!_authResolver.IsAuthenticationRequired(context))
        {
            _logger.LogInformation("No authentication required for this endpoint, skipping permissions check.");
            await next(context);
            return;
        }
        var requiredRoles = _authResolver.GetRequiredRoles(context);

        if(requiredRoles.Length == 0)
        {
            _logger.LogInformation("No specific roles required for this endpoint, skipping permissions check.");
            await next(context);
            return;
        }

        var req = await context.GetHttpRequestDataAsync();

        if(req == null)
        {
            throw new InvalidOperationException("HttpRequestData is required for permissions check but was not found in the context.");
        }

        var user = (Cis2User)context.Items["Cis2User"]!;

        if (requiredRoles.Any(role => _roleManager.ValidateRole(user, role)))
        {
            await next(context);
            return;
        }

        await HandleUnauthorizedAsync(context, req, $"User {user.Uid} does not have required roles to access this resource.", "Forbidden: You do not have permission to access this resource.");
        return;
    }

    private async Task HandleUnauthorizedAsync(FunctionContext context, HttpRequestData request, string logMessage, string responseMessage)
    {
        _logger.LogWarning("Permissions Error: {LogMessage}", logMessage);
        var response = await _createResponse.CreateHttpResponseWithBodyAsync(System.Net.HttpStatusCode.Forbidden, request, responseMessage);
        context.GetInvocationResult().Value = response;
    }
}
