namespace Common;

using Microsoft.Extensions.Options;

public class RoleManager : IRoleManager
{
    private readonly Dictionary<string, Role> _roleMappings;

    public RoleManager(IOptions<RoleConfig> roleConfig)
    {
        _roleMappings = new Dictionary<string, Role>
        {
            { roleConfig.Value.CohortManagerUserWorkgroupId, Role.CohortManagerUser },
            { roleConfig.Value.CohortManagerDummyGpRemovalWorkgroupId, Role.CohortManagerDummyGpRemoval }
        };
    }
    public bool ValidateRole(Cis2User user, Role role)
    {
        var workgroupId = _roleMappings.FirstOrDefault(x => x.Value == role).Key;

        if (workgroupId == null)
        {
            return false;
        }
        return user.NhsidNrbacRoles.Any(x => x.WorkgroupsCodes.Contains(workgroupId));
    }

}
