using ProductManagementSystem.Api.Controllers;

namespace ProductManagementSystem.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public void Me_ResponseType_ShouldNotContainExpiresAt()
    {
        var meResponseType = typeof(AuthController).GetNestedType("MeResponse");
        Assert.NotNull(meResponseType);
        Assert.Null(meResponseType.GetProperty("ExpiresAt"));
    }

    [Fact]
    public void Me_ResponseType_ShouldContainUserIdEmailAndRoles()
    {
        var meResponseType = typeof(AuthController).GetNestedType("MeResponse");
        Assert.NotNull(meResponseType);
        Assert.NotNull(meResponseType.GetProperty("UserId"));
        Assert.NotNull(meResponseType.GetProperty("Email"));
        Assert.NotNull(meResponseType.GetProperty("Roles"));
    }
}
