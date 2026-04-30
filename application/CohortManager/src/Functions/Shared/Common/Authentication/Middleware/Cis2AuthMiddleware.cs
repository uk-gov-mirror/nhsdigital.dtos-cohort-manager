namespace Common;

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class Cis2AuthMiddleware : IFunctionsWorkerMiddleware
{

    private readonly ILogger<Cis2AuthMiddleware> _logger;
    private readonly ICreateResponse _createResponse;
    private readonly IAuthenticationService _authService;
    private readonly AuthConfig _authConfig;
    private readonly IFunctionContextAuthResolver _authResolver;

    public Cis2AuthMiddleware(ILogger<Cis2AuthMiddleware> logger , ICreateResponse createResponse, IAuthenticationService authService, IOptions<AuthConfig> authConfig, IFunctionContextAuthResolver authResolver)
    {
        _logger = logger;
        _createResponse = createResponse;
        _authService = authService;
        _authConfig = authConfig.Value;
        _authResolver = authResolver;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if(_authConfig.ByPassAuthentication || !_authResolver.IsAuthenticationRequired(context))
        {
            _logger.LogInformation("Authentication is bypassed or not required for this endpoint, skipping authentication.");
            await next(context);
            return;
        }

        var cis2UserService = context.InstanceServices.GetRequiredService<ICis2UserService>();

        var req = await context.GetHttpRequestDataAsync();

        if(req == null)
        {
            throw new InvalidOperationException("HttpRequestData is required for authentication but was not found in the context.");
        }

        var accessToken = string.Empty;
        var tokensExist = AuthHelper.TryGetIdTokenFromHeaders(context, out var token);
        tokensExist = tokensExist && AuthHelper.TryGetAccessTokenFromHeaders(context, out accessToken);

        if(!tokensExist)
        {
            await HandleUnauthorizedAsync(context, req!, "Authorization header is missing or invalid", "Unauthorized: Missing or invalid Authorization header.");
            return;
        }

        var validateToken = await _authService.ValidateTokenAsync(token);

        if(!validateToken)
        {
            await HandleUnauthorizedAsync(context, req!, "Token validation failed", "Unauthorized: Invalid token.");
            return;
        }

        var cis2User = await cis2UserService.GetUserFromToken(accessToken);
        if(cis2User == null)
        {
            await HandleUnauthorizedAsync(context, req!, "Failed to retrieve user from token", "Unauthorized: Failed to retrieve user from token.");
            return;
        }

        context.Items["Cis2User"] = cis2User;
        context.Items["AuthToken"] = token;
        await next(context);
    }

    private async Task HandleUnauthorizedAsync(FunctionContext context, HttpRequestData request, string logMessage, string responseMessage)
    {
        _logger.LogWarning("Authentication Error: {LogMessage}", logMessage);
        var response = await _createResponse.CreateHttpResponseWithBodyAsync(HttpStatusCode.Unauthorized, request, responseMessage);
        context.GetInvocationResult().Value = response;
    }
}
