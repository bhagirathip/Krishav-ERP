namespace KrishavERP.Api.Modules.Auth;

public record LoginRequest(string Username, string Password);

public class RoleSaveRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<RolePermission> Permissions { get; set; } = new();
}

public class UserCreateRequest
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Password { get; set; } = "";
    public int RoleId { get; set; }
}

public class UserUpdateRequest
{
    public string DisplayName { get; set; } = "";
    // Leave blank to keep the current password unchanged.
    public string? Password { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
}
