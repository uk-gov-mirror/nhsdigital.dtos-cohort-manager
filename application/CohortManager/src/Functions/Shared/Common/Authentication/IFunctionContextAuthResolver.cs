namespace Common;

using Microsoft.Azure.Functions.Worker;

public interface IFunctionContextAuthResolver
{
    Cis2User? GetCis2User(FunctionContext context);

    bool IsAuthenticationRequired(FunctionContext context);

    Role[] GetRequiredRoles(FunctionContext context);
}
