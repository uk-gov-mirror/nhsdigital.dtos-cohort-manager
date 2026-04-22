namespace Common;

[AttributeUsage(AttributeTargets.Method)]
public class AuthenticationAttribute : Attribute
{
    public Role[]? Roles { get; }
    public bool RequiresAuthentication => Roles != null && Roles.Length > 0;
    public AuthenticationAttribute(Role role)
    {
        Roles = new[] { role };
    }
    public AuthenticationAttribute(Role[] roles)
    {
        Roles = roles;
    }
    public AuthenticationAttribute()
    {
        Roles = null;
    }
}
