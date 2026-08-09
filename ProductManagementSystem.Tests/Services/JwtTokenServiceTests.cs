using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using ProductManagementSystem.Api.Models;
using ProductManagementSystem.Api.Services;

namespace ProductManagementSystem.Tests.Services;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_ExpiresAt_MatchesSettingsExpiryMinutes()
    {
        var settings = new JwtSettings
        {
            Key = "super-secret-key-for-testing-only-32chars!",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpiryMinutes = 45
        };
        var sut = new JwtTokenService(settings);
        var user = new IdentityUser { Id = "u1", UserName = "test@test.com", Email = "test@test.com" };

        var before = DateTime.UtcNow;
        var result = sut.CreateToken(user, new List<string>());
        var after = DateTime.UtcNow;

        // ExpiresAt should be ~45 minutes from now, not hardcoded 60
        Assert.True(result.ExpiresAt >= before.AddMinutes(44));
        Assert.True(result.ExpiresAt <= after.AddMinutes(46));

        // Also verify the JWT itself encodes the correct expiry
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        var jwtExpiry = jwt.ValidTo;
        Assert.True(jwtExpiry >= before.AddMinutes(44));
        Assert.True(jwtExpiry <= after.AddMinutes(46));
    }
}
