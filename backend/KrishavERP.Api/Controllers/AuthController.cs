using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace KrishavERP.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public AuthController(
        AppDbContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var username = request.Username?.Trim();
        var password = request.Password ?? "";

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new
            {
                message = "Username and password are required."
            });
        }

        var user = await _db.AppUsers
            .FirstOrDefaultAsync(x =>
                x.Username == username &&
                x.IsActive);

        // Keep response deliberately generic so login cannot be used
        // to discover whether a username exists.
        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }

        if (user.LockoutEndUtc.HasValue &&
            user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remaining =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        (user.LockoutEndUtc.Value - DateTime.UtcNow)
                        .TotalMinutes));

            return StatusCode(423, new
            {
                message =
                    $"Account is temporarily locked. Try again in about {remaining} minute(s)."
            });
        }

        if (!PasswordUtil.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEndUtc =
                    DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginAttempts = 0;
            }

            await _db.SaveChangesAsync();

            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var permissions = user.RoleId.HasValue
            ? await _db.RolePermissions
                .Where(x => x.RoleId == user.RoleId.Value)
                .ToListAsync()
            : new List<RolePermission>();

        var designation = user.RoleId.HasValue
            ? await _db.AppRoles
                .Where(x => x.Id == user.RoleId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync()
            : null;

        var token = CreateToken(user);
        var logo = await _db.AppSettings
            .FirstOrDefaultAsync(x => x.Name == "Hospital Logo");

        return Ok(new
        {
            token,
            displayName = user.DisplayName,
            designation = designation ?? "",
            permissions,
            logo = logo?.Value ?? ""
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idText, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.AppUsers
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.IsActive);

        if (user == null)
        {
            return Unauthorized();
        }

        var permissions = user.RoleId.HasValue
            ? await _db.RolePermissions
                .Where(x => x.RoleId == user.RoleId.Value)
                .ToListAsync()
            : new List<RolePermission>();

        var designation = user.RoleId.HasValue
            ? await _db.AppRoles
                .Where(x => x.Id == user.RoleId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync()
            : null;

        return Ok(new
        {
            user.Id,
            user.Username,
            user.DisplayName,
            designation = designation ?? "",
            permissions
        });
    }

    private string CreateToken(AppUser user)
    {
        var keyText = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key is not configured.");

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),
            new Claim(
                ClaimTypes.Name,
                user.Username)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(keyText));

        var token = new JwtSecurityToken(
            _configuration["Jwt:Issuer"],
            _configuration["Jwt:Audience"],
            claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}
