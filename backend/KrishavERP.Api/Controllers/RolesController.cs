using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RolesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("modules")]
    public IActionResult Modules()
    {
        return Ok(DbSeeder.Modules);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var roles = await _db.AppRoles
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var result = new List<object>();

        foreach (var role in roles)
        {
            var permissions = await _db.RolePermissions
                .Where(x => x.RoleId == role.Id)
                .OrderBy(x => x.Module)
                .ToListAsync();

            result.Add(new
            {
                role.Id,
                role.Name,
                role.Description,
                Permissions = permissions
            });
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Add(RoleSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Role name is required." });
        }

        var exists = await _db.AppRoles
            .AnyAsync(x => x.Name == request.Name && x.IsActive);

        if (exists)
        {
            return Conflict(new { message = "Role already exists." });
        }

        var role = new AppRole
        {
            Name = request.Name.Trim(),
            Description = request.Description
        };

        _db.AppRoles.Add(role);
        await _db.SaveChangesAsync();

        foreach (var permission in request.Permissions)
        {
            permission.Id = 0;
            permission.RoleId = role.Id;
            _db.RolePermissions.Add(permission);
        }

        await _db.SaveChangesAsync();
        return Ok(role);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, RoleSaveRequest request)
    {
        var role = await _db.AppRoles.FindAsync(id);
        if (role == null)
        {
            return NotFound(new { message = "Designation not found." });
        }

        role.Name = request.Name.Trim();
        role.Description = request.Description;

        var existingPermissions = await _db.RolePermissions
            .Where(x => x.RoleId == id)
            .ToListAsync();

        _db.RolePermissions.RemoveRange(existingPermissions);

        foreach (var permission in request.Permissions)
        {
            permission.Id = 0;
            permission.RoleId = id;
            _db.RolePermissions.Add(permission);
        }

        await _db.SaveChangesAsync();
        return Ok(role);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.AppRoles.FindAsync(id);
        if (role == null)
        {
            return NotFound(new { message = "Designation not found." });
        }

        role.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await _db.AppUsers
            .OrderBy(x => x.Username)
            .Select(x => new
            {
                x.Id,
                x.Username,
                x.DisplayName,
                x.RoleId,
                x.IsActive
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> AddUser(UserCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Name, username and password are required."
            });
        }

        if (await _db.AppUsers.AnyAsync(x => x.Username == request.Username))
        {
            return Conflict(new { message = "Username already exists." });
        }

        if (request.Password.Length < 8 ||
            !request.Password.Any(char.IsUpper) ||
            !request.Password.Any(char.IsLower) ||
            !request.Password.Any(char.IsDigit) ||
            !request.Password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return BadRequest(new
            {
                message =
                    "Password must be at least 8 characters and include uppercase, lowercase, number and special character."
            });
        }

        var validRole = await _db.AppRoles
            .AnyAsync(x => x.Id == request.RoleId && x.IsActive);

        if (!validRole)
        {
            return BadRequest(new { message = "Select a valid designation." });
        }

        var user = new AppUser
        {
            Username = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = PasswordUtil.Hash(request.Password),
            RoleId = request.RoleId,
            IsActive = true
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            UserId = user.Id,
            user.Username,
            user.DisplayName,
            user.RoleId
        });
    }

    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> EditUser(int id, UserUpdateRequest request)
    {
        var user = await _db.AppUsers.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var validRole = await _db.AppRoles
            .AnyAsync(x => x.Id == request.RoleId && x.IsActive);

        if (!validRole)
        {
            return BadRequest(new { message = "Select a valid designation." });
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            if (request.Password.Length < 8 ||
                !request.Password.Any(char.IsUpper) ||
                !request.Password.Any(char.IsLower) ||
                !request.Password.Any(char.IsDigit) ||
                !request.Password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                return BadRequest(new
                {
                    message =
                        "Password must be at least 8 characters and include uppercase, lowercase, number and special character."
                });
            }

            user.PasswordHash = PasswordUtil.Hash(request.Password);
        }

        user.DisplayName = request.DisplayName.Trim();
        user.RoleId = request.RoleId;
        user.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Username,
            user.DisplayName,
            user.RoleId,
            user.IsActive
        });
    }

    [HttpPut("users/{userId:int}/role/{roleId:int}")]
    public async Task<IActionResult> AssignRole(int userId, int roleId)
    {
        var user = await _db.AppUsers.FindAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        var validRole = await _db.AppRoles
            .AnyAsync(x => x.Id == roleId && x.IsActive);

        if (!validRole)
        {
            return BadRequest(new { message = "Designation not found." });
        }

        user.RoleId = roleId;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
