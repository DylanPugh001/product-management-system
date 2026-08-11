namespace ProductManagementSystem.Tests;

/// <summary>
/// Tests in this collection run sequentially to avoid env-var race conditions.
/// JwtConfigurationTests mutates Jwt__Key; SecurityConfigTests reads it at server startup.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class IntegrationTestCollection
{
    public const string Name = "Integration";
}
