using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProductManagementSystem.Api.Services;

namespace ProductManagementSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly JwtTokenService _tokenService;

    public AuthController(UserManager<IdentityUser> userManager, JwtTokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public record LoginRequest(string Email, string Password);

    public record LoginResponse(
        string Token,
        string UserId,
        string Email,
        IList<string> Roles,
        DateTime ExpiresAt);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await _userManager.GetRolesAsync(user);

        var token = _tokenService.CreateToken(user, roles);

        return Ok(new LoginResponse(
            token,
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            roles,
            DateTime.UtcNow.AddHours(1)));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<LoginResponse> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new LoginResponse(string.Empty, userId, email, roles, DateTime.MinValue));
    }
}
