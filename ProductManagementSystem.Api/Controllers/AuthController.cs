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
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly JwtTokenService _tokenService;

    public AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        JwtTokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
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
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // SEC-05: use SignInManager so lockout is enforced
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Account locked. Try again later." });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var tokenResult = _tokenService.CreateToken(user, roles);

        return Ok(new LoginResponse(
            tokenResult.Token,
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            roles,
            tokenResult.ExpiresAt));
    }

    public record MeResponse(string UserId, string Email, IList<string> Roles);

    [HttpGet("me")]
    [Authorize]
    public ActionResult<MeResponse> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new MeResponse(userId, email, roles));
    }
}
