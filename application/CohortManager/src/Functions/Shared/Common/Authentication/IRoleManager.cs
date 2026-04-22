namespace Common;

public interface IRoleManager
{
    public bool ValidateRole(Cis2User user, Role role);
}
